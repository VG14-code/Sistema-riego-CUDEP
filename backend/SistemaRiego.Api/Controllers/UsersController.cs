using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/users"), Authorize(Policy = Policies.Administrator)]
public sealed class UsersController(AppDbContext db, IAuthService auth, ITotpService? totp = null) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<UserSummary>>> GetAll(CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking().Include(x => x.UserRoles).ThenInclude(x => x.Role).OrderBy(x => x.FullName).ToListAsync(ct);
        return Ok(users.Select(x => new UserSummary(x.Id, x.Email ?? string.Empty, x.FullName, x.Status.ToString(), x.UserRoles.Select(y => y.Role.Name ?? string.Empty).Where(y => y.Length > 0).Order().ToArray(), x.MustChangePassword)));
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, UpdateUserStatusRequest request, CancellationToken ct)
    {
        if (!await VerifyTotp(ct)) return StatusCode(StatusCodes.Status403Forbidden, new { message = TotpError });
        var user = await db.Users.FindAsync([id], ct);
        if (user is null) return NotFound();
        user.Status = request.Status;
        user.UpdatedAtUtc = DateTime.UtcNow;
        if (request.Status != UserStatus.Active)
            foreach (var session in await db.Sessions.Where(x => x.UserId == id && x.RevokedAtUtc == null).ToListAsync(ct)) session.RevokedAtUtc = DateTime.UtcNow;
        db.AccessAudits.Add(new AccessAudit { UserId = id, EventType = "USER_STATUS_CHANGED", Detail = $"Estado: {request.Status}" });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/roles")]
    public async Task<IActionResult> Roles(Guid id, UpdateUserRolesRequest request, CancellationToken ct)
    {
        if (!await VerifyTotp(ct)) return StatusCode(StatusCodes.Status403Forbidden, new { message = TotpError });
        var user = await db.Users.Include(x => x.UserRoles).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null) return NotFound();
        var names = request.Roles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var roles = await db.Roles.Where(x => names.Contains(x.Name)).ToListAsync(ct);
        if (roles.Count != names.Length || roles.Count == 0) return BadRequest(new { message = "Debe indicar uno o más roles válidos." });
        db.UserRoles.RemoveRange(user.UserRoles);
        user.UserRoles = roles.Select(role => new UserRole { UserId = id, RoleId = role.Id }).ToList();
        db.AccessAudits.Add(new AccessAudit { UserId = id, EventType = "USER_ROLES_CHANGED", Detail = $"Roles: {string.Join(", ", roles.Select(x => x.Name))}" });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<ActionResult<AdminPasswordResetResponse>> ResetPassword(Guid id, CancellationToken ct)
    {
        if (!await VerifyTotp(ct)) return StatusCode(StatusCodes.Status403Forbidden, new { message = TotpError });
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        try
        {
            var password = await auth.AdminResetPasswordAsync(actorId, id, Context(), ct);
            return password is null ? NotFound(new { message = "El usuario no existe." }) : Ok(new AdminPasswordResetResponse(password));
        }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }

    private string? TotpError { get; set; }
    private async Task<bool> VerifyTotp(CancellationToken ct)
    {
        if (totp is null) return true;
        var verification = await totp.VerifyCriticalOperationAsync(HttpContext, ct);
        TotpError = verification.Error;
        return verification.Allowed;
    }

    private AuthContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
}