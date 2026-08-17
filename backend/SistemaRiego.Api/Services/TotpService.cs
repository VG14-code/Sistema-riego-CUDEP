using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Services;

public interface ITotpService
{
    Task<(bool Allowed, string? Error)> VerifyCriticalOperationAsync(HttpContext context, CancellationToken ct);
}

public sealed class TotpService(UserManager<User> userManager) : ITotpService
{
    public async Task<(bool Allowed, string? Error)> VerifyCriticalOperationAsync(HttpContext context, CancellationToken ct)
    {
        var rawId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(rawId, out var userId)) return (false, "Usuario no identificado.");
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !await userManager.GetTwoFactorEnabledAsync(user)) return (false, "Debe activar TOTP antes de ejecutar esta operación crítica.");
        var code = context.Request.Headers["X-TOTP-Code"].ToString().Replace(" ", string.Empty).Replace("-", string.Empty);
        if (string.IsNullOrWhiteSpace(code)) return (false, "Debe enviar el código TOTP en X-TOTP-Code.");
        return await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code)
            ? (true, null)
            : (false, "Código TOTP inválido o vencido.");
    }
}
