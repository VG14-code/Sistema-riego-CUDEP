using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;
namespace SistemaRiego.Api.Controllers;
[ApiController, Route("api/users"), Authorize(Policy = Policies.Administrator)]
public sealed class UsersController(AppDbContext db, ITotpService? totp = null) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<UserSummary>>> GetAll(CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking().Include(x => x.UserRoles).ThenInclude(x => x.Role).OrderBy(x => x.FullName).ToListAsync(ct);
        return Ok(users.Select(x => new UserSummary(x.Id, x.Email ?? string.Empty, x.FullName, x.Status.ToString(), x.UserRoles.Select(y => y.Role.Name ?? string.Empty).Where(y => y.Length > 0).Order().ToArray())));
    }
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, UpdateUserStatusRequest request, CancellationToken ct)
    {
        if (totp is not null) { var verification = await totp.VerifyCriticalOperationAsync(HttpContext, ct); if (!verification.Allowed) return StatusCode(StatusCodes.Status403Forbidden, new { message = verification.Error }); }
        var user = await db.Users.FindAsync([id], ct); if (user is null) return NotFound(); user.Status = request.Status; user.UpdatedAtUtc = DateTime.UtcNow;
        if (request.Status != UserStatus.Active) foreach (var s in await db.Sessions.Where(x => x.UserId == id && x.RevokedAtUtc == null).ToListAsync(ct)) s.RevokedAtUtc = DateTime.UtcNow;
        db.AccessAudits.Add(new AccessAudit { UserId = id, EventType = "USER_STATUS_CHANGED", Detail = $"Estado: {request.Status}" }); await db.SaveChangesAsync(ct); return NoContent();
    }
    [HttpPut("{id:guid}/roles")]
    public async Task<IActionResult> Roles(Guid id, UpdateUserRolesRequest request, CancellationToken ct)
    {
        if (totp is not null) { var verification = await totp.VerifyCriticalOperationAsync(HttpContext, ct); if (!verification.Allowed) return StatusCode(StatusCodes.Status403Forbidden, new { message = verification.Error }); }
        var user = await db.Users.Include(x => x.UserRoles).SingleOrDefaultAsync(x => x.Id == id, ct); if (user is null) return NotFound();
        var names = request.Roles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); var roles = await db.Roles.Where(x => names.Contains(x.Name)).ToListAsync(ct);
        if (roles.Count != names.Length || roles.Count == 0) return BadRequest(new { message = "Debe indicar uno o más roles válidos." });
        db.UserRoles.RemoveRange(user.UserRoles); user.UserRoles = roles.Select(r => new UserRole { UserId = id, RoleId = r.Id }).ToList();
        db.AccessAudits.Add(new AccessAudit { UserId = id, EventType = "USER_ROLES_CHANGED", Detail = $"Roles: {string.Join(", ", roles.Select(x => x.Name))}" }); await db.SaveChangesAsync(ct); return NoContent();
    }
}
