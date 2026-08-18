using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/alerts"), Authorize(Policy = Policies.Operator)]
public sealed class AlertsController(AppDbContext db, IAlertService alerts) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> List(string? severity, string? type, string? status, string? entityType, string? entityId, int take = 200, CancellationToken ct = default)
    {
        var q = db.SystemAlerts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(severity)) q = q.Where(x => x.Severity == severity); if (!string.IsNullOrWhiteSpace(type)) q = q.Where(x => x.Type == type); if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status); if (!string.IsNullOrWhiteSpace(entityType)) q = q.Where(x => x.RelatedEntityType == entityType); if (!string.IsNullOrWhiteSpace(entityId)) q = q.Where(x => x.RelatedEntityId == entityId);
        return Ok(await q.OrderByDescending(x => x.RaisedAtUtc).Take(Math.Clamp(take, 1, 1000)).ToListAsync(ct));
    }
    [HttpPost("manual"), Authorize(Policy = Policies.Technician)]
    public async Task<ActionResult> Manual(ManualAlertRequest request, CancellationToken ct)
    { var item = await alerts.RaiseAsync(new($"MANUAL:{Guid.NewGuid():N}", request.Type, request.Severity, request.Description, request.RelatedEntityType, request.RelatedEntityId, "Prueba manual"), ct); return CreatedAtAction(nameof(List), new { id = item.Id }, item); }
    [HttpPost("{id:long}/acknowledge")]
    public async Task<ActionResult> Acknowledge(long id, CancellationToken ct)
    { var item = await db.SystemAlerts.FindAsync([id], ct); if (item is null) return NotFound(); if (item.Status == "Resuelta") return Conflict(new { message = "La alerta ya está resuelta." }); item.Status = "Reconocida"; item.AcknowledgedAtUtc = DateTime.UtcNow; item.AcknowledgedByEmail = User.FindFirstValue(ClaimTypes.Email); item.AcknowledgedByUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null; await db.SaveChangesAsync(ct); return NoContent(); }
    [HttpPost("{id:long}/resolve"), Authorize(Policy = Policies.Technician)]
    public async Task<ActionResult> Resolve(long id, CancellationToken ct)
    { var item = await db.SystemAlerts.FindAsync([id], ct); if (item is null) return NotFound(); item.Status = "Resuelta"; item.ResolvedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(ct); return NoContent(); }
}
