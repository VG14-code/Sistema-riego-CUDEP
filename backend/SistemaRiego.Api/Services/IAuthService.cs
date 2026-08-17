using SistemaRiego.Api.Contracts;
namespace SistemaRiego.Api.Services;
public interface IAuthService
{
    Task<UserSummary> RegisterAsync(RegisterRequest request, CancellationToken ct); Task<AuthResponse?> LoginAsync(LoginRequest request, AuthContext context, CancellationToken ct);
    Task<AuthResponse?> RefreshAsync(string refreshToken, AuthContext context, CancellationToken ct); Task<bool> LogoutAsync(string refreshToken, CancellationToken ct);
    Task<string?> RequestPasswordRecoveryAsync(string email, CancellationToken ct); Task<bool> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct);
}
