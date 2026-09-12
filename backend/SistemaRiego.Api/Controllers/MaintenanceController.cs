using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/maintenance"), Authorize(Policy = PermissionPolicies.MaintenanceManage)]
public sealed class MaintenanceController(AppDbContext db) : ControllerBase
{
    [HttpGet("plans")] public async Task<ActionResult> Plans(CancellationToken ct) => Ok(await db.MaintenancePlans.AsNoTracking().OrderBy(x => x.ScheduledAtUtc).ToListAsync(ct));
    [HttpPost("plans")] public async Task<ActionResult> CreatePlan(MaintenancePlanRequest r, CancellationToken ct) { if (!await IsRegisteredEquipmentAsync(r, ct)) return UnknownEquipment(); var x = Plan(new MaintenancePlan { Name = r.Name, EquipmentType = r.EquipmentType, EquipmentId = r.EquipmentId }, r); db.Add(x); await db.SaveChangesAsync(ct); return Ok(x); }
    [HttpPut("plans/{id:guid}")] public async Task<ActionResult> UpdatePlan(Guid id, MaintenancePlanRequest r, CancellationToken ct) { var x = await db.MaintenancePlans.FindAsync([id], ct); if (x is null) return NotFound(); if (!await IsRegisteredEquipmentAsync(r, ct)) return UnknownEquipment(); Plan(x, r); await db.SaveChangesAsync(ct); return Ok(x); }

    /// <summary>Equipos que pueden recibir un plan. Sustituye al ID que antes se escribia a mano.</summary>
    [HttpGet("equipment")] public async Task<ActionResult> Equipment(CancellationToken ct) => Ok(await EquipmentOptionsAsync(ct));

    private async Task<List<MaintenanceEquipmentOption>> EquipmentOptionsAsync(CancellationToken ct)
    {
        var valveIds = await db.IrrigationZoneValves.AsNoTracking().Select(x => x.DeviceId).Distinct().ToListAsync(ct);
        var options = new List<MaintenanceEquipmentOption>();
        options.AddRange(await db.WaterPumps.AsNoTracking().OrderBy(x => x.Code).Select(x => new MaintenanceEquipmentOption("Bomba", x.Id.ToString(), x.Code)).ToListAsync(ct));
        options.AddRange(await db.IoTDevices.AsNoTracking().OrderBy(x => x.Name).Select(x => new MaintenanceEquipmentOption("Dispositivo IoT", x.Id.ToString(), x.Name + " · " + x.Code)).ToListAsync(ct));
        options.AddRange(await db.IoTSensors.AsNoTracking().OrderBy(x => x.Name).Select(x => new MaintenanceEquipmentOption("Sensor", x.Id.ToString(), x.Name + " · " + x.Code)).ToListAsync(ct));
        options.AddRange(await db.IoTDevices.AsNoTracking().Where(x => valveIds.Contains(x.Id)).OrderBy(x => x.Name).Select(x => new MaintenanceEquipmentOption("Válvula", x.Id.ToString(), x.Name + " · " + x.Code)).ToListAsync(ct));
        options.AddRange(await db.SolarPanelArrays.AsNoTracking().OrderBy(x => x.Name).Select(x => new MaintenanceEquipmentOption("Panel solar", x.Id.ToString(), x.Name)).ToListAsync(ct));
        return options;
    }

    private async Task<bool> IsRegisteredEquipmentAsync(MaintenancePlanRequest r, CancellationToken ct) =>
        (await EquipmentOptionsAsync(ct)).Any(x => x.Type == r.EquipmentType && string.Equals(x.Id, r.EquipmentId, StringComparison.OrdinalIgnoreCase));

    private BadRequestObjectResult UnknownEquipment() => BadRequest(new { message = "Selecciona un equipo registrado del tipo indicado." });
    [HttpDelete("plans/{id:guid}")] public async Task<ActionResult> DeletePlan(Guid id, CancellationToken ct) { var x = await db.MaintenancePlans.FindAsync([id], ct); if (x is null) return NotFound(); db.Remove(x); await db.SaveChangesAsync(ct); return NoContent(); }
    [HttpGet("activities")] public async Task<ActionResult> Activities(CancellationToken ct) => Ok(await db.MaintenanceActivities.AsNoTracking().OrderByDescending(x => x.ScheduledAtUtc).ToListAsync(ct));
    [HttpPost("activities")] public async Task<ActionResult> CreateActivity(MaintenanceActivityRequest r, CancellationToken ct) { var x = Activity(new MaintenanceActivity { Title = r.Title, EquipmentType = r.EquipmentType, EquipmentId = r.EquipmentId }, r); db.Add(x); await db.SaveChangesAsync(ct); return Ok(x); }
    [HttpPut("activities/{id:long}")] public async Task<ActionResult> UpdateActivity(long id, MaintenanceActivityRequest r, CancellationToken ct) { var x = await db.MaintenanceActivities.FindAsync([id], ct); if (x is null) return NotFound(); Activity(x, r); await db.SaveChangesAsync(ct); return Ok(x); }
    [HttpDelete("activities/{id:long}")] public async Task<ActionResult> DeleteActivity(long id, CancellationToken ct) { var x = await db.MaintenanceActivities.FindAsync([id], ct); if (x is null) return NotFound(); db.Remove(x); await db.SaveChangesAsync(ct); return NoContent(); }
    [HttpGet("incidents")] public async Task<ActionResult> Incidents(string? status, CancellationToken ct)
    {
        var q = db.MaintenanceIncidents.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status);
        var items = await q.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
        var ids = items.Select(x => Guid.TryParse(x.EquipmentId, out var id) ? id : Guid.Empty).Where(x => x != Guid.Empty).Distinct().ToList();
        var names = await db.IoTDevices.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name + " · " + x.Code, ct);
        foreach (var item in items)
            if (Guid.TryParse(item.EquipmentId, out var id) && names.TryGetValue(id, out var name))
                item.EquipmentName = name;
        return Ok(items);
    }
    [HttpPost("incidents")] public async Task<ActionResult> CreateIncident(MaintenanceIncidentRequest r, CancellationToken ct) { var x = new MaintenanceIncident { Title = r.Title, Description = r.Description, EquipmentType = r.EquipmentType, EquipmentId = r.EquipmentId, Severity = r.Severity, AssignedToUserId = r.AssignedToUserId, AssignedToEmail = r.AssignedToEmail, Notes = r.Notes, Status = r.Status, Origin = "Manual" }; db.Add(x); await db.SaveChangesAsync(ct); return Ok(x); }
    [HttpPut("incidents/{id:long}")] public async Task<ActionResult> UpdateIncident(long id, MaintenanceIncidentUpdate r, CancellationToken ct) { var x = await db.MaintenanceIncidents.FindAsync([id], ct); if (x is null) return NotFound(); x.Status = r.Status; x.AssignedToUserId = r.AssignedToUserId; x.AssignedToEmail = r.AssignedToEmail; x.Notes = r.Notes; x.ResolvedAtUtc = r.Status == "Resuelta" ? DateTime.UtcNow : null; await db.SaveChangesAsync(ct); return Ok(x); }
    [HttpDelete("incidents/{id:long}")] public async Task<ActionResult> DeleteIncident(long id, CancellationToken ct) { var x = await db.MaintenanceIncidents.FindAsync([id], ct); if (x is null) return NotFound(); db.Remove(x); await db.SaveChangesAsync(ct); return NoContent(); }
    [HttpGet("equipment/{equipmentType}/{equipmentId}/history")] public async Task<ActionResult> History(string equipmentType, string equipmentId, CancellationToken ct) => Ok(new { plans = await db.MaintenancePlans.AsNoTracking().Where(x => x.EquipmentType == equipmentType && x.EquipmentId == equipmentId).ToListAsync(ct), activities = await db.MaintenanceActivities.AsNoTracking().Where(x => x.EquipmentType == equipmentType && x.EquipmentId == equipmentId).ToListAsync(ct), incidents = await db.MaintenanceIncidents.AsNoTracking().Where(x => x.EquipmentType == equipmentType && x.EquipmentId == equipmentId).ToListAsync(ct) });
    private static MaintenancePlan Plan(MaintenancePlan x, MaintenancePlanRequest r) { x.Name = r.Name; x.Frequency = r.Frequency; x.IntervalDays = r.IntervalDays; x.EquipmentType = r.EquipmentType; x.EquipmentId = r.EquipmentId; x.ScheduledAtUtc = r.ScheduledAtUtc; x.NextDueAtUtc = r.IntervalDays.HasValue ? r.ScheduledAtUtc.AddDays(r.IntervalDays.Value) : r.ScheduledAtUtc; x.AssignedToUserId = r.AssignedToUserId; x.AssignedToEmail = r.AssignedToEmail; x.Notes = r.Notes; x.Status = r.Status; return x; }
    private static MaintenanceActivity Activity(MaintenanceActivity x, MaintenanceActivityRequest r) { x.MaintenancePlanId = r.MaintenancePlanId; x.Title = r.Title; x.EquipmentType = r.EquipmentType; x.EquipmentId = r.EquipmentId; x.ScheduledAtUtc = r.ScheduledAtUtc; x.AssignedToUserId = r.AssignedToUserId; x.AssignedToEmail = r.AssignedToEmail; x.Notes = r.Notes; x.Status = r.Status; x.PerformedAtUtc = r.PerformedAtUtc; return x; }
}

public sealed record MaintenanceEquipmentOption(string Type, string Id, string Name);
