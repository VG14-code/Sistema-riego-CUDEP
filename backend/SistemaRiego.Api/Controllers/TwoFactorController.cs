using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/security/2fa"), Authorize]
public sealed class TwoFactorController(UserManager<User> userManager) : ControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var user = await Current(); if (user is null) return Unauthorized();
        return Ok(new { enabled = await userManager.GetTwoFactorEnabledAsync(user), hasAuthenticator = !string.IsNullOrWhiteSpace(await userManager.GetAuthenticatorKeyAsync(user)) });
    }

    [HttpPost("setup")]
    public async Task<IActionResult> Setup()
    {
        var user = await Current(); if (user is null) return Unauthorized();
        await userManager.ResetAuthenticatorKeyAsync(user);
        var key = await userManager.GetAuthenticatorKeyAsync(user) ?? throw new InvalidOperationException("No se pudo generar la clave TOTP.");
        var issuer = Uri.EscapeDataString("Sistema de Riego CUDEP");
        var account = Uri.EscapeDataString(user.Email ?? user.Id.ToString());
        return Ok(new { sharedKey = FormatKey(key), authenticatorUri = $"otpauth://totp/{issuer}:{account}?secret={key}&issuer={issuer}&digits=6" });
    }

    [HttpPost("enable")]
    public async Task<IActionResult> Enable([FromBody] TotpCodeRequest request)
    {
        var user = await Current(); if (user is null) return Unauthorized();
        if (!await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, Clean(request.Code))) return BadRequest(new { message = "Código TOTP inválido." });
        await userManager.SetTwoFactorEnabledAsync(user, true);
        var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 8);
        return Ok(new { enabled = true, recoveryCodes });
    }

    [HttpPost("disable")]
    public async Task<IActionResult> Disable([FromBody] TotpCodeRequest request)
    {
        var user = await Current(); if (user is null) return Unauthorized();
        if (!await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, Clean(request.Code))) return BadRequest(new { message = "Código TOTP inválido." });
        await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);
        return NoContent();
    }

    private Task<User?> Current() => userManager.FindByIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty);
    private static string Clean(string code) => code.Replace(" ", string.Empty).Replace("-", string.Empty);
    private static string FormatKey(string key) => string.Join(' ', Enumerable.Range(0, (key.Length + 3) / 4).Select(i => key.Substring(i * 4, Math.Min(4, key.Length - i * 4)))).ToLowerInvariant();
}

public sealed record TotpCodeRequest(string Code);
