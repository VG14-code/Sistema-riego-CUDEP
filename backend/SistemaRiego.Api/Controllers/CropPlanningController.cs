using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/crop-planning"), Authorize(Policy = PermissionPolicies.CropPlanningRead)]
public sealed class CropPlanningController(AppDbContext db) : ControllerBase
{
    [HttpGet("cycles")] public async Task<IActionResult> Cycles(CancellationToken ct) => Ok((await View().ToListAsync(ct)).OrderBy(x => x.SowingDate));

    [HttpPost("cycles"), Authorize(Policy = PermissionPolicies.CropPlanningManage)]
    public async Task<IActionResult> Create(CycleRequest request, CancellationToken ct)
    {
        var error = await Validate(request, null, ct); if (error is not null) return BadRequest(new { message = error });
        var cycle = Map(request, new CropCycle { Name = request.Name.Trim() }); db.Add(cycle); await Save("CROP_CYCLE_CREATED", cycle.Name, ct); return Ok(cycle);
    }

    [HttpPut("cycles/{id:guid}"), Authorize(Policy = PermissionPolicies.CropPlanningManage)]
    public async Task<IActionResult> Update(Guid id, CycleRequest request, CancellationToken ct)
    {
        var cycle = await db.CropCycles.FindAsync([id], ct); if (cycle is null) return NotFound();
        var error = await Validate(request, id, ct); if (error is not null) return BadRequest(new { message = error });
        Map(request, cycle); await Save("CROP_CYCLE_UPDATED", cycle.Name, ct); return NoContent();
    }

    [HttpDelete("cycles/{id:guid}"), Authorize(Policy = PermissionPolicies.CropPlanningDelete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var cycle = await db.CropCycles.FindAsync([id], ct); if (cycle is null) return NotFound();
        if (await db.IrrigationRuns.AnyAsync(x => x.IrrigationRule != null && x.IrrigationRule.CropWaterRequirement != null && x.IrrigationRule.CropWaterRequirement.CropId == cycle.CropId, ct)) return Conflict(new { message = "El ciclo tiene operaciones de riego relacionadas." });
        db.Remove(cycle); await Save("CROP_CYCLE_DELETED", cycle.Name, ct); return NoContent();
    }

    [HttpPatch("cycles/{id:guid}/stage/{stageId:guid}"), Authorize(Policy = PermissionPolicies.CropPlanningManage)]
    public async Task<IActionResult> ChangeStage(Guid id, Guid stageId, CancellationToken ct)
    {
        var cycle = await db.CropCycles.FindAsync([id], ct); if (cycle is null) return NotFound();
        if (!await db.PhenologicalStages.AnyAsync(x => x.Id == stageId && x.CropId == cycle.CropId, ct)) return BadRequest(new { message = "La etapa no pertenece al cultivo del ciclo." });
        cycle.CurrentStageId = stageId; await Save("CROP_STAGE_CHANGED", cycle.Name, ct); return NoContent();
    }

    [HttpGet("calendar")]
    public async Task<IActionResult> Calendar(CancellationToken ct) => Ok(await db.CropCycles.AsNoTracking().OrderBy(x => x.SowingDate).Select(x => new
    {
        id = x.Id, title = x.Name + " · " + x.Crop.Name, start = x.SowingDate, end = x.ExpectedHarvestDate.AddDays(1),
        crop = x.Crop.Name, zone = x.IrrigationZone.Name, x.Status, currentStage = x.CurrentStage != null ? x.CurrentStage.Name : "Sin etapa",
        color = x.Status == "Vencido" ? "#c74b50" : x.Status == "Activo" ? "#16886f" : "#4879b8"
    }).ToListAsync(ct));

    [HttpGet("alerts")]
    public async Task<IActionResult> Alerts(int days = 14, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow); var limit = today.AddDays(Math.Clamp(days, 1, 90));
        return Ok(await db.CropCycles.AsNoTracking().Where(x => x.ActualHarvestDate == null && x.ExpectedHarvestDate <= limit && (x.Status == "Activo" || x.Status == "Planificado" || x.Status == "Vencido"))
            .OrderBy(x => x.ExpectedHarvestDate).Select(x => new { x.Id, x.Name, Crop = x.Crop.Name, Zone = x.IrrigationZone.Name, x.ExpectedHarvestDate, DaysRemaining = x.ExpectedHarvestDate.DayNumber - today.DayNumber, Severity = x.ExpectedHarvestDate < today ? "Vencida" : "Próxima" }).ToListAsync(ct));
    }

    private IQueryable<CropCycleResponse> View() => db.CropCycles.AsNoTracking().Select(x => new CropCycleResponse(x.Id, x.Name, x.CropId, x.Crop.Name, x.IrrigationZoneId, x.IrrigationZone.Name, x.CurrentStageId, x.CurrentStage != null ? x.CurrentStage.Name : "Sin etapa", x.SowingDate, x.ExpectedHarvestDate, x.ActualHarvestDate, x.AreaHectares, x.PlantCount, x.Status, x.Notes));

    private async Task<string?> Validate(CycleRequest request, Guid? currentId, CancellationToken ct)
    {
        if (request.ExpectedHarvestDate <= request.SowingDate) return "La cosecha debe ser posterior a la siembra.";
        if (request.AreaHectares <= 0 || request.PlantCount < 0) return "Área y cantidad de plantas no son válidas.";
        if (!await db.Crops.AnyAsync(x => x.Id == request.CropId && x.IsActive, ct)) return "Cultivo inválido.";
        if (!await db.IrrigationZones.AnyAsync(x => x.Id == request.IrrigationZoneId && x.IsActive, ct)) return "Zona inválida.";
        if (request.CurrentStageId.HasValue && !await db.PhenologicalStages.AnyAsync(x => x.Id == request.CurrentStageId && x.CropId == request.CropId, ct)) return "La etapa no pertenece al cultivo.";
        var overlaps = await db.CropCycles.AnyAsync(x => x.Id != currentId && x.IrrigationZoneId == request.IrrigationZoneId && (x.Status == "Activo" || x.Status == "Planificado") && request.SowingDate <= x.ExpectedHarvestDate && request.ExpectedHarvestDate >= x.SowingDate, ct);
        return overlaps ? "El ciclo se superpone con otro ciclo activo o planificado en la misma zona." : null;
    }

    private static CropCycle Map(CycleRequest request, CropCycle cycle) { cycle.CropId = request.CropId; cycle.IrrigationZoneId = request.IrrigationZoneId; cycle.CurrentStageId = request.CurrentStageId; cycle.Name = request.Name.Trim(); cycle.SowingDate = request.SowingDate; cycle.ExpectedHarvestDate = request.ExpectedHarvestDate; cycle.ActualHarvestDate = request.ActualHarvestDate; cycle.AreaHectares = request.AreaHectares; cycle.PlantCount = request.PlantCount; cycle.Status = request.Status.Trim(); cycle.Notes = request.Notes?.Trim(); return cycle; }
    private async Task Save(string type, string detail, CancellationToken ct) { db.AccessAudits.Add(new AccessAudit { EventType = type, Detail = detail }); await db.SaveChangesAsync(ct); }
}
