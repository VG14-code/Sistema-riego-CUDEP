using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(IAuthService auth, ITotpService? totp = null) : ControllerBase
{
    [HttpPost("register"), Authorize(Policy = PermissionPolicies.UsersManage)]
    public async Task<ActionResult<UserSummary>> Register(RegisterRequest request, CancellationToken ct)
    {
        if (totp is not null)
        {
            var verification = await totp.VerifyCriticalOperationAsync(HttpContext, ct);
            if (!verification.Allowed) return StatusCode(StatusCodes.Status403Forbidden, new { message = verification.Error });
        }
        try { return StatusCode(201, await auth.RegisterAsync(request, ct)); }
        catch (PasswordPolicyException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, Context(), ct);
        if (result is null) return Unauthorized(new { message = "Credenciales inválidas o cuenta no disponible." });
        // Con segundo factor activo la respuesta no trae token: solo el desafío que
        // /auth/login/2fa canjea después de verificar el código.
        if (result.Challenge is not null) return Ok(result.Challenge);
        return SessionResponse(result.Session!);
    }

    [HttpPost("login/2fa"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> LoginTwoFactor(TwoFactorLoginRequest request, CancellationToken ct)
    {
        var result = await auth.CompleteTwoFactorLoginAsync(request, Context(), ct);
        return result is null ? Unauthorized(new { message = "El código es inválido, expiró o el desafío ya no es válido." }) : SessionResponse(result);
    }

    [HttpPost("refresh"), AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken ct)
    {
        var refreshToken = Token(request.RefreshToken);
        if (string.IsNullOrWhiteSpace(refreshToken)) return Unauthorized(new { message = "La sesión no contiene un token de renovación." });
        var result = await auth.RefreshAsync(refreshToken, Context(), ct);
        return result is null ? Unauthorized(new { message = "La sesión no es válida o ha expirado." }) : SessionResponse(result);
    }

    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken ct)
    {
        var refreshToken = Token(request.RefreshToken);
        if (string.IsNullOrWhiteSpace(refreshToken)) return BadRequest(new { message = "La sesión ya no está activa." });
        var closed = await auth.LogoutAsync(refreshToken, ct);
        ClearRefreshCookie();
        return closed ? NoContent() : BadRequest(new { message = "La sesión ya no está activa." });
    }

    [HttpPost("forgot-password"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<ActionResult<ForgotPasswordResponse>> Forgot(ForgotPasswordRequest request, CancellationToken ct)
    {
        await auth.RequestPasswordRecoveryAsync(request.Email, Context(), ct);
        return Accepted(new ForgotPasswordResponse("Si el correo está registrado, recibirás un enlace en unos minutos."));
    }

    [HttpPost("reset-password"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> Reset(ResetPasswordRequest request, CancellationToken ct)
    {
        try { return await auth.ResetPasswordAsync(request, Context(), ct) ? NoContent() : BadRequest(new { message = "El enlace es inválido, ya fue utilizado o expiró." }); }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpPost("change-required-password"), Authorize]
    public async Task<ActionResult<AuthResponse>> ChangeRequiredPassword(RequiredPasswordChangeRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        try
        {
            var response = await auth.ChangeRequiredPasswordAsync(userId, request, Context(), ct);
            return response is null ? BadRequest(new { message = "No existe un cambio obligatorio pendiente o la cuenta no está disponible." }) : SessionResponse(response);
        }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }

    private AuthContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());

    private OkObjectResult SessionResponse(AuthResponse response)
    {
        Response.Cookies.Append("riego.refresh", response.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth",
            Expires = DateTimeOffset.UtcNow.AddDays(7),
            IsEssential = true
        });
        return Ok(response);
    }

    private string? Token(string? bodyToken) => string.IsNullOrWhiteSpace(bodyToken)
        ? Request.Cookies["riego.refresh"]
        : bodyToken;

    private void ClearRefreshCookie() => Response.Cookies.Delete("riego.refresh", new CookieOptions
    {
        HttpOnly = true,
        Secure = Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth"
    });
}