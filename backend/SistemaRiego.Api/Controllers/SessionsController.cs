using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/sessions"), Authorize(Policy = Policies.Operator)]
public sealed class SessionsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var userId = CurrentUserId(); var isAdmin = User.IsInRole(RoleNames.Administrator);
        return Ok(await db.Sessions.AsNoTracking().Where(x => isAdmin || x.UserId == userId).OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.Id, x.UserId, User = x.User.FullName, x.User.Email, x.CreatedAtUtc, x.ExpiresAtUtc, x.RevokedAtUtc, IsActive = x.RevokedAtUtc == null && x.ExpiresAtUtc > DateTime.UtcNow, x.IpAddress, x.UserAgent }).Take(100).ToListAsync(ct));
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        var session = await db.Sessions.FindAsync([id], ct); if (session is null) return NotFound();
        if (!User.IsInRole(RoleNames.Administrator) && session.UserId != CurrentUserId()) return Forbid();
        if (session.RevokedAtUtc is null) session.RevokedAtUtc = DateTime.UtcNow;
        db.AccessAudits.Add(new AccessAudit { UserId = CurrentUserId(), EventType = "SESSION_REVOKED", Detail = $"Sesión cerrada remotamente: {id}" }); await db.SaveChangesAsync(ct); return NoContent();
    }
    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
