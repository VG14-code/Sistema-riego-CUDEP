using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/agronomy"), Authorize(Policy = PermissionPolicies.AgronomyRead)]
public sealed class AgronomyController(AppDbContext db, IIrrigationRecommendationCalculator? recommendationCalculator = null, ITotpService? totp = null, IIrrigationCommandService? commands = null) : ControllerBase
{
    private readonly IIrrigationRecommendationCalculator calculator = recommendationCalculator ?? new IrrigationRecommendationCalculator();

    [HttpGet("soil-types")] public async Task<IActionResult> SoilTypes(CancellationToken ct) => Ok(await db.SoilTypes.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct));
    [HttpPost("soil-types"), Authorize(Policy = PermissionPolicies.AgronomyManage)] public async Task<IActionResult> CreateSoil(SoilTypeRequest r, CancellationToken ct) { var error = ValidateSoil(r); if (error is not null) return BadRequest(Message(error)); var code = await ResolveCodeAsync(r.Code, db.SoilTypes.Select(x => x.Code), "SUELO", ct); if (await db.SoilTypes.AnyAsync(x => x.Code == code, ct)) return Conflict(Message("El código de suelo ya existe.")); var x = Map(r, new SoilType { Code = code, Name = r.Name.Trim() }); db.Add(x); await Save("SOIL_CREATED", code, ct); return Ok(x); }
    [HttpPut("soil-types/{id:guid}"), Authorize(Policy = PermissionPolicies.AgronomyManage)] public async Task<IActionResult> UpdateSoil(Guid id, SoilTypeRequest r, CancellationToken ct) { var error = ValidateSoil(r); if (error is not null) return BadRequest(Message(error)); var x = await db.SoilTypes.FindAsync([id], ct); if (x is null) return NotFound(); var code = string.IsNullOrWhiteSpace(r.Code) ? x.Code : Code(r.Code); if (await db.SoilTypes.AnyAsync(y => y.Id != id && y.Code == code, ct)) return Conflict(Message("El código de suelo ya existe.")); x.Code = code; Map(r, x); await Save("SOIL_UPDATED", code, ct); return NoContent(); }
    [HttpDelete("soil-types/{id:guid}"), Authorize(Policy = PermissionPolicies.AgronomyDelete)] public async Task<IActionResult> DeleteSoil(Guid id, CancellationToken ct) { var x = await db.SoilTypes.FindAsync([id], ct); if (x is null) return NotFound(); if (await db.FarmBlocks.AnyAsync(y => y.SoilTypeId == id, ct) || await db.CropWaterRequirements.AnyAsync(y => y.SoilTypeId == id, ct)) return Conflict(Message("El suelo tiene bloques o requisitos asociados.")); db.Remove(x); await Save("SOIL_DELETED", x.Code, ct); return NoContent(); }

    [HttpGet("crop-types")] public async Task<IActionResult> CropTypes(CancellationToken ct) => Ok(await db.CropTypes.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct));
    [HttpPost("crop-types"), Authorize(Policy = PermissionPolicies.AgronomyManage)] public async Task<IActionResult> CreateCropType(CropTypeRequest r, CancellationToken ct) { var code = await ResolveCodeAsync(r.Code, db.CropTypes.Select(x => x.Code), "TIPO", ct); if (await db.CropTypes.AnyAsync(x => x.Code == code, ct)) return Conflict(Message("El tipo de cultivo ya existe.")); var x = new CropType { Code = code, Name = r.Name.Trim(), IsActive = r.IsActive }; db.Add(x); await Save("CROP_TYPE_CREATED", code, ct); return Ok(x); }
    [HttpPut("crop-types/{id:guid}"), Authorize(Policy = PermissionPolicies.AgronomyManage)] public async Task<IActionResult> UpdateCropType(Guid id, CropTypeRequest r, CancellationToken ct) { var x = await db.CropTypes.FindAsync([id], ct); if (x is null) return NotFound(); var code = string.IsNullOrWhiteSpace(r.Code) ? x.Code : Code(r.Code); if (await db.CropTypes.AnyAsync(y => y.Id != id && y.Code == code, ct)) return Conflict(Message("El tipo de cultivo ya existe.")); x.Code = code; x.Name = r.Name.Trim(); x.IsActive = r.IsActive; await Save("CROP_TYPE_UPDATED", x.Code, ct); return NoContent(); }
    [HttpDelete("crop-types/{id:guid}"), Authorize(Policy = PermissionPolicies.AgronomyDelete)] public async Task<IActionResult> DeleteCropType(Guid id, CancellationToken ct) { var x = await db.CropTypes.FindAsync([id], ct); if (x is null) return NotFound(); if (await db.Crops.AnyAsync(y => y.CropTypeId == id, ct)) return Conflict(Message("El tipo tiene cultivos asociados.")); db.Remove(x); await Save("CROP_TYPE_DELETED", x.Code, ct); return NoContent(); }

    [HttpGet("crops")] public async Task<IActionResult> Crops(CancellationToken ct) => Ok(await db.Crops.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, x.Code, x.Name, x.ScientificName, x.Description, x.IsActive, x.CropTypeId, CropType = x.CropType.Name, Stages = x.Stages.OrderBy(s => s.Sequence).Select(s => new { s.Id, s.Name, s.Sequence, s.EstimatedDays, s.Description }) }).ToListAsync(ct));
    [HttpPost("crops"), Authorize(Policy = PermissionPolicies.AgronomyManage)] public async Task<IActionResult> CreateCrop(CropRequest r, CancellationToken ct) { var code = await ResolveCodeAsync(r.Code, db.Crops.Select(y => y.Code), "CULT", ct); var error = await ValidateCrop(r, null, code, ct); if (error is not null) return BadRequest(Message(error)); var x = Map(r, new Crop { CropTypeId = r.CropTypeId, Code = code, Name = r.Name.Trim() }, code); db.Add(x); await Save("CROP_CREATED", x.Code, ct); return Ok(new { x.Id, x.Code, x.Name, x.ScientificName, x.Description, x.IsActive, x.CropTypeId }); }
    [HttpPut("crops/{id:guid}"), Authorize(Policy = PermissionPolicies.AgronomyManage)] public async Task<IActionResult> UpdateCrop(Guid id, CropRequest r, CancellationToken ct) { var x = await db.Crops.FindAsync([id], ct); if (x is null) return NotFound(); var code = string.IsNullOrWhiteSpace(r.Code) ? x.Code : Code(r.Code); var error = await ValidateCrop(r, id, code, ct); if (error is not null) return BadRequest(Message(error)); Map(r, x, code); await Save("CROP_UPDATED", x.Code, ct); return NoContent(); }
    [HttpDelete("crops/{id:guid}"), Authorize(Policy = PermissionPolicies.AgronomyDelete)] public async Task<IActionResult> DeleteCrop(Guid id, CancellationToken ct) { var x = await db.Crops.Include(y => y.Stages).SingleOrDefaultAsync(y => y.Id == id, ct); if (x is null) return NotFound(); if (await db.CropCycles.AnyAsync(y => y.CropId == id, ct) || await db.CropWaterRequirements.AnyAsync(y => y.CropId == id, ct)) return Conflict(Message("El cultivo tiene ciclos o requisitos asociados.")); db.Remove(x); await Save("CROP_DELETED", x.Code, ct); return NoContent(); }

    [HttpPost("stages"), Authorize(Policy = PermissionPolicies.AgronomyManage)] public async Task<IActionResult> CreateStage(StageRequest r, CancellationToken ct) { var error = await ValidateStage(r, null, ct); if (error is not null) return BadRequest(Message(error)); var x = Map(r, new PhenologicalStage { CropId = r.CropId, Name = r.Name.Trim() }); db.Add(x); await Save("CROP_STAGE_CREATED", x.Name, ct); return Ok(x); }
    [HttpPut("stages/{id:guid}"), Authorize(Policy = PermissionPolicies.AgronomyManage)] public async Task<IActionResult> UpdateStage(Guid id, StageRequest r, CancellationToken ct) { var x = await db.PhenologicalStages.FindAsync([id], ct); if (x is null) return NotFound(); var error = await ValidateStage(r, id, ct); if (error is not null) return BadRequest(Message(error)); Map(r, x); await Save("CROP_STAGE_UPDATED", x.Name, ct); return NoContent(); }
    [HttpDelete("stages/{id:guid}"), Authorize(Policy = PermissionPolicies.AgronomyDelete)] public async Task<IActionResult> DeleteStage(Guid id, CancellationToken ct) { var x = await db.PhenologicalStages.FindAsync([id], ct); if (x is null) return NotFound(); if (await db.CropCycles.AnyAsync(y => y.CurrentStageId == id, ct) || await db.CropWaterRequirements.AnyAsync(y => y.PhenologicalStageId == id, ct)) return Conflict(Message("La etapa está en uso.")); db.Remove(x); await Save("CROP_STAGE_DELETED", x.Name, ct); return NoContent(); }

    [HttpGet("requirements")] public async Task<IActionResult> Requirements(CancellationToken ct) => Ok(await db.CropWaterRequirements.AsNoTracking().OrderBy(x => x.Crop.Name).Select(x => new { x.Id, x.CropId, Crop = x.Crop.Name, x.PhenologicalStageId, Stage = x.PhenologicalStage != null ? x.PhenologicalStage.Name : "General", x.SoilTypeId, Soil = x.SoilType != null ? x.SoilType.Name : "Cualquier suelo", x.MinimumMoisturePercent, x.TargetMoisturePercent, x.MaximumMoisturePercent, x.BaseVolumeLiters, x.FrequencyHours, x.BaseDurationMinutes, x.MinimumTemperatureCelsius, x.MaximumTemperatureCelsius, x.MinimumAmbientHumidityPercent, x.MaximumAmbientHumidityPercent, x.AllowedFrom, x.AllowedUntil, x.IsActive }).ToListAsync(ct));
    [HttpPost("requirements"), Authorize(Policy = PermissionPolicies.AgronomyManage)] public async Task<IActionResult> CreateRequirement(RequirementRequest r, CancellationToken ct) { var error = await ValidateRequirement(r, ct); if (error is not null) return BadRequest(Message(error)); var x = Map(r, new CropWaterRequirement { CropId = r.CropId }); db.Add(x); await Save("WATER_REQUIREMENT_CREATED", r.CropId.ToString(), ct); return Ok(new { x.Id, x.CropId, x.PhenologicalStageId, x.SoilTypeId, x.MinimumMoisturePercent, x.TargetMoisturePercent, x.MaximumMoisturePercent, x.BaseVolumeLiters, x.FrequencyHours, x.BaseDurationMinutes, x.MinimumTemperatureCelsius, x.MaximumTemperatureCelsius, x.MinimumAmbientHumidityPercent, x.MaximumAmbientHumidityPercent, x.AllowedFrom, x.AllowedUntil, x.IsActive }); }
    [HttpPut("requirements/{id:guid}"), Authorize(Policy = PermissionPolicies.AgronomyManage)] public async Task<IActionResult> UpdateRequirement(Guid id, RequirementRequest r, CancellationToken ct) { var x = await db.CropWaterRequirements.FindAsync([id], ct); if (x is null) return NotFound(); var error = await ValidateRequirement(r, ct); if (error is not null) return BadRequest(Message(error)); Map(r, x); await Save("WATER_REQUIREMENT_UPDATED", id.ToString(), ct); return NoContent(); }
    [HttpDelete("requirements/{id:guid}"), Authorize(Policy = PermissionPolicies.AgronomyDelete)] public async Task<IActionResult> DeleteRequirement(Guid id, CancellationToken ct) { var x = await db.CropWaterRequirements.FindAsync([id], ct); if (x is null) return NotFound(); if (await db.IrrigationRules.AnyAsync(y => y.CropWaterRequirementId == id, ct)) return Conflict(Message("El requisito está asociado a una regla de automatización.")); db.Remove(x); await Save("WATER_REQUIREMENT_DELETED", id.ToString(), ct); return NoContent(); }

    [HttpGet("environmental-evaluations")]
    public async Task<IActionResult> EnvironmentalEvaluations(CancellationToken ct)
    {
        var cycles = await CycleQuery().Where(x => x.Status == "Activo" || x.Status == "Planificado").ToListAsync(ct);
        var result = new List<EnvironmentalEvaluationResponse>();
        foreach (var cycle in cycles)
        {
            var requirement = await FindRequirement(cycle, ct);
            if (requirement is not null) result.Add(await EvaluateEnvironment(cycle, requirement, ct));
        }
        return Ok(result);
    }

    [HttpGet("recommendations")]
    public async Task<IActionResult> Recommendations(CancellationToken ct)
    {
        var cycles = await CycleQuery().Where(x => x.Status == "Activo" || x.Status == "Planificado").ToListAsync(ct);
        var result = new List<IrrigationRecommendationResponse>();
        foreach (var cycle in cycles)
        {
            var recommendation = await BuildRecommendation(cycle, ct);
            if (recommendation is not null) result.Add(recommendation);
        }
        return Ok(result);
    }

    [HttpPost("recommendations/{cycleId:guid}/approve"), Authorize(Policy = PermissionPolicies.IrrigationOperate)]
    public async Task<IActionResult> ApproveRecommendation(Guid cycleId, ApproveIrrigationRecommendationRequest request, CancellationToken ct)
    {
        if (totp is not null)
        {
            var verification = await totp.VerifyCriticalOperationAsync(HttpContext, ct);
            if (!verification.Allowed) return StatusCode(StatusCodes.Status403Forbidden, new { message = verification.Error });
        }
        var cycle = await CycleQuery(false).SingleOrDefaultAsync(x => x.Id == cycleId, ct);
        if (cycle is null || !cycle.IrrigationZone.IsActive) return NotFound(new { message = "El ciclo o su zona no están disponibles." });
        var recommendation = await BuildRecommendation(cycle, ct);
        if (recommendation is null) return Conflict(new { message = "No existe un requerimiento aplicable al cultivo, etapa y suelo." });
        if (!string.Equals(recommendation.Decision, "Regar", StringComparison.OrdinalIgnoreCase) || recommendation.SuggestedMinutes <= 0)
            return Conflict(new { message = $"La recomendación actual es '{recommendation.Decision}' y no puede aprobarse como riego." });
        if (await db.IrrigationRuns.AnyAsync(x => x.IrrigationZoneId == cycle.IrrigationZoneId && (x.Status == "En curso" || x.Status == "Esperando ACK" || x.Status == "Cierre pendiente"), ct))
            return Conflict(new { message = "La zona ya tiene un riego en curso." });
        var tanks = await db.WaterTanks.Where(x => x.Status != "Inactivo").ToListAsync(ct);
        var available = tanks.Sum(x => Math.Max(0, x.CurrentLevelLiters - x.CapacityLiters * x.MinimumSafePercent / 100));
        if (available < recommendation.SuggestedLiters) return Conflict(new { message = "Las reservas activas no tienen nivel seguro suficiente para esta recomendación." });
        var now = DateTime.UtcNow; var userId = CurrentUserId(); var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
        var flow = recommendation.SuggestedMinutes == 0 ? 0 : decimal.Round(recommendation.SuggestedLiters / recommendation.SuggestedMinutes, 2);
        var run = new IrrigationRun { IrrigationZoneId = cycle.IrrigationZoneId, Mode = "Asistido", Status = commands is null ? "En curso" : "Esperando ACK", PlannedDurationMinutes = recommendation.SuggestedMinutes, FlowRateLitersMinute = flow, RequestedAtUtc = now, StartedAtUtc = commands is null ? now : null, RequestedByUserId = userId, RequestedByEmail = email, Reason = recommendation.Explanation, Observations = Trim(request.Observations) };
        db.IrrigationRuns.Add(run);
        db.OperationalEvents.Add(new OperationalEvent { Category = "Riego asistido", EventType = commands is null ? "RECOMMENDATION_APPROVED_STARTED" : "RECOMMENDATION_APPROVED_REQUESTED", IrrigationZoneId = cycle.IrrigationZoneId, UserId = userId, UserEmail = email, Detail = $"{cycle.Name}: {recommendation.SuggestedMinutes} min, {recommendation.SuggestedLiters:0.##} L, suelo {recommendation.Soil} (factor {recommendation.SoilCorrectionFactor:0.####})." });
        await db.SaveChangesAsync(ct);
        if (commands is not null)
        {
            try { await commands.SendAsync(cycle.IrrigationZoneId, run, "ABRIR_VALVULA", userId, ct); }
            catch (Exception exception) { run.Status = "Fallido"; run.EndedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(ct); return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = exception.Message }); }
        }
        return Ok(new { run.Id, run.Mode, run.Status, recommendation.SuggestedMinutes, recommendation.SuggestedLiters, message = commands is null ? "Recomendación aprobada y riego asistido iniciado." : "Recomendación aprobada; orden enviada y pendiente de ACK MQTT." });
    }

    private IQueryable<CropCycle> CycleQuery(bool tracking = false)
    {
        var query = tracking ? db.CropCycles.AsQueryable() : db.CropCycles.AsNoTracking();
        return query.Include(x => x.Crop).Include(x => x.IrrigationZone).ThenInclude(x => x.IrrigationSector).ThenInclude(x => x.FarmBlock).ThenInclude(x => x.SoilType);
    }

    private async Task<CropWaterRequirement?> FindRequirement(CropCycle cycle, CancellationToken ct)
    {
        var soilId = cycle.IrrigationZone.IrrigationSector.FarmBlock.SoilTypeId;
        return await db.CropWaterRequirements.AsNoTracking()
            .Where(x => x.CropId == cycle.CropId && x.IsActive && (x.PhenologicalStageId == cycle.CurrentStageId || x.PhenologicalStageId == null) && (x.SoilTypeId == soilId || x.SoilTypeId == null))
            .OrderByDescending(x => x.PhenologicalStageId == cycle.CurrentStageId).ThenByDescending(x => x.SoilTypeId == soilId)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<IrrigationRecommendationResponse?> BuildRecommendation(CropCycle cycle, CancellationToken ct)
    {
        var requirement = await FindRequirement(cycle, ct); if (requirement is null) return null;
        var sensorId = cycle.IrrigationZone.PrimarySensorId;
        var moisture = sensorId is null ? null : await db.SensorReadings.AsNoTracking().Where(x => x.SensorId == sensorId && x.IsValid).OrderByDescending(x => x.CapturedAtUtc).Select(x => (decimal?)x.Value).FirstOrDefaultAsync(ct);
        var decision = calculator.Calculate(new(moisture, requirement.MinimumMoisturePercent, requirement.TargetMoisturePercent, requirement.MaximumMoisturePercent, requirement.BaseDurationMinutes, requirement.BaseVolumeLiters));
        var soil = cycle.IrrigationZone.IrrigationSector.FarmBlock.SoilType; var factor = soil?.IrrigationCorrectionFactor ?? 1m;
        var minutes = decision.Minutes <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling(decision.Minutes * factor));
        var liters = decision.Liters <= 0 ? 0 : decimal.Round(decision.Liters * factor, 2, MidpointRounding.AwayFromZero);
        var environment = await EvaluateEnvironment(cycle, requirement, ct); var blocked = !environment.IrrigationAllowed;
        var explanation = blocked ? "Riego bloqueado por validación ambiental: " + string.Join("; ", environment.Reasons) : $"{decision.Explanation} Ajuste por suelo {soil?.Name ?? "no asignado"}: factor {factor:0.####}.";
        return new(cycle.Id, cycle.Name, cycle.Crop.Name, cycle.IrrigationZone.Name, moisture, requirement.MinimumMoisturePercent, requirement.TargetMoisturePercent, blocked ? "ESPERAR" : decision.Decision, blocked ? 0 : minutes, blocked ? 0 : liters, explanation, soil?.Name ?? "Sin suelo asignado", factor);
    }

    private async Task<EnvironmentalEvaluationResponse> EvaluateEnvironment(CropCycle cycle, CropWaterRequirement requirement, CancellationToken ct)
    {
        async Task<decimal?> Latest(string sensorType) => await db.SensorReadings.AsNoTracking().Where(x => x.IrrigationZoneId == cycle.IrrigationZoneId && x.IsValid && x.Sensor.SensorType.Code == sensorType).OrderByDescending(x => x.CapturedAtUtc).Select(x => (decimal?)x.Value).FirstOrDefaultAsync(ct);
        var temperature = await Latest("AIR_TEMPERATURE");
        var humidity = await Latest("AIR_HUMIDITY");
        var now = TimeOnly.FromDateTime(DateTime.Now);
        var schedule = !requirement.AllowedFrom.HasValue || !requirement.AllowedUntil.HasValue || (requirement.AllowedFrom <= requirement.AllowedUntil ? now >= requirement.AllowedFrom && now <= requirement.AllowedUntil : now >= requirement.AllowedFrom || now <= requirement.AllowedUntil);
        var temperatureAllowed = (!requirement.MinimumTemperatureCelsius.HasValue || temperature >= requirement.MinimumTemperatureCelsius) && (!requirement.MaximumTemperatureCelsius.HasValue || temperature <= requirement.MaximumTemperatureCelsius) && (temperature.HasValue || (!requirement.MinimumTemperatureCelsius.HasValue && !requirement.MaximumTemperatureCelsius.HasValue));
        var humidityAllowed = (!requirement.MinimumAmbientHumidityPercent.HasValue || humidity >= requirement.MinimumAmbientHumidityPercent) && (!requirement.MaximumAmbientHumidityPercent.HasValue || humidity <= requirement.MaximumAmbientHumidityPercent) && (humidity.HasValue || (!requirement.MinimumAmbientHumidityPercent.HasValue && !requirement.MaximumAmbientHumidityPercent.HasValue));
        var reasons = new List<string>();
        if (!schedule) reasons.Add("fuera del horario permitido");
        if (!temperatureAllowed) reasons.Add(temperature.HasValue ? $"temperatura {temperature:0.##} °C fuera de rango" : "sin lectura de temperatura ambiente");
        if (!humidityAllowed) reasons.Add(humidity.HasValue ? $"humedad ambiental {humidity:0.##}% fuera de rango" : "sin lectura de humedad ambiental");
        if (reasons.Count == 0) reasons.Add("condiciones ambientales dentro de los límites");
        return new(cycle.Id, cycle.Name, cycle.IrrigationZone.Name, temperature, humidity, schedule, temperatureAllowed, humidityAllowed, schedule && temperatureAllowed && humidityAllowed, reasons);
    }

    private static string? ValidateSoil(SoilTypeRequest r) => r.FieldCapacityPercent <= 0 || r.SaturationPercent <= r.FieldCapacityPercent || r.SaturationPercent > 100 || r.InfiltrationMillimetersHour <= 0 || r.IrrigationCorrectionFactor is < 0.25m or > 4m ? "Debe cumplirse 0 < capacidad de campo < saturación ≤ 100 e infiltración positiva; el factor de riego debe estar entre 0.25 y 4." : null;
    private async Task<string?> ValidateCrop(CropRequest r, Guid? id, string code, CancellationToken ct) { if (!await db.CropTypes.AnyAsync(x => x.Id == r.CropTypeId && x.IsActive, ct)) return "Tipo de cultivo inválido."; return await db.Crops.AnyAsync(x => x.Id != id && x.Code == code, ct) ? "El código de cultivo ya existe." : null; }
    private async Task<string?> ValidateStage(StageRequest r, Guid? id, CancellationToken ct) { if (r.Sequence < 1 || r.EstimatedDays < 1) return "Secuencia y duración deben ser positivas."; if (!await db.Crops.AnyAsync(x => x.Id == r.CropId, ct)) return "Cultivo inválido."; return await db.PhenologicalStages.AnyAsync(x => x.Id != id && x.CropId == r.CropId && x.Sequence == r.Sequence, ct) ? "La secuencia ya existe para este cultivo." : null; }
    private async Task<string?> ValidateRequirement(RequirementRequest r, CancellationToken ct) { if (r.MinimumMoisturePercent < 0 || r.TargetMoisturePercent <= r.MinimumMoisturePercent || r.MaximumMoisturePercent <= r.TargetMoisturePercent || r.MaximumMoisturePercent > 100 || r.BaseDurationMinutes <= 0 || r.BaseVolumeLiters < 0 || r.FrequencyHours < 1 || r.MinimumAmbientHumidityPercent is < 0 || r.MaximumAmbientHumidityPercent is > 100 || (r.MinimumAmbientHumidityPercent.HasValue && r.MaximumAmbientHumidityPercent.HasValue && r.MinimumAmbientHumidityPercent >= r.MaximumAmbientHumidityPercent)) return "Los umbrales deben cumplir mínimo < objetivo < máximo ≤ 100, con volumen, frecuencia y duración válidos."; if (!await db.Crops.AnyAsync(x => x.Id == r.CropId, ct)) return "Cultivo inválido."; return null; }
    private static SoilType Map(SoilTypeRequest r, SoilType x) { x.Name = r.Name.Trim(); x.FieldCapacityPercent = r.FieldCapacityPercent; x.SaturationPercent = r.SaturationPercent; x.InfiltrationMillimetersHour = r.InfiltrationMillimetersHour; x.IrrigationCorrectionFactor = r.IrrigationCorrectionFactor; x.Description = Trim(r.Description); x.IsActive = r.IsActive; return x; }
    private static Crop Map(CropRequest r, Crop x, string code) { x.CropTypeId = r.CropTypeId; x.Code = code; x.Name = r.Name.Trim(); x.ScientificName = Trim(r.ScientificName); x.Description = Trim(r.Description); x.IsActive = r.IsActive; return x; }
    private static PhenologicalStage Map(StageRequest r, PhenologicalStage x) { x.CropId = r.CropId; x.Name = r.Name.Trim(); x.Sequence = r.Sequence; x.EstimatedDays = r.EstimatedDays; x.Description = Trim(r.Description); return x; }
    private static CropWaterRequirement Map(RequirementRequest r, CropWaterRequirement x) { x.CropId = r.CropId; x.PhenologicalStageId = r.PhenologicalStageId; x.SoilTypeId = r.SoilTypeId; x.MinimumMoisturePercent = r.MinimumMoisturePercent; x.TargetMoisturePercent = r.TargetMoisturePercent; x.MaximumMoisturePercent = r.MaximumMoisturePercent; x.BaseVolumeLiters = r.BaseVolumeLiters; x.FrequencyHours = r.FrequencyHours; x.BaseDurationMinutes = r.BaseDurationMinutes; x.MinimumTemperatureCelsius = r.MinimumTemperatureCelsius; x.MaximumTemperatureCelsius = r.MaximumTemperatureCelsius; x.MinimumAmbientHumidityPercent = r.MinimumAmbientHumidityPercent; x.MaximumAmbientHumidityPercent = r.MaximumAmbientHumidityPercent; x.AllowedFrom = r.AllowedFrom; x.AllowedUntil = r.AllowedUntil; x.IsActive = r.IsActive; return x; }
    private async Task Save(string type, string detail, CancellationToken ct) { db.AccessAudits.Add(new AccessAudit { EventType = type, Detail = detail }); await db.SaveChangesAsync(ct); }
    private Guid? CurrentUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private static object Message(string value) => new { message = value };
    // Los codigos se asignan solos cuando el formulario no envia uno. Escribirlos a
    // mano produjo duplicados y valores numericos sueltos: la limpieza del 29 de
    // agosto elimino cultivos con codigo "2", "16" y "19".
    private static async Task<string> ResolveCodeAsync(string? requested, IQueryable<string> existing, string prefix, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(requested)) return Code(requested);
        var codes = await existing.ToListAsync(ct);
        return NextCode(codes, prefix);
    }

    internal static string NextCode(IEnumerable<string> existing, string prefix)
    {
        var highest = existing
            .Where(x => x is not null && x.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase))
            .Select(x => int.TryParse(x[(prefix.Length + 1)..], out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{prefix}-{highest + 1:0000}";
    }

    private static string Code(string value) => value.Trim().ToUpperInvariant().Replace(' ', '_');
    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}


