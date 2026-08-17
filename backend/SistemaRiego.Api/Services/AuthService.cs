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

public sealed class AuthService(AppDbContext db, IOptions<JwtOptions> options, ILogger<AuthService> logger, UserManager<User> userManager) : IAuthService
{
    private readonly JwtOptions jwt = options.Value;

    public async Task<UserSummary> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var normalized = request.Email.Trim().ToUpperInvariant();
        if (await db.Users.AnyAsync(x => x.NormalizedEmail == normalized, ct)) throw new InvalidOperationException("El correo ya se encuentra registrado.");
        ValidatePassword(request.Password);
        var email = request.Email.Trim().ToLowerInvariant();
        var user = new User { Email = email, UserName = email, EmailConfirmed = true, FullName = request.FullName.Trim() };
        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded) throw new InvalidOperationException(string.Join(" ", created.Errors.Select(x => x.Description)));
        var assigned = await userManager.AddToRoleAsync(user, RoleNames.Operator);
        if (!assigned.Succeeded) throw new InvalidOperationException(string.Join(" ", assigned.Errors.Select(x => x.Description)));
        db.AccessAudits.Add(new AccessAudit { UserId = user.Id, EventType = "USER_REGISTERED", Detail = "Cuenta creada" });
        await db.SaveChangesAsync(ct);
        return Summary((await LoadUser(normalized, ct))!);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, AuthContext context, CancellationToken ct)
    {
        var user = await LoadUser(request.Email.Trim().ToUpperInvariant(), ct);
        var locked = user is not null && await userManager.IsLockedOutAsync(user);
        if (user is null || user.Status != UserStatus.Active || locked || string.IsNullOrWhiteSpace(user.PasswordHash ?? user.Credential?.PasswordHash)) { await Failed(user?.Id, context, ct); return null; }
        var valid = await userManager.CheckPasswordAsync(user, request.Password);
        if (!valid)
        {
            await userManager.AccessFailedAsync(user);
            await Failed(user.Id, context, ct); return null;
        }
        await userManager.ResetAccessFailedCountAsync(user);
        return await CreateSession(user, context, ct);
    }

    public async Task<AuthResponse?> RefreshAsync(string raw, AuthContext context, CancellationToken ct)
    {
        var old = await db.Sessions.Include(x => x.User).ThenInclude(x => x.UserRoles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.RefreshTokenHash == Hash(raw), ct);
        if (old is null || !old.IsActive || old.User.Status != UserStatus.Active) return null;
        old.RevokedAtUtc = DateTime.UtcNow; var response = await CreateSession(old.User, context, ct, false);
        old.ReplacedBySessionId = await db.Sessions.Where(x => x.UserId == old.UserId).OrderByDescending(x => x.CreatedAtUtc).Select(x => x.Id).FirstAsync(ct);
        await db.SaveChangesAsync(ct); return response;
    }

    public async Task<bool> LogoutAsync(string raw, CancellationToken ct)
    {
        var session = await db.Sessions.SingleOrDefaultAsync(x => x.RefreshTokenHash == Hash(raw), ct); if (session is null || session.RevokedAtUtc is not null) return false;
        session.RevokedAtUtc = DateTime.UtcNow; db.AccessAudits.Add(new AccessAudit { UserId = session.UserId, EventType = "LOGOUT", Detail = "Sesión cerrada" }); await db.SaveChangesAsync(ct); return true;
    }

    public async Task<string?> RequestPasswordRecoveryAsync(string email, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == email.Trim().ToUpperInvariant(), ct); if (user is null || user.Status == UserStatus.Disabled) return null;
        var raw = await userManager.GeneratePasswordResetTokenAsync(user);
        db.PasswordRecoveryTokens.Add(new PasswordRecoveryToken { UserId = user.Id, TokenHash = Hash(raw), ExpiresAtUtc = DateTime.UtcNow.AddMinutes(jwt.PasswordRecoveryMinutes) });
        db.AccessAudits.Add(new AccessAudit { UserId = user.Id, EventType = "PASSWORD_RECOVERY_REQUESTED", Detail = "Recuperación solicitada" }); await db.SaveChangesAsync(ct);
        logger.LogInformation("Token de recuperación de desarrollo generado para {Email}", user.Email); return raw;
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        ValidatePassword(request.NewPassword);
        var token = await db.PasswordRecoveryTokens.Include(x => x.User).ThenInclude(x => x.Credential).SingleOrDefaultAsync(x => x.TokenHash == Hash(request.Token), ct);
        if (token is null || token.UsedAtUtc is not null || token.ExpiresAtUtc <= DateTime.UtcNow) return false;
        var reset = await userManager.ResetPasswordAsync(token.User, request.Token, request.NewPassword);
        if (!reset.Succeeded) return false;
        token.UsedAtUtc = DateTime.UtcNow;
        foreach (var session in await db.Sessions.Where(x => x.UserId == token.UserId && x.RevokedAtUtc == null).ToListAsync(ct)) session.RevokedAtUtc = DateTime.UtcNow;
        db.AccessAudits.Add(new AccessAudit { UserId = token.UserId, EventType = "PASSWORD_RESET", Detail = "Contraseña actualizada y sesiones revocadas" }); await db.SaveChangesAsync(ct); return true;
    }

    private async Task<AuthResponse> CreateSession(User user, AuthContext context, CancellationToken ct, bool save = true)
    {
        var now = DateTime.UtcNow; var accessExpiry = now.AddMinutes(jwt.AccessTokenMinutes); var refresh = RandomToken();
        db.Sessions.Add(new Session { UserId = user.Id, RefreshTokenHash = Hash(refresh), ExpiresAtUtc = now.AddDays(jwt.RefreshTokenDays), IpAddress = context.IpAddress, UserAgent = context.UserAgent });
        db.AccessAudits.Add(new AccessAudit { UserId = user.Id, EventType = "LOGIN_SUCCESS", Detail = "Sesión iniciada", IpAddress = context.IpAddress }); if (save) await db.SaveChangesAsync(ct);
        var roles = user.UserRoles.Select(x => x.Role.Name ?? string.Empty).Where(x => x.Length > 0).ToArray();
        var email = user.Email ?? throw new InvalidOperationException("La cuenta no tiene correo.");
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new(JwtRegisteredClaimNames.Email, email), new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Name, user.FullName) };
        claims.AddRange(roles.Select(x => new Claim(ClaimTypes.Role, x)));
        var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience, claims, now, accessExpiry, new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)), SecurityAlgorithms.HmacSha256));
        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), refresh, accessExpiry, Summary(user));
    }

    private Task<User?> LoadUser(string normalized, CancellationToken ct) => db.Users.Include(x => x.Credential).Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.NormalizedEmail == normalized, ct);
    private async Task Failed(Guid? id, AuthContext context, CancellationToken ct) { db.AccessAudits.Add(new AccessAudit { UserId = id, EventType = "LOGIN_FAILED", Detail = "Credenciales inválidas o cuenta no disponible", IpAddress = context.IpAddress }); await db.SaveChangesAsync(ct); }
    private static UserSummary Summary(User user) => new(user.Id, user.Email ?? string.Empty, user.FullName, user.Status.ToString(), user.UserRoles.Select(x => x.Role.Name ?? string.Empty).Where(x => x.Length > 0).Order().ToArray());
    private static string RandomToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static void ValidatePassword(string password) { if (password.Length < 8 || !password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit) || !password.Any(x => !char.IsLetterOrDigit(x))) throw new InvalidOperationException("La contraseña debe tener al menos 8 caracteres, mayúscula, minúscula, número y símbolo."); }
}
