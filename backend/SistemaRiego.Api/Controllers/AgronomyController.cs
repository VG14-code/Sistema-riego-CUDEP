using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/agronomy"), Authorize(Policy = Policies.Operator)]
public sealed class AgronomyController(AppDbContext db, IIrrigationRecommendationCalculator? recommendationCalculator = null) : ControllerBase
{
    private readonly IIrrigationRecommendationCalculator calculator = recommendationCalculator ?? new IrrigationRecommendationCalculator();

    [HttpGet("soil-types")] public async Task<IActionResult> SoilTypes(CancellationToken ct) => Ok(await db.SoilTypes.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct));
    [HttpPost("soil-types"), Authorize(Policy = Policies.Technician)] public async Task<IActionResult> CreateSoil(SoilTypeRequest r, CancellationToken ct) { var error = ValidateSoil(r); if (error is not null) return BadRequest(Message(error)); var code = Code(r.Code); if (await db.SoilTypes.AnyAsync(x => x.Code == code, ct)) return Conflict(Message("El código de suelo ya existe.")); var x = Map(r, new SoilType { Code = code, Name = r.Name.Trim() }); db.Add(x); await Save("SOIL_CREATED", code, ct); return Ok(x); }
    [HttpPut("soil-types/{id:guid}"), Authorize(Policy = Policies.Technician)] public async Task<IActionResult> UpdateSoil(Guid id, SoilTypeRequest r, CancellationToken ct) { var error = ValidateSoil(r); if (error is not null) return BadRequest(Message(error)); var x = await db.SoilTypes.FindAsync([id], ct); if (x is null) return NotFound(); var code = Code(r.Code); if (await db.SoilTypes.AnyAsync(y => y.Id != id && y.Code == code, ct)) return Conflict(Message("El código de suelo ya existe.")); x.Code = code; Map(r, x); await Save("SOIL_UPDATED", code, ct); return NoContent(); }
    [HttpDelete("soil-types/{id:guid}"), Authorize(Policy = Policies.Administrator)] public async Task<IActionResult> DeleteSoil(Guid id, CancellationToken ct) { var x = await db.SoilTypes.FindAsync([id], ct); if (x is null) return NotFound(); if (await db.FarmBlocks.AnyAsync(y => y.SoilTypeId == id, ct) || await db.CropWaterRequirements.AnyAsync(y => y.SoilTypeId == id, ct)) return Conflict(Message("El suelo tiene bloques o requisitos asociados.")); db.Remove(x); await Save("SOIL_DELETED", x.Code, ct); return NoContent(); }

    [HttpGet("crop-types")] public async Task<IActionResult> CropTypes(CancellationToken ct) => Ok(await db.CropTypes.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct));
    [HttpPost("crop-types"), Authorize(Policy = Policies.Technician)] public async Task<IActionResult> CreateCropType(CropTypeRequest r, CancellationToken ct) { var code = Code(r.Code); if (await db.CropTypes.AnyAsync(x => x.Code == code, ct)) return Conflict(Message("El tipo de cultivo ya existe.")); var x = new CropType { Code = code, Name = r.Name.Trim(), IsActive = r.IsActive }; db.Add(x); await Save("CROP_TYPE_CREATED", code, ct); return Ok(x); }
    [HttpPut("crop-types/{id:guid}"), Authorize(Policy = Policies.Technician)] public async Task<IActionResult> UpdateCropType(Guid id, CropTypeRequest r, CancellationToken ct) { var x = await db.CropTypes.FindAsync([id], ct); if (x is null) return NotFound(); var code = Code(r.Code); if (await db.CropTypes.AnyAsync(y => y.Id != id && y.Code == code, ct)) return Conflict(Message("El tipo de cultivo ya existe.")); x.Code = code; x.Name = r.Name.Trim(); x.IsActive = r.IsActive; await Save("CROP_TYPE_UPDATED", x.Code, ct); return NoContent(); }
    [HttpDelete("crop-types/{id:guid}"), Authorize(Policy = Policies.Administrator)] public async Task<IActionResult> DeleteCropType(Guid id, CancellationToken ct) { var x = await db.CropTypes.FindAsync([id], ct); if (x is null) return NotFound(); if (await db.Crops.AnyAsync(y => y.CropTypeId == id, ct)) return Conflict(Message("El tipo tiene cultivos asociados.")); db.Remove(x); await Save("CROP_TYPE_DELETED", x.Code, ct); return NoContent(); }

    [HttpGet("crops")] public async Task<IActionResult> Crops(CancellationToken ct) => Ok(await db.Crops.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, x.Code, x.Name, x.ScientificName, x.Description, x.IsActive, x.CropTypeId, CropType = x.CropType.Name, Stages = x.Stages.OrderBy(s => s.Sequence).Select(s => new { s.Id, s.Name, s.Sequence, s.EstimatedDays, s.Description }) }).ToListAsync(ct));
    [HttpPost("crops"), Authorize(Policy = Policies.Technician)] public async Task<IActionResult> CreateCrop(CropRequest r, CancellationToken ct) { var error = await ValidateCrop(r, null, ct); if (error is not null) return BadRequest(Message(error)); var x = Map(r, new Crop { CropTypeId = r.CropTypeId, Code = Code(r.Code), Name = r.Name.Trim() }); db.Add(x); await Save("CROP_CREATED", x.Code, ct); return Ok(x); }
    [HttpPut("crops/{id:guid}"), Authorize(Policy = Policies.Technician)] public async Task<IActionResult> UpdateCrop(Guid id, CropRequest r, CancellationToken ct) { var x = await db.Crops.FindAsync([id], ct); if (x is null) return NotFound(); var error = await ValidateCrop(r, id, ct); if (error is not null) return BadRequest(Message(error)); Map(r, x); await Save("CROP_UPDATED", x.Code, ct); return NoContent(); }
    [HttpDelete("crops/{id:guid}"), Authorize(Policy = Policies.Administrator)] public async Task<IActionResult> DeleteCrop(Guid id, CancellationToken ct) { var x = await db.Crops.Include(y => y.Stages).SingleOrDefaultAsync(y => y.Id == id, ct); if (x is null) return NotFound(); if (await db.CropCycles.AnyAsync(y => y.CropId == id, ct) || await db.CropWaterRequirements.AnyAsync(y => y.CropId == id, ct)) return Conflict(Message("El cultivo tiene ciclos o requisitos asociados.")); db.Remove(x); await Save("CROP_DELETED", x.Code, ct); return NoContent(); }

    [HttpPost("stages"), Authorize(Policy = Policies.Technician)] public async Task<IActionResult> CreateStage(StageRequest r, CancellationToken ct) { var error = await ValidateStage(r, null, ct); if (error is not null) return BadRequest(Message(error)); var x = Map(r, new PhenologicalStage { CropId = r.CropId, Name = r.Name.Trim() }); db.Add(x); await Save("CROP_STAGE_CREATED", x.Name, ct); return Ok(x); }
    [HttpPut("stages/{id:guid}"), Authorize(Policy = Policies.Technician)] public async Task<IActionResult> UpdateStage(Guid id, StageRequest r, CancellationToken ct) { var x = await db.PhenologicalStages.FindAsync([id], ct); if (x is null) return NotFound(); var error = await ValidateStage(r, id, ct); if (error is not null) return BadRequest(Message(error)); Map(r, x); await Save("CROP_STAGE_UPDATED", x.Name, ct); return NoContent(); }
    [HttpDelete("stages/{id:guid}"), Authorize(Policy = Policies.Administrator)] public async Task<IActionResult> DeleteStage(Guid id, CancellationToken ct) { var x = await db.PhenologicalStages.FindAsync([id], ct); if (x is null) return NotFound(); if (await db.CropCycles.AnyAsync(y => y.CurrentStageId == id, ct) || await db.CropWaterRequirements.AnyAsync(y => y.PhenologicalStageId == id, ct)) return Conflict(Message("La etapa está en uso.")); db.Remove(x); await Save("CROP_STAGE_DELETED", x.Name, ct); return NoContent(); }

    [HttpGet("requirements")] public async Task<IActionResult> Requirements(CancellationToken ct) => Ok(await db.CropWaterRequirements.AsNoTracking().OrderBy(x => x.Crop.Name).Select(x => new { x.Id, x.CropId, Crop = x.Crop.Name, x.PhenologicalStageId, Stage = x.PhenologicalStage != null ? x.PhenologicalStage.Name : "General", x.SoilTypeId, Soil = x.SoilType != null ? x.SoilType.Name : "Cualquier suelo", x.MinimumMoisturePercent, x.TargetMoisturePercent, x.MaximumMoisturePercent, x.BaseVolumeLiters, x.FrequencyHours, x.BaseDurationMinutes, x.MinimumTemperatureCelsius, x.MaximumTemperatureCelsius, x.MinimumAmbientHumidityPercent, x.MaximumAmbientHumidityPercent, x.AllowedFrom, x.AllowedUntil, x.IsActive }).ToListAsync(ct));
    [HttpPost("requirements"), Authorize(Policy = Policies.Technician)] public async Task<IActionResult> CreateRequirement(RequirementRequest r, CancellationToken ct) { var error = await ValidateRequirement(r, ct); if (error is not null) return BadRequest(Message(error)); var x = Map(r, new CropWaterRequirement { CropId = r.CropId }); db.Add(x); await Save("WATER_REQUIREMENT_CREATED", r.CropId.ToString(), ct); return Ok(x); }
    [HttpPut("requirements/{id:guid}"), Authorize(Policy = Policies.Technician)] public async Task<IActionResult> UpdateRequirement(Guid id, RequirementRequest r, CancellationToken ct) { var x = await db.CropWaterRequirements.FindAsync([id], ct); if (x is null) return NotFound(); var error = await ValidateRequirement(r, ct); if (error is not null) return BadRequest(Message(error)); Map(r, x); await Save("WATER_REQUIREMENT_UPDATED", id.ToString(), ct); return NoContent(); }
    [HttpDelete("requirements/{id:guid}"), Authorize(Policy = Policies.Administrator)] public async Task<IActionResult> DeleteRequirement(Guid id, CancellationToken ct) { var x = await db.CropWaterRequirements.FindAsync([id], ct); if (x is null) return NotFound(); if (await db.IrrigationRules.AnyAsync(y => y.CropWaterRequirementId == id, ct)) return Conflict(Message("El requisito está asociado a una regla de automatización.")); db.Remove(x); await Save("WATER_REQUIREMENT_DELETED", id.ToString(), ct); return NoContent(); }

    [HttpGet("environmental-evaluations")]
    public async Task<IActionResult> EnvironmentalEvaluations(CancellationToken ct)
    {
        var cycles = await db.CropCycles.AsNoTracking().Include(x => x.IrrigationZone).Where(x => x.Status == "Activo" || x.Status == "Planificado").ToListAsync(ct);
        var result = new List<EnvironmentalEvaluationResponse>();
        foreach (var cycle in cycles)
        {
            var requirement = await db.CropWaterRequirements.AsNoTracking().Where(x => x.CropId == cycle.CropId && x.IsActive && (x.PhenologicalStageId == cycle.CurrentStageId || x.PhenologicalStageId == null)).OrderByDescending(x => x.PhenologicalStageId != null).FirstOrDefaultAsync(ct);
            if (requirement is not null) result.Add(await EvaluateEnvironment(cycle, requirement, ct));
        }
        return Ok(result);
    }

    [HttpGet("recommendations")]
    public async Task<IActionResult> Recommendations(CancellationToken ct)
    {
        var cycles = await db.CropCycles.AsNoTracking().Include(x => x.Crop).Include(x => x.IrrigationZone).Where(x => x.Status == "Activo" || x.Status == "Planificado").ToListAsync(ct);
        var result = new List<IrrigationRecommendationResponse>();
        foreach (var cycle in cycles)
        {
            var requirement = await db.CropWaterRequirements.AsNoTracking().Where(x => x.CropId == cycle.CropId && x.IsActive && (x.PhenologicalStageId == cycle.CurrentStageId || x.PhenologicalStageId == null)).OrderByDescending(x => x.PhenologicalStageId != null).FirstOrDefaultAsync(ct);
            if (requirement is null) continue;
            var sensorId = cycle.IrrigationZone.PrimarySensorId;
            var moisture = sensorId is null ? null : await db.SensorReadings.AsNoTracking().Where(x => x.SensorId == sensorId && x.IsValid).OrderByDescending(x => x.CapturedAtUtc).Select(x => (decimal?)x.Value).FirstOrDefaultAsync(ct);
            var decision = calculator.Calculate(new(moisture, requirement.MinimumMoisturePercent, requirement.TargetMoisturePercent, requirement.MaximumMoisturePercent, requirement.BaseDurationMinutes, requirement.BaseVolumeLiters));
            var environment = await EvaluateEnvironment(cycle, requirement, ct);
            var blocked = !environment.IrrigationAllowed;
            result.Add(new(cycle.Id, cycle.Name, cycle.Crop.Name, cycle.IrrigationZone.Name, moisture, requirement.MinimumMoisturePercent, requirement.TargetMoisturePercent, blocked ? "ESPERAR" : decision.Decision, blocked ? 0 : decision.Minutes, blocked ? 0 : decision.Liters, blocked ? "Riego bloqueado por validación ambiental: " + string.Join("; ", environment.Reasons) : decision.Explanation));
        }
        return Ok(result);
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

    private static string? ValidateSoil(SoilTypeRequest r) => r.FieldCapacityPercent <= 0 || r.SaturationPercent <= r.FieldCapacityPercent || r.SaturationPercent > 100 || r.InfiltrationMillimetersHour <= 0 ? "Debe cumplirse 0 < capacidad de campo < saturación ≤ 100 e infiltración positiva." : null;
    private async Task<string?> ValidateCrop(CropRequest r, Guid? id, CancellationToken ct) { if (!await db.CropTypes.AnyAsync(x => x.Id == r.CropTypeId && x.IsActive, ct)) return "Tipo de cultivo inválido."; var code = Code(r.Code); return await db.Crops.AnyAsync(x => x.Id != id && x.Code == code, ct) ? "El código de cultivo ya existe." : null; }
    private async Task<string?> ValidateStage(StageRequest r, Guid? id, CancellationToken ct) { if (r.Sequence < 1 || r.EstimatedDays < 1) return "Secuencia y duración deben ser positivas."; if (!await db.Crops.AnyAsync(x => x.Id == r.CropId, ct)) return "Cultivo inválido."; return await db.PhenologicalStages.AnyAsync(x => x.Id != id && x.CropId == r.CropId && x.Sequence == r.Sequence, ct) ? "La secuencia ya existe para este cultivo." : null; }
    private async Task<string?> ValidateRequirement(RequirementRequest r, CancellationToken ct) { if (r.MinimumMoisturePercent < 0 || r.TargetMoisturePercent <= r.MinimumMoisturePercent || r.MaximumMoisturePercent <= r.TargetMoisturePercent || r.MaximumMoisturePercent > 100 || r.BaseDurationMinutes <= 0 || r.BaseVolumeLiters < 0 || r.FrequencyHours < 1 || r.MinimumAmbientHumidityPercent is < 0 || r.MaximumAmbientHumidityPercent is > 100 || (r.MinimumAmbientHumidityPercent.HasValue && r.MaximumAmbientHumidityPercent.HasValue && r.MinimumAmbientHumidityPercent >= r.MaximumAmbientHumidityPercent)) return "Los umbrales deben cumplir mínimo < objetivo < máximo ≤ 100, con volumen, frecuencia y duración válidos."; if (!await db.Crops.AnyAsync(x => x.Id == r.CropId, ct)) return "Cultivo inválido."; return null; }
    private static SoilType Map(SoilTypeRequest r, SoilType x) { x.Name = r.Name.Trim(); x.FieldCapacityPercent = r.FieldCapacityPercent; x.SaturationPercent = r.SaturationPercent; x.InfiltrationMillimetersHour = r.InfiltrationMillimetersHour; x.Description = Trim(r.Description); x.IsActive = r.IsActive; return x; }
    private static Crop Map(CropRequest r, Crop x) { x.CropTypeId = r.CropTypeId; x.Code = Code(r.Code); x.Name = r.Name.Trim(); x.ScientificName = Trim(r.ScientificName); x.Description = Trim(r.Description); x.IsActive = r.IsActive; return x; }
    private static PhenologicalStage Map(StageRequest r, PhenologicalStage x) { x.CropId = r.CropId; x.Name = r.Name.Trim(); x.Sequence = r.Sequence; x.EstimatedDays = r.EstimatedDays; x.Description = Trim(r.Description); return x; }
    private static CropWaterRequirement Map(RequirementRequest r, CropWaterRequirement x) { x.CropId = r.CropId; x.PhenologicalStageId = r.PhenologicalStageId; x.SoilTypeId = r.SoilTypeId; x.MinimumMoisturePercent = r.MinimumMoisturePercent; x.TargetMoisturePercent = r.TargetMoisturePercent; x.MaximumMoisturePercent = r.MaximumMoisturePercent; x.BaseVolumeLiters = r.BaseVolumeLiters; x.FrequencyHours = r.FrequencyHours; x.BaseDurationMinutes = r.BaseDurationMinutes; x.MinimumTemperatureCelsius = r.MinimumTemperatureCelsius; x.MaximumTemperatureCelsius = r.MaximumTemperatureCelsius; x.MinimumAmbientHumidityPercent = r.MinimumAmbientHumidityPercent; x.MaximumAmbientHumidityPercent = r.MaximumAmbientHumidityPercent; x.AllowedFrom = r.AllowedFrom; x.AllowedUntil = r.AllowedUntil; x.IsActive = r.IsActive; return x; }
    private async Task Save(string type, string detail, CancellationToken ct) { db.AccessAudits.Add(new AccessAudit { EventType = type, Detail = detail }); await db.SaveChangesAsync(ct); }
    private static object Message(string value) => new { message = value };
    private static string Code(string value) => value.Trim().ToUpperInvariant().Replace(' ', '_');
    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}


