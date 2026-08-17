using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/audit"), Authorize(Policy = Policies.Administrator)]
public sealed class AuditController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<AuditResponse>>> Get([FromQuery] string? eventType, [FromQuery] int take = 100, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 300);
        var query = from audit in db.AccessAudits.AsNoTracking()
                    join user in db.Users.AsNoTracking() on audit.UserId equals user.Id into users
                    from user in users.DefaultIfEmpty()
                    select new { audit, user };
        if (!string.IsNullOrWhiteSpace(eventType)) query = query.Where(x => x.audit.EventType == eventType.Trim().ToUpperInvariant());
        var entries = await query.OrderByDescending(x => x.audit.OccurredAtUtc).Take(take).ToListAsync(ct);
        return Ok(entries.Select(x => new AuditResponse(x.audit.Id, x.audit.UserId, x.user == null ? null : x.user.Email, x.audit.EventType, x.audit.Detail, x.audit.OccurredAtUtc, x.audit.IpAddress)));
    }
}
