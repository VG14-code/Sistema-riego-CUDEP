using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/crop-planning/rotations"), Authorize(Policy = Policies.Operator)]
public sealed class CropRotationsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid? zoneId, CancellationToken ct)
    {
        var query = db.CropRotationPlans.AsNoTracking();
        if (zoneId.HasValue) query = query.Where(x => x.IrrigationZoneId == zoneId);
        return Ok(await query.OrderBy(x => x.IrrigationZone.Name).ThenBy(x => x.PlannedStartDate)
            .Select(x => new { x.Id, x.IrrigationZoneId, Zone = x.IrrigationZone.Name, x.CropId, Crop = x.Crop.Name, x.PreviousCycleId, PreviousCrop = x.PreviousCycle != null ? x.PreviousCycle.Crop.Name : null, x.PlannedStartDate, x.PlannedEndDate, x.Status, x.CompatibilityNotes })
            .ToListAsync(ct));
    }

    [HttpPost, Authorize(Policy = Policies.Technician)]
    public async Task<IActionResult> Create(CropRotationRequest request, CancellationToken ct)
    {
        var error = await Validate(request, null, ct); if (error is not null) return BadRequest(new { message = error });
        var item = Map(request, new CropRotationPlan { IrrigationZoneId = request.IrrigationZoneId, CropId = request.CropId });
        db.CropRotationPlans.Add(item); await Save("CROP_ROTATION_CREATED", item.Id.ToString(), ct); return Ok(item);
    }

    [HttpPut("{id:guid}"), Authorize(Policy = Policies.Technician)]
    public async Task<IActionResult> Update(Guid id, CropRotationRequest request, CancellationToken ct)
    {
        var item = await db.CropRotationPlans.FindAsync([id], ct); if (item is null) return NotFound();
        var error = await Validate(request, id, ct); if (error is not null) return BadRequest(new { message = error });
        Map(request, item); await Save("CROP_ROTATION_UPDATED", id.ToString(), ct); return NoContent();
    }

    [HttpDelete("{id:guid}"), Authorize(Policy = Policies.Administrator)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var item = await db.CropRotationPlans.FindAsync([id], ct); if (item is null) return NotFound();
        db.Remove(item); await Save("CROP_ROTATION_DELETED", id.ToString(), ct); return NoContent();
    }

    [HttpGet("timeline/{zoneId:guid}")]
    public async Task<IActionResult> Timeline(Guid zoneId, CancellationToken ct)
    {
        var history = await db.CropCycles.AsNoTracking().Where(x => x.IrrigationZoneId == zoneId).Select(x => new { Id = x.Id.ToString(), Crop = x.Crop.Name, Start = x.SowingDate, End = x.ActualHarvestDate ?? x.ExpectedHarvestDate, Kind = "Histórico", x.Status }).ToListAsync(ct);
        var plans = await db.CropRotationPlans.AsNoTracking().Where(x => x.IrrigationZoneId == zoneId).Select(x => new { Id = x.Id.ToString(), Crop = x.Crop.Name, Start = x.PlannedStartDate, End = x.PlannedEndDate, Kind = "Planificación", x.Status }).ToListAsync(ct);
        return Ok(history.Concat(plans).OrderBy(x => x.Start));
    }

    private async Task<string?> Validate(CropRotationRequest r, Guid? id, CancellationToken ct)
    {
        if (r.PlannedEndDate <= r.PlannedStartDate) return "El fin de la rotación debe ser posterior al inicio.";
        if (!await db.IrrigationZones.AnyAsync(x => x.Id == r.IrrigationZoneId && x.IsActive, ct)) return "Zona inválida.";
        if (!await db.Crops.AnyAsync(x => x.Id == r.CropId && x.IsActive, ct)) return "Cultivo inválido.";
        if (r.PreviousCycleId.HasValue && !await db.CropCycles.AnyAsync(x => x.Id == r.PreviousCycleId && x.IrrigationZoneId == r.IrrigationZoneId, ct)) return "El ciclo anterior no pertenece a la zona.";
        var overlaps = await db.CropRotationPlans.AnyAsync(x => x.Id != id && x.IrrigationZoneId == r.IrrigationZoneId && r.PlannedStartDate <= x.PlannedEndDate && r.PlannedEndDate >= x.PlannedStartDate, ct);
        if (overlaps) return "La rotación se superpone con otra planificación de la zona.";
        var lastCrop = await db.CropCycles.Where(x => x.IrrigationZoneId == r.IrrigationZoneId).OrderByDescending(x => x.ActualHarvestDate ?? x.ExpectedHarvestDate).Select(x => (Guid?)x.CropId).FirstOrDefaultAsync(ct);
        if (lastCrop == r.CropId && string.IsNullOrWhiteSpace(r.CompatibilityNotes)) return "Repetir el mismo cultivo requiere justificar la compatibilidad o manejo sanitario.";
        return null;
    }

    private static CropRotationPlan Map(CropRotationRequest r, CropRotationPlan x) { x.IrrigationZoneId = r.IrrigationZoneId; x.CropId = r.CropId; x.PreviousCycleId = r.PreviousCycleId; x.PlannedStartDate = r.PlannedStartDate; x.PlannedEndDate = r.PlannedEndDate; x.Status = r.Status.Trim(); x.CompatibilityNotes = r.CompatibilityNotes?.Trim(); return x; }
    private async Task Save(string type, string detail, CancellationToken ct) { db.AccessAudits.Add(new AccessAudit { EventType = type, Detail = detail }); await db.SaveChangesAsync(ct); }
}

