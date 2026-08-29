using SistemaRiego.Api.Contracts;
namespace SistemaRiego.Api.Services;
public interface IAuthService
{
    Task<UserSummary> RegisterAsync(RegisterRequest request, CancellationToken ct); Task<LoginResult?> LoginAsync(LoginRequest request, AuthContext context, CancellationToken ct);
    Task<AuthResponse?> CompleteTwoFactorLoginAsync(TwoFactorLoginRequest request, AuthContext context, CancellationToken ct);
    Task<AuthResponse?> RefreshAsync(string refreshToken, AuthContext context, CancellationToken ct); Task<bool> LogoutAsync(string refreshToken, CancellationToken ct);
    Task RequestPasswordRecoveryAsync(string email, AuthContext context, CancellationToken ct); Task<bool> ResetPasswordAsync(ResetPasswordRequest request, AuthContext context, CancellationToken ct);
    Task<string?> AdminResetPasswordAsync(Guid actorId, Guid targetUserId, AuthContext context, CancellationToken ct);
    Task<AuthResponse?> ChangeRequiredPasswordAsync(Guid userId, RequiredPasswordChangeRequest request, AuthContext context, CancellationToken ct);
}
