using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Services;

public sealed class AuthService(
    AppDbContext db,
    IOptions<JwtOptions> options,
    IOptions<EmailOptions> emailOptions,
    ILogger<AuthService> logger,
    UserManager<User> userManager,
    IEmailSender emailSender,
    IPermissionResolver permissionResolver) : IAuthService
{
    private readonly JwtOptions jwt = options.Value;
    private readonly EmailOptions email = emailOptions.Value;
    private const int MaxTwoFactorAttempts = 5;

    public async Task<UserSummary> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var normalized = request.Email.Trim().ToUpperInvariant();
        if (await db.Users.AnyAsync(x => x.NormalizedEmail == normalized, ct)) throw new InvalidOperationException("El correo ya se encuentra registrado.");
        ValidatePassword(request.Password);
        var address = request.Email.Trim().ToLowerInvariant();
        var user = new User { Email = address, UserName = address, EmailConfirmed = true, FullName = request.FullName.Trim() };
        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded) throw new InvalidOperationException(Errors(created));
        var assigned = await userManager.AddToRoleAsync(user, RoleNames.Operator);
        if (!assigned.Succeeded) throw new InvalidOperationException(Errors(assigned));
        db.AccessAudits.Add(new AccessAudit { UserId = user.Id, EventType = "USER_REGISTERED", Detail = "Cuenta creada" });
        await db.SaveChangesAsync(ct);
        return Summary((await LoadUser(normalized, ct))!);
    }

    public async Task<LoginResult?> LoginAsync(LoginRequest request, AuthContext context, CancellationToken ct)
    {
        var user = await LoadUser(request.Email.Trim().ToUpperInvariant(), ct);
        var locked = user is not null && await userManager.IsLockedOutAsync(user);
        if (user is null || user.Status != UserStatus.Active || locked || string.IsNullOrWhiteSpace(user.PasswordHash ?? user.Credential?.PasswordHash))
        {
            await Failed(user?.Id, context, ct);
            return null;
        }
        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            await ApplyConfiguredLockoutAsync(user, ct);
            await Failed(user.Id, context, ct);
            return null;
        }
        await userManager.ResetAccessFailedCountAsync(user);

        // La contraseña correcta no basta cuando la cuenta tiene segundo factor: aquí
        // no se emite ningún token de acceso, solo un desafío de un solo uso que
        // /auth/login/2fa canjea despues de verificar el codigo.
        if (await userManager.GetTwoFactorEnabledAsync(user))
            return new LoginResult(null, await CreateTwoFactorChallenge(user, context, ct));

        return new LoginResult(await CreateSession(user, context, ct), null);
    }

    public async Task<AuthResponse?> CompleteTwoFactorLoginAsync(TwoFactorLoginRequest request, AuthContext context, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var challenge = await db.TwoFactorLoginChallenges.Include(x => x.User).ThenInclude(x => x.UserRoles).ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.TokenHash == Hash(request.ChallengeToken), ct);
        if (challenge is null || challenge.UsedAtUtc is not null || challenge.ExpiresAtUtc <= now || challenge.AttemptCount >= MaxTwoFactorAttempts)
        {
            await Failed(challenge?.UserId, context, ct);
            return null;
        }
        var user = challenge.User;
        // El desafio esta ligado al correo con el que se pidio: un token valido no
        // sirve para completar el acceso de otra cuenta.
        if (user.Status != UserStatus.Active || !string.Equals(user.NormalizedEmail, request.Email.Trim().ToUpperInvariant(), StringComparison.Ordinal))
        {
            challenge.UsedAtUtc = now;
            await Failed(user.Id, context, ct);
            return null;
        }

        challenge.AttemptCount++;
        // El TOTP se normaliza quitando separadores, pero los codigos de recuperacion
        // de Identity incluyen el guion y deben canjearse tal como se entregaron.
        var entered = request.Code.Trim();
        var code = entered.Replace(" ", string.Empty).Replace("-", string.Empty);
        var verified = await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code);
        if (!verified)
        {
            // Los codigos de recuperacion generados al activar el 2FA tambien valen
            // aqui; cada uno se consume al usarse.
            // RedeemTwoFactorRecoveryCodeAsync lanza si el codigo viene vacio.
            var redeemed = string.IsNullOrWhiteSpace(entered) ? IdentityResult.Failed() : await userManager.RedeemTwoFactorRecoveryCodeAsync(user, entered);
            verified = redeemed.Succeeded;
            if (verified) db.AccessAudits.Add(new AccessAudit { UserId = user.Id, EventType = "LOGIN_2FA_RECOVERY_CODE", Detail = "Acceso completado con código de recuperación", IpAddress = context.IpAddress });
        }
        if (!verified)
        {
            if (challenge.AttemptCount >= MaxTwoFactorAttempts) challenge.UsedAtUtc = now;
            db.AccessAudits.Add(new AccessAudit { UserId = user.Id, EventType = "LOGIN_2FA_FAILED", Detail = $"Código de segundo factor inválido (intento {challenge.AttemptCount} de {MaxTwoFactorAttempts})", IpAddress = context.IpAddress });
            await db.SaveChangesAsync(ct);
            return null;
        }

        challenge.UsedAtUtc = now;
        return await CreateSession(user, context, ct);
    }

    private async Task<TwoFactorChallengeResponse> CreateTwoFactorChallenge(User user, AuthContext context, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        foreach (var previous in await db.TwoFactorLoginChallenges.Where(x => x.UserId == user.Id && x.UsedAtUtc == null).ToListAsync(ct)) previous.UsedAtUtc = now;
        var raw = RandomToken();
        var expires = now.AddMinutes(Math.Max(1, jwt.TwoFactorChallengeMinutes));
        db.TwoFactorLoginChallenges.Add(new TwoFactorLoginChallenge { UserId = user.Id, TokenHash = Hash(raw), ExpiresAtUtc = expires, IpAddress = context.IpAddress });
        db.AccessAudits.Add(new AccessAudit { UserId = user.Id, EventType = "LOGIN_2FA_REQUIRED", Detail = "Contraseña verificada; pendiente el segundo factor", IpAddress = context.IpAddress });
        await db.SaveChangesAsync(ct);
        return new TwoFactorChallengeResponse(raw, expires);
    }

    public async Task<AuthResponse?> RefreshAsync(string raw, AuthContext context, CancellationToken ct)
    {
        var old = await db.Sessions.Include(x => x.User).ThenInclude(x => x.UserRoles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.RefreshTokenHash == Hash(raw), ct);
        if (old is null || !old.IsActive || old.User.Status != UserStatus.Active) return null;
        old.RevokedAtUtc = DateTime.UtcNow;
        var response = await CreateSession(old.User, context, ct, false);
        old.ReplacedBySessionId = await db.Sessions.Where(x => x.UserId == old.UserId).OrderByDescending(x => x.CreatedAtUtc).Select(x => x.Id).FirstAsync(ct);
        await db.SaveChangesAsync(ct);
        return response;
    }

    public async Task<bool> LogoutAsync(string raw, CancellationToken ct)
    {
        var session = await db.Sessions.SingleOrDefaultAsync(x => x.RefreshTokenHash == Hash(raw), ct);
        if (session is null || session.RevokedAtUtc is not null) return false;
        session.RevokedAtUtc = DateTime.UtcNow;
        db.AccessAudits.Add(new AccessAudit { UserId = session.UserId, EventType = "LOGOUT", Detail = "Sesión cerrada" });
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task RequestPasswordRecoveryAsync(string address, AuthContext context, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == address.Trim().ToUpperInvariant(), ct);
        db.AccessAudits.Add(new AccessAudit { UserId = user?.Id, EventType = "PASSWORD_RECOVERY_REQUESTED", Detail = "Solicitud de recuperación recibida", IpAddress = context.IpAddress });
        if (user is null || user.Status == UserStatus.Disabled)
        {
            await db.SaveChangesAsync(ct);
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var previous in await db.PasswordRecoveryTokens.Where(x => x.UserId == user.Id && x.UsedAtUtc == null).ToListAsync(ct)) previous.UsedAtUtc = now;
        var raw = await userManager.GeneratePasswordResetTokenAsync(user);
        var expires = now.AddMinutes(jwt.PasswordRecoveryMinutes);
        var recovery = new PasswordRecoveryToken { UserId = user.Id, TokenHash = Hash(raw), ExpiresAtUtc = expires };
        db.PasswordRecoveryTokens.Add(recovery);
        await db.SaveChangesAsync(ct);

        var link = $"{email.FrontendBaseUrl.TrimEnd('/')}/?resetToken={Uri.EscapeDataString(raw)}";
        try
        {
            await emailSender.SendPasswordRecoveryAsync(user.Email!, user.FullName, link, expires, ct);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "No fue posible entregar el correo de recuperación para la solicitud {RecoveryRequestId}", recovery.Id);
        }
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request, AuthContext context, CancellationToken ct)
    {
        ValidatePassword(request.NewPassword);
        var token = await db.PasswordRecoveryTokens.Include(x => x.User).SingleOrDefaultAsync(x => x.TokenHash == Hash(request.Token), ct);
        var now = DateTime.UtcNow;
        if (token is null || token.UsedAtUtc is not null || token.ExpiresAtUtc <= now) return false;
        var reset = await userManager.ResetPasswordAsync(token.User, request.Token, request.NewPassword);
        if (!reset.Succeeded) return false;

        token.User.MustChangePassword = false;
        token.User.UpdatedAtUtc = now;
        foreach (var recovery in await db.PasswordRecoveryTokens.Where(x => x.UserId == token.UserId && x.UsedAtUtc == null).ToListAsync(ct)) recovery.UsedAtUtc = now;
        await RevokeSessions(token.UserId, now, ct);
        db.AccessAudits.Add(new AccessAudit { UserId = token.UserId, EventType = "PASSWORD_RESET", Detail = "Contraseña actualizada y sesiones revocadas", IpAddress = context.IpAddress });
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<string?> AdminResetPasswordAsync(Guid actorId, Guid targetUserId, AuthContext context, CancellationToken ct)
    {
        var target = await db.Users.SingleOrDefaultAsync(x => x.Id == targetUserId, ct);
        if (target is null) return null;
        var temporaryPassword = TemporaryPassword();
        var identityToken = await userManager.GeneratePasswordResetTokenAsync(target);
        var reset = await userManager.ResetPasswordAsync(target, identityToken, temporaryPassword);
        if (!reset.Succeeded) throw new InvalidOperationException(Errors(reset));

        var now = DateTime.UtcNow;
        target.MustChangePassword = true;
        target.UpdatedAtUtc = now;
        foreach (var recovery in await db.PasswordRecoveryTokens.Where(x => x.UserId == targetUserId && x.UsedAtUtc == null).ToListAsync(ct)) recovery.UsedAtUtc = now;
        await RevokeSessions(targetUserId, now, ct);
        db.AccessAudits.Add(new AccessAudit { UserId = actorId, EventType = "PASSWORD_ADMIN_RESET", Detail = $"Contraseña restablecida administrativamente para usuario {targetUserId}; cambio obligatorio activado", IpAddress = context.IpAddress });
        await db.SaveChangesAsync(ct);
        return temporaryPassword;
    }

    public async Task<AuthResponse?> ChangeRequiredPasswordAsync(Guid userId, RequiredPasswordChangeRequest request, AuthContext context, CancellationToken ct)
    {
        ValidatePassword(request.NewPassword);
        var user = await LoadUser(userId, ct);
        if (user is null || !user.MustChangePassword || user.Status != UserStatus.Active) return null;
        var changed = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!changed.Succeeded) throw new InvalidOperationException(Errors(changed));

        var now = DateTime.UtcNow;
        user.MustChangePassword = false;
        user.UpdatedAtUtc = now;
        await RevokeSessions(userId, now, ct);
        db.AccessAudits.Add(new AccessAudit { UserId = userId, EventType = "PASSWORD_REQUIRED_CHANGE_COMPLETED", Detail = "Contraseña temporal reemplazada y sesiones anteriores revocadas", IpAddress = context.IpAddress });
        return await CreateSession(user, context, ct);
    }

    private async Task<AuthResponse> CreateSession(User user, AuthContext context, CancellationToken ct, bool save = true)
    {
        var now = DateTime.UtcNow;
        var accessExpiry = now.AddMinutes(jwt.AccessTokenMinutes);
        var refresh = RandomToken();
        db.Sessions.Add(new Session { UserId = user.Id, RefreshTokenHash = Hash(refresh), ExpiresAtUtc = now.AddDays(jwt.RefreshTokenDays), IpAddress = context.IpAddress, UserAgent = context.UserAgent });
        db.AccessAudits.Add(new AccessAudit { UserId = user.Id, EventType = "LOGIN_SUCCESS", Detail = user.MustChangePassword ? "Sesión restringida: cambio de contraseña requerido" : "Sesión iniciada", IpAddress = context.IpAddress });
        if (save) await db.SaveChangesAsync(ct);

        var roles = user.UserRoles.Select(x => x.Role.Name ?? string.Empty).Where(x => x.Length > 0).ToArray();
        var permissions = await permissionResolver.GetEffectivePermissionsAsync(user.Id, ct);
        var address = user.Email ?? throw new InvalidOperationException("La cuenta no tiene correo.");
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, address),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new("security_stamp", await userManager.GetSecurityStampAsync(user)),
            new("pwd_change_required", user.MustChangePassword ? "true" : "false")
        };
        claims.AddRange(roles.Select(x => new Claim(ClaimTypes.Role, x)));
        claims.AddRange(permissions.Select(x => new Claim("perm", x)));
        var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience, claims, now, accessExpiry, new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)), SecurityAlgorithms.HmacSha256));
        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), refresh, accessExpiry, Summary(user));
    }

    private async Task RevokeSessions(Guid userId, DateTime now, CancellationToken ct)
    {
        foreach (var session in await db.Sessions.Where(x => x.UserId == userId && x.RevokedAtUtc == null).ToListAsync(ct)) session.RevokedAtUtc = now;
    }

    private Task<User?> LoadUser(string normalized, CancellationToken ct) => db.Users.Include(x => x.Credential).Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.NormalizedEmail == normalized, ct);
    private Task<User?> LoadUser(Guid id, CancellationToken ct) => db.Users.Include(x => x.Credential).Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.Id == id, ct);

    // Identity bloquea con su propio MaxFailedAccessAttempts, fijo en el arranque.
    // MAX_LOGIN_ATTEMPTS permite endurecer ese limite sin reiniciar: si el parametro
    // es menor, el bloqueo se aplica antes; si es mayor o no existe, manda Identity.
    private async Task ApplyConfiguredLockoutAsync(User user, CancellationToken ct)
    {
        var raw = await db.GlobalParameters.AsNoTracking().Where(x => x.Key == "MAX_LOGIN_ATTEMPTS").Select(x => x.Value).SingleOrDefaultAsync(ct);
        if (!int.TryParse(raw, out var maximum) || maximum < 1) return;
        if (await userManager.IsLockedOutAsync(user)) return;
        if (await userManager.GetAccessFailedCountAsync(user) < maximum) return;
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.Add(userManager.Options.Lockout.DefaultLockoutTimeSpan));
        db.AccessAudits.Add(new AccessAudit { UserId = user.Id, EventType = "LOGIN_LOCKED_OUT", Detail = $"Cuenta bloqueada tras {maximum} intentos fallidos (MAX_LOGIN_ATTEMPTS)." });
    }

    private async Task Failed(Guid? id, AuthContext context, CancellationToken ct)
    {
        db.AccessAudits.Add(new AccessAudit { UserId = id, EventType = "LOGIN_FAILED", Detail = "Credenciales inválidas o cuenta no disponible", IpAddress = context.IpAddress });
        await db.SaveChangesAsync(ct);
    }

    private static UserSummary Summary(User user) => new(user.Id, user.Email ?? string.Empty, user.FullName, user.Status.ToString(), user.UserRoles.Select(x => x.Role.Name ?? string.Empty).Where(x => x.Length > 0).Order().ToArray(), user.MustChangePassword);
    private static string RandomToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    private static string TemporaryPassword() => $"Tmp!7a{Convert.ToHexString(RandomNumberGenerator.GetBytes(8))}";
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static string Errors(IdentityResult result) => string.Join(" ", result.Errors.Select(x => x.Description));
    private static void ValidatePassword(string password)
    {
        if (password.Length < 8 || !password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit) || !password.Any(x => !char.IsLetterOrDigit(x)))
            throw new InvalidOperationException("La contraseña debe tener al menos 8 caracteres, mayúscula, minúscula, número y símbolo.");
    }
}