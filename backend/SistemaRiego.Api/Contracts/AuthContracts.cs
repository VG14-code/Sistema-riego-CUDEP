using System.ComponentModel.DataAnnotations;
namespace SistemaRiego.Api.Contracts;
public sealed record RegisterRequest([Required, EmailAddress] string Email, [Required, MinLength(8)] string Password, [Required, MinLength(3), MaxLength(150)] string FullName);
public sealed record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);
public sealed record RefreshRequest([Required] string RefreshToken); public sealed record LogoutRequest([Required] string RefreshToken);
public sealed record ForgotPasswordRequest([Required, EmailAddress] string Email); public sealed record ResetPasswordRequest([Required] string Token, [Required, MinLength(8)] string NewPassword);
public sealed record AuthResponse(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAtUtc, UserSummary User);
public sealed record UserSummary(Guid Id, string Email, string FullName, string Status, IReadOnlyCollection<string> Roles);
public sealed record ForgotPasswordResponse(string Message, string? DevelopmentToken = null); public sealed record AuthContext(string? IpAddress, string? UserAgent);
