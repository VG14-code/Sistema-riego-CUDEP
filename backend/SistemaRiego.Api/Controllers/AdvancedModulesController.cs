using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/advanced"), Authorize]
public sealed class AdvancedModulesController(AppDbContext db, IHttpClientFactory httpClients) : ControllerBase
{
    [HttpGet("catalogs")]
    public async Task<ActionResult> Catalogs(CancellationToken ct) => Ok(new
    {
        zones = await db.IrrigationZones.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, x.Code, x.Name, x.IsActive, OperationalStatus = x.OperationalStatus.Name }).ToListAsync(ct),
        tanks = await db.WaterTanks.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Status }).ToListAsync(ct),
        pumps = await db.WaterPumps.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.WaterTankId, x.Status }).ToListAsync(ct),
        sources = await db.WaterSources.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct),
        arrays = await db.SolarPanelArrays.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct),
        batteries = await db.SolarBatteries.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct)
    });

    [HttpGet("hydraulics")]
    public async Task<ActionResult> Hydraulics(CancellationToken ct) => Ok(await db.HydraulicConfigurations.AsNoTracking().Include(x => x.IrrigationZone).Include(x => x.WaterSource).Include(x => x.WaterTank).Include(x => x.WaterPump).OrderBy(x => x.IrrigationZone.Name).ToListAsync(ct));

    [HttpPost("hydraulics"), Authorize(Policy = PermissionPolicies.TerritoryManage)]
    public async Task<ActionResult> SaveHydraulics(HydraulicRequest r, CancellationToken ct)
    {
        if (r.DesignFlowLitersMinute <= 0 || r.PipeDiameterMillimeters <= 0 || r.MaximumPressureBar <= r.MinimumPressureBar) return BadRequest(new { message = "Revisa tubería, caudal y rango de presión." });
        var item = await db.HydraulicConfigurations.SingleOrDefaultAsync(x => x.IrrigationZoneId == r.IrrigationZoneId, ct) ?? new HydraulicConfiguration { IrrigationZoneId = r.IrrigationZoneId };
        if (item.Id == Guid.Empty || db.Entry(item).State == EntityState.Detached) db.Add(item);
        item.WaterSourceId = r.WaterSourceId; item.WaterTankId = r.WaterTankId; item.WaterPumpId = r.WaterPumpId; item.PipeDiameterMillimeters = r.PipeDiameterMillimeters; item.PipeLengthMeters = r.PipeLengthMeters; item.DesignFlowLitersMinute = r.DesignFlowLitersMinute; item.MinimumPressureBar = r.MinimumPressureBar; item.MaximumPressureBar = r.MaximumPressureBar; item.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); return Ok(item);
    }

    [HttpPatch("zones/{id:guid}/state"), Authorize(Policy = PermissionPolicies.TerritoryManage)]
    public async Task<ActionResult> ZoneState(Guid id, ZoneStateRequest r, CancellationToken ct)
    {
        var zone = await db.IrrigationZones.FindAsync([id], ct); if (zone is null) return NotFound();
        var status = await db.MasterCatalogItems.SingleOrDefaultAsync(x => x.Kind == CatalogKind.OperationalStatus && x.Code == r.StatusCode, ct); if (status is null) return BadRequest(new { message = "Estado operativo no válido." });
        zone.OperationalStatusId = status.Id; zone.IsActive = r.IsActive; await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpPost("rules/{id:guid}/simulate")]
    public async Task<ActionResult> Simulate(Guid id, CancellationToken ct)
    {
        var rule = await db.IrrigationRules.Include(x => x.IrrigationZone).SingleOrDefaultAsync(x => x.Id == id, ct); if (rule is null) return NotFound();
        var reading = await db.SensorReadings.AsNoTracking().Where(x => x.IrrigationZoneId == rule.IrrigationZoneId && x.IsValid).OrderByDescending(x => x.CapturedAtUtc).FirstOrDefaultAsync(ct);
        var active = await db.IrrigationRuns.AnyAsync(x => x.IrrigationZoneId == rule.IrrigationZoneId && (x.Status == "En curso" || x.Status == "Esperando ACK" || x.Status == "Cierre pendiente"), ct);
        var now = DateTime.UtcNow; var local = now.ToLocalTime(); var time = TimeOnly.FromDateTime(local); var day = ((int)local.DayOfWeek + 6) % 7 + 1;
        var inside = rule.AllowedFrom <= rule.AllowedUntil ? time >= rule.AllowedFrom && time <= rule.AllowedUntil : time >= rule.AllowedFrom || time <= rule.AllowedUntil;
        var shouldStart = rule.IsEnabled && !active && (!rule.SuspendedUntilUtc.HasValue || rule.SuspendedUntilUtc <= now) && rule.AllowedDays.Split(',').Contains(day.ToString()) && inside && reading is not null && reading.Value < rule.MinimumMoisturePercent;
        var decision = shouldStart ? "Regaría" : active ? "Riego activo" : reading is null ? "Sin lectura válida" : !inside ? "Fuera de ventana" : reading.Value >= rule.MinimumMoisturePercent ? "No regaría" : "Bloqueada";
        var reason = reading is null ? "No existe una lectura válida para la zona." : $"Humedad {reading.Value:0.0}% frente al mínimo {rule.MinimumMoisturePercent:0.0}%.";
        var evaluation = new IrrigationRuleEvaluation { IrrigationRuleId = rule.Id, IsSimulation = true, MoisturePercent = reading?.Value, Decision = decision, Reason = reason, StartedIrrigation = false }; db.Add(evaluation); await db.SaveChangesAsync(ct);
        return Ok(new { evaluation.Id, evaluation.EvaluatedAtUtc, decision, reason, wouldStart = shouldStart });
    }

    [HttpGet("rules/evaluations")]
    public async Task<ActionResult> Evaluations(int take = 200, CancellationToken ct = default) => Ok(await db.IrrigationRuleEvaluations.AsNoTracking().Include(x => x.IrrigationRule).OrderByDescending(x => x.EvaluatedAtUtc).Take(Math.Clamp(take, 1, 1000)).Select(x => new { x.Id, x.IrrigationRuleId, Rule = x.IrrigationRule.Name, x.EvaluatedAtUtc, x.IsSimulation, x.MoisturePercent, x.Decision, x.Reason, x.StartedIrrigation }).ToListAsync(ct));

    [HttpGet("rules/{id:guid}/versions")]
    public async Task<ActionResult> Versions(Guid id, CancellationToken ct) => Ok(await db.IrrigationRuleVersions.AsNoTracking().Where(x => x.IrrigationRuleId == id).OrderByDescending(x => x.Version).ToListAsync(ct));

    [HttpGet("schedules")]
    public async Task<ActionResult> Schedules(CancellationToken ct) => Ok(await db.IrrigationSchedules.AsNoTracking().Include(x => x.IrrigationZone).OrderBy(x => x.NextRunAtUtc).ToListAsync(ct));
    [HttpPost("schedules"), Authorize(Policy = PermissionPolicies.IrrigationOperate)]
    public async Task<ActionResult> CreateSchedule(ScheduleRequest r, CancellationToken ct) { var error = ValidateSchedule(r); if (error is not null) return BadRequest(new { message = error }); var x = Apply(new IrrigationSchedule { Name = r.Name, IrrigationZoneId = r.IrrigationZoneId, CreatedByEmail = User.FindFirstValue(ClaimTypes.Email) }, r); db.Add(x); await db.SaveChangesAsync(ct); return Ok(x); }
    [HttpPut("schedules/{id:guid}"), Authorize(Policy = PermissionPolicies.IrrigationOperate)]
    public async Task<ActionResult> UpdateSchedule(Guid id, ScheduleRequest r, CancellationToken ct) { var x = await db.IrrigationSchedules.FindAsync([id], ct); if (x is null) return NotFound(); var error = ValidateSchedule(r); if (error is not null) return BadRequest(new { message = error }); Apply(x, r); await db.SaveChangesAsync(ct); return Ok(x); }
    [HttpDelete("schedules/{id:guid}"), Authorize(Policy = PermissionPolicies.IrrigationOperate)] public async Task<ActionResult> DeleteSchedule(Guid id, CancellationToken ct) { var x = await db.IrrigationSchedules.FindAsync([id], ct); if (x is null) return NotFound(); db.Remove(x); await db.SaveChangesAsync(ct); return NoContent(); }

    [HttpGet("sources")] public async Task<ActionResult> Sources(CancellationToken ct) => Ok(await db.WaterSources.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct));
    [HttpPost("sources"), Authorize(Policy = PermissionPolicies.WaterSupplyManage)] public async Task<ActionResult> CreateSource(WaterSourceRequest r, CancellationToken ct) { if (await db.WaterSources.AnyAsync(x => x.Code == r.Code, ct)) return Conflict(new { message = "El código de la fuente ya existe." }); var x = Apply(new WaterSource { Code = r.Code, Name = r.Name }, r); db.Add(x); await db.SaveChangesAsync(ct); return Ok(x); }
    [HttpPut("sources/{id:guid}"), Authorize(Policy = PermissionPolicies.WaterSupplyManage)] public async Task<ActionResult> UpdateSource(Guid id, WaterSourceRequest r, CancellationToken ct) { var x = await db.WaterSources.FindAsync([id], ct); if (x is null) return NotFound(); Apply(x, r); await db.SaveChangesAsync(ct); return Ok(x); }
    [HttpDelete("sources/{id:guid}"), Authorize(Policy = PermissionPolicies.WaterSupplyManage)] public async Task<ActionResult> DeleteSource(Guid id, CancellationToken ct) { var x = await db.WaterSources.FindAsync([id], ct); if (x is null) return NotFound(); db.Remove(x); await db.SaveChangesAsync(ct); return NoContent(); }
    [HttpGet("auto-fill")] public async Task<ActionResult> AutoFill(CancellationToken ct) => Ok(await db.AutomaticFillConfigurations.AsNoTracking().Include(x => x.WaterTank).Include(x => x.WaterPump).Include(x => x.WaterSource).ToListAsync(ct));
    [HttpPost("auto-fill"), Authorize(Policy = PermissionPolicies.WaterSupplyManage)] public async Task<ActionResult> SaveAutoFill(AutoFillRequest r, CancellationToken ct) { if (r.StartAtPercent < 0 || r.StopAtPercent > 100 || r.StartAtPercent >= r.StopAtPercent) return BadRequest(new { message = "Los niveles de arranque y parada no son válidos." }); var x = await db.AutomaticFillConfigurations.SingleOrDefaultAsync(y => y.WaterTankId == r.WaterTankId, ct) ?? new AutomaticFillConfiguration { WaterTankId = r.WaterTankId }; if (db.Entry(x).State == EntityState.Detached) db.Add(x); x.WaterPumpId = r.WaterPumpId; x.WaterSourceId = r.WaterSourceId; x.StartAtPercent = r.StartAtPercent; x.StopAtPercent = r.StopAtPercent; x.IsEnabled = r.IsEnabled; await EvaluateFill(x, ct); await db.SaveChangesAsync(ct); return Ok(x); }

    [HttpGet("energy/assets")] public async Task<ActionResult> EnergyAssets(CancellationToken ct) => Ok(new { arrays = await db.SolarPanelArrays.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct), batteries = await db.SolarBatteries.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct), controllers = await db.ChargeControllers.AsNoTracking().Include(x => x.SolarPanelArray).Include(x => x.SolarBattery).OrderBy(x => x.Name).ToListAsync(ct) });
    [HttpPost("energy/arrays"), Authorize(Policy = PermissionPolicies.EnergyManage)] public async Task<ActionResult> Array(SolarArrayRequest r, CancellationToken ct) { var x = new SolarPanelArray { Name = r.Name, RatedPowerWatts = r.RatedPowerWatts, PanelCount = r.PanelCount, IsActive = r.IsActive }; db.Add(x); await db.SaveChangesAsync(ct); return Ok(x); }
    [HttpPut("energy/arrays/{id:guid}"), Authorize(Policy = PermissionPolicies.EnergyManage)] public async Task<ActionResult> Array(Guid id, SolarArrayRequest r, CancellationToken ct) { var x = await db.SolarPanelArrays.FindAsync([id], ct); if (x is null) return NotFound(); x.Name = r.Name; x.RatedPowerWatts = r.RatedPowerWatts; x.PanelCount = r.PanelCount; x.IsActive = r.IsActive; await db.SaveChangesAsync(ct); return Ok(x); }
    [HttpDelete("energy/arrays/{id:guid}"), Authorize(Policy = PermissionPolicies.EnergyManage)] public async Task<ActionResult> Array(Guid id, CancellationToken ct) { var x = await db.SolarPanelArrays.FindAsync([id], ct); if (x is null) return NotFound(); db.Remove(x); await db.SaveChangesAsync(ct); return NoContent(); }
    [HttpPost("energy/batteries"), Authorize(Policy = PermissionPolicies.EnergyManage)] public async Task<ActionResult> Battery(BatteryRequest r, CancellationToken ct) { var x = new SolarBattery { Name = r.Name, CapacityWattHours = r.CapacityWattHours, NominalVoltage = r.NominalVoltage, MinimumSafeChargePercent = r.MinimumSafeChargePercent, CurrentChargePercent = r.CurrentChargePercent, Status = r.Status }; db.Add(x); await db.SaveChangesAsync(ct); return Ok(x); }
    [HttpPut("energy/batteries/{id:guid}"), Authorize(Policy = PermissionPolicies.EnergyManage)] public async Task<ActionResult> Battery(Guid id, BatteryRequest r, CancellationToken ct) { var x = await db.SolarBatteries.FindAsync([id], ct); if (x is null) return NotFound(); x.Name = r.Name; x.CapacityWattHours = r.CapacityWattHours; x.NominalVoltage = r.NominalVoltage; x.MinimumSafeChargePercent = r.MinimumSafeChargePercent; x.CurrentChargePercent = r.CurrentChargePercent; x.Status = r.Status; await db.SaveChangesAsync(ct); return Ok(x); }
    [HttpDelete("energy/batteries/{id:guid}"), Authorize(Policy = PermissionPolicies.EnergyManage)] public async Task<ActionResult> Battery(Guid id, CancellationToken ct) { var x = await db.SolarBatteries.FindAsync([id], ct); if (x is null) return NotFound(); db.Remove(x); await db.SaveChangesAsync(ct); return NoContent(); }
    [HttpPost("energy/controllers"), Authorize(Policy = PermissionPolicies.EnergyManage)] public async Task<ActionResult> Controller(ControllerRequest r, CancellationToken ct) { var x = new ChargeController { Name = r.Name, SolarPanelArrayId = r.SolarPanelArrayId, SolarBatteryId = r.SolarBatteryId, RatedCurrentAmps = r.RatedCurrentAmps, Status = r.Status }; db.Add(x); await db.SaveChangesAsync(ct); return Ok(x); }
    [HttpPut("energy/controllers/{id:guid}"), Authorize(Policy = PermissionPolicies.EnergyManage)] public async Task<ActionResult> Controller(Guid id, ControllerRequest r, CancellationToken ct) { var x = await db.ChargeControllers.FindAsync([id], ct); if (x is null) return NotFound(); x.Name = r.Name; x.SolarPanelArrayId = r.SolarPanelArrayId; x.SolarBatteryId = r.SolarBatteryId; x.RatedCurrentAmps = r.RatedCurrentAmps; x.Status = r.Status; await db.SaveChangesAsync(ct); return Ok(x); }
    [HttpDelete("energy/controllers/{id:guid}"), Authorize(Policy = PermissionPolicies.EnergyManage)] public async Task<ActionResult> Controller(Guid id, CancellationToken ct) { var x = await db.ChargeControllers.FindAsync([id], ct); if (x is null) return NotFound(); db.Remove(x); await db.SaveChangesAsync(ct); return NoContent(); }
    [HttpGet("energy/autonomy")] public async Task<ActionResult> Autonomy(int hours = 168, CancellationToken ct = default) => Ok(await db.EnergyReadings.AsNoTracking().Where(x => x.CapturedAtUtc >= DateTime.UtcNow.AddHours(-Math.Clamp(hours, 1, 8760))).OrderByDescending(x => x.CapturedAtUtc).Select(x => new { x.CapturedAtUtc, x.BatteryPercent, x.ConsumptionWatts, x.GenerationWatts, autonomyHours = x.ConsumptionWatts > 0 ? x.ChargeController.SolarBattery.CapacityWattHours * x.BatteryPercent / 100 / x.ConsumptionWatts : (decimal?)null }).ToListAsync(ct));

    [HttpGet("efficiency/baselines")] public async Task<ActionResult> Baselines(CancellationToken ct) => Ok(await db.WaterEfficiencyBaselines.AsNoTracking().Include(x => x.IrrigationZone).OrderBy(x => x.Name).ToListAsync(ct));
    [HttpPost("efficiency/baselines"), Authorize(Policy = PermissionPolicies.AnalyticsManage)] public async Task<ActionResult> Baseline(BaselineRequest r, CancellationToken ct) { var x = new WaterEfficiencyBaseline { IrrigationZoneId = r.IrrigationZoneId, Name = r.Name, LitersPerEvent = r.LitersPerEvent, AnomalyThresholdPercent = r.AnomalyThresholdPercent, ValidFromUtc = r.ValidFromUtc, IsActive = r.IsActive }; db.Add(x); await db.SaveChangesAsync(ct); return Ok(x); }
    [HttpPut("efficiency/baselines/{id:guid}"), Authorize(Policy = PermissionPolicies.AnalyticsManage)] public async Task<ActionResult> Baseline(Guid id, BaselineRequest r, CancellationToken ct) { var x = await db.WaterEfficiencyBaselines.FindAsync([id], ct); if (x is null) return NotFound(); x.IrrigationZoneId = r.IrrigationZoneId; x.Name = r.Name; x.LitersPerEvent = r.LitersPerEvent; x.AnomalyThresholdPercent = r.AnomalyThresholdPercent; x.ValidFromUtc = r.ValidFromUtc; x.IsActive = r.IsActive; await db.SaveChangesAsync(ct); return Ok(x); }
    [HttpDelete("efficiency/baselines/{id:guid}"), Authorize(Policy = PermissionPolicies.AnalyticsManage)] public async Task<ActionResult> Baseline(Guid id, CancellationToken ct) { var x = await db.WaterEfficiencyBaselines.FindAsync([id], ct); if (x is null) return NotFound(); db.Remove(x); await db.SaveChangesAsync(ct); return NoContent(); }
    [HttpGet("efficiency/analysis")] public async Task<ActionResult> Efficiency(int days = 30, CancellationToken ct = default) { var from = DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 3650)); var baselines = await db.WaterEfficiencyBaselines.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct); var records = await db.WaterConsumptionRecords.AsNoTracking().Include(x => x.IrrigationZone).Where(x => x.RecordedAtUtc >= from).ToListAsync(ct); var rows = records.Select(x => { var b = baselines.Where(y => y.IrrigationZoneId == x.IrrigationZoneId || y.IrrigationZoneId == null).OrderByDescending(y => y.IrrigationZoneId.HasValue).FirstOrDefault(); var saving = b is null ? (decimal?)null : b.LitersPerEvent - x.VolumeLiters; var deviation = b is null || b.LitersPerEvent == 0 ? (decimal?)null : (x.VolumeLiters - b.LitersPerEvent) / b.LitersPerEvent * 100; return new { x.Id, zone = x.IrrigationZone.Name, x.RecordedAtUtc, x.VolumeLiters, baselineLiters = b?.LitersPerEvent, savingLiters = saving, savingPercent = deviation.HasValue ? -deviation : null, isAnomaly = deviation.HasValue && Math.Abs(deviation.Value) >= b!.AnomalyThresholdPercent }; }).ToList(); return Ok(new { totalSavingLiters = rows.Sum(x => x.savingLiters ?? 0), anomalies = rows.Count(x => x.isAnomaly), rows }); }

    [HttpGet("work-orders")] public async Task<ActionResult> WorkOrders(string? equipmentId, CancellationToken ct) { var q = db.MaintenanceWorkOrders.AsNoTracking().AsQueryable(); if (!string.IsNullOrWhiteSpace(equipmentId)) q = q.Where(x => x.EquipmentId == equipmentId); return Ok(await q.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct)); }
    [HttpPost("work-orders"), Authorize(Policy = PermissionPolicies.MaintenanceManage)] public async Task<ActionResult> WorkOrder(WorkOrderRequest r, CancellationToken ct) { var x = Apply(new MaintenanceWorkOrder { Title = r.Title, EquipmentType = r.EquipmentType, EquipmentId = r.EquipmentId }, r); db.Add(x); await db.SaveChangesAsync(ct); return Ok(x); }
    [HttpPut("work-orders/{id:guid}"), Authorize(Policy = PermissionPolicies.MaintenanceManage)] public async Task<ActionResult> WorkOrder(Guid id, WorkOrderRequest r, CancellationToken ct) { var x = await db.MaintenanceWorkOrders.FindAsync([id], ct); if (x is null) return NotFound(); Apply(x, r); await db.SaveChangesAsync(ct); return Ok(x); }
    [HttpDelete("work-orders/{id:guid}"), Authorize(Policy = PermissionPolicies.MaintenanceManage)] public async Task<ActionResult> WorkOrder(Guid id, CancellationToken ct) { var x = await db.MaintenanceWorkOrders.FindAsync([id], ct); if (x is null) return NotFound(); db.Remove(x); await db.SaveChangesAsync(ct); return NoContent(); }

    [HttpGet("notification-rules")] public async Task<ActionResult> NotificationRules(CancellationToken ct) => Ok(await db.NotificationRules.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct));
    [HttpPost("notification-rules"), Authorize(Policy = PermissionPolicies.AlertsManage)] public async Task<ActionResult> NotificationRule(NotificationRuleRequest r, CancellationToken ct) { var x = Apply(new NotificationRule { Name = r.Name }, r); db.Add(x); await db.SaveChangesAsync(ct); return Ok(x); }
    [HttpPut("notification-rules/{id:guid}"), Authorize(Policy = PermissionPolicies.AlertsManage)] public async Task<ActionResult> NotificationRule(Guid id, NotificationRuleRequest r, CancellationToken ct) { var x = await db.NotificationRules.FindAsync([id], ct); if (x is null) return NotFound(); Apply(x, r); await db.SaveChangesAsync(ct); return Ok(x); }
    [HttpDelete("notification-rules/{id:guid}"), Authorize(Policy = PermissionPolicies.AlertsManage)] public async Task<ActionResult> NotificationRule(Guid id, CancellationToken ct) { var x = await db.NotificationRules.FindAsync([id], ct); if (x is null) return NotFound(); db.Remove(x); await db.SaveChangesAsync(ct); return NoContent(); }
    [HttpGet("notifications/summary")]
    public async Task<ActionResult> NotificationSummary(string period = "daily", CancellationToken ct = default)
    {
        var days = period.Equals("weekly", StringComparison.OrdinalIgnoreCase) ? 7 : 1; var from = DateTime.UtcNow.AddDays(-days);
        var alerts = await db.SystemAlerts.AsNoTracking().Where(x => x.RaisedAtUtc >= from).ToListAsync(ct);
        return Ok(new { period = days == 7 ? "Semanal" : "Diario", fromUtc = from, toUtc = DateTime.UtcNow, total = alerts.Count, active = alerts.Count(x => x.Status == "Activa"), resolved = alerts.Count(x => x.Status == "Resuelta"), critical = alerts.Count(x => x.Severity == "Crítica" || x.Severity == "Critica"), byType = alerts.GroupBy(x => x.Type).OrderByDescending(x => x.Count()).Select(x => new { type = x.Key, count = x.Count() }) });
    }
    [HttpGet("integrations/history")] public async Task<ActionResult> IntegrationHistory(CancellationToken ct) => Ok(await db.IntegrationExecutions.AsNoTracking().OrderByDescending(x => x.ExecutedAtUtc).Take(300).ToListAsync(ct));
    [HttpPost("integrations/{name}/test"), Authorize(Policy = PermissionPolicies.SettingsManage)] public async Task<ActionResult> TestIntegration(string name, CancellationToken ct) { var key = name.Equals("n8n", StringComparison.OrdinalIgnoreCase) ? "N8N_WEBHOOK_URL" : name.Equals("powerbi", StringComparison.OrdinalIgnoreCase) ? "POWERBI_PUSH_URL" : name.Equals("telegram", StringComparison.OrdinalIgnoreCase) ? "TELEGRAM_WEBHOOK_URL" : name.Equals("teams", StringComparison.OrdinalIgnoreCase) ? "TEAMS_WEBHOOK_URL" : null; if (key is null) return BadRequest(new { message = "Integración no soportada." }); var url = await db.GlobalParameters.Where(x => x.Key == key).Select(x => x.Value).SingleOrDefaultAsync(ct); if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)) return BadRequest(new { message = $"Configura {key} con una URL HTTP válida." }); var log = new IntegrationExecution { Integration = name, Operation = "Prueba de conexión" }; try { using var response = await httpClients.CreateClient().PostAsJsonAsync(uri, new { source = "SistemaRiego", eventType = "CONNECTION_TEST", sentAtUtc = DateTime.UtcNow }, ct); log.HttpStatusCode = (int)response.StatusCode; log.Status = response.IsSuccessStatusCode ? "Exitoso" : "Fallido"; log.Detail = response.ReasonPhrase; } catch (Exception ex) { log.Status = "Fallido"; log.Detail = ex.Message; } db.Add(log); await db.SaveChangesAsync(ct); return Ok(log); }

    [HttpGet("powerbi/model")] public ActionResult PowerBiModel() => Ok(new { relationships = new[] { "DimZona[Id] 1-* HechoConsumo[ZonaId]", "DimFecha[Fecha] 1-* HechoConsumo[Fecha]", "DimCultivo[Id] 1-* HechoConsumo[CultivoId]" }, dax = new Dictionary<string, string> { ["Consumo total (L)"] = "SUM(HechoConsumo[VolumenLitros])", ["Ahorro (L)"] = "SUM(HechoConsumo[LineaBaseLitros]) - [Consumo total (L)]", ["Ahorro %"] = "DIVIDE([Ahorro (L)], SUM(HechoConsumo[LineaBaseLitros]))", ["Eficiencia %"] = "100 - ABS(DIVIDE(SUM(HechoConsumo[Desviacion]), [Consumo total (L)])) * 100" }, views = new[] { "vw_PowerBI_Consumption", "vw_PowerBI_Irrigation", "vw_PowerBI_Alerts", "vw_PowerBI_Energy" } });

    private async Task EvaluateFill(AutomaticFillConfiguration x, CancellationToken ct) { var tank = await db.WaterTanks.FindAsync([x.WaterTankId], ct); if (tank is null || tank.CapacityLiters <= 0) { x.LastDecision = "Tanque no disponible"; return; } var percent = tank.CurrentLevelLiters / tank.CapacityLiters * 100; x.LastEvaluatedAtUtc = DateTime.UtcNow; x.LastDecision = !x.IsEnabled ? "Desactivado" : percent <= x.StartAtPercent ? "Solicitar llenado" : percent >= x.StopAtPercent ? "Detener llenado" : "Mantener estado"; }
    private static string? ValidateSchedule(ScheduleRequest r) => r.DurationMinutes is < 1 or > 120 || r.FlowRateLitersMinute is <= 0 or > 100 ? "Duración o caudal fuera del rango permitido." : r.Recurrence == "Recurrente" && (!r.IntervalDays.HasValue || r.IntervalDays < 1) ? "Indica el intervalo de la recurrencia." : null;
    private static IrrigationSchedule Apply(IrrigationSchedule x, ScheduleRequest r) { x.Name = r.Name.Trim(); x.IrrigationZoneId = r.IrrigationZoneId; x.NextRunAtUtc = r.NextRunAtUtc; x.Recurrence = r.Recurrence; x.IntervalDays = r.Recurrence == "Recurrente" ? r.IntervalDays : null; x.DurationMinutes = r.DurationMinutes; x.FlowRateLitersMinute = r.FlowRateLitersMinute; x.IsActive = r.IsActive; return x; }
    private static WaterSource Apply(WaterSource x, WaterSourceRequest r) { x.Code = r.Code.Trim().ToUpperInvariant(); x.Name = r.Name.Trim(); x.Type = r.Type; x.MaximumFlowLitersMinute = r.MaximumFlowLitersMinute; x.IsActive = r.IsActive; x.Notes = r.Notes; return x; }
    private static MaintenanceWorkOrder Apply(MaintenanceWorkOrder x, WorkOrderRequest r) { x.Kind = r.Kind; x.Title = r.Title; x.EquipmentType = r.EquipmentType; x.EquipmentId = r.EquipmentId; x.TechnicianName = r.TechnicianName; x.TechnicianEmail = r.TechnicianEmail; x.CommitmentAtUtc = r.CommitmentAtUtc; x.Status = r.Status; x.Notes = r.Notes; x.CompletedAtUtc = r.Status == "Completada" ? DateTime.UtcNow : null; return x; }
    private static NotificationRule Apply(NotificationRule x, NotificationRuleRequest r) { x.Name = r.Name; x.AlertType = r.AlertType; x.MinimumSeverity = r.MinimumSeverity; x.Channels = r.Channels; x.Recipients = r.Recipients; x.DailySummary = r.DailySummary; x.WeeklySummary = r.WeeklySummary; x.IsActive = r.IsActive; return x; }
}

public sealed record HydraulicRequest(Guid IrrigationZoneId, Guid? WaterSourceId, Guid? WaterTankId, Guid? WaterPumpId, decimal PipeDiameterMillimeters, decimal PipeLengthMeters, decimal DesignFlowLitersMinute, decimal MinimumPressureBar, decimal MaximumPressureBar);
public sealed record ZoneStateRequest(string StatusCode, bool IsActive);
public sealed record ScheduleRequest(string Name, Guid IrrigationZoneId, DateTime NextRunAtUtc, string Recurrence, int? IntervalDays, int DurationMinutes, decimal FlowRateLitersMinute, bool IsActive);
public sealed record WaterSourceRequest(string Code, string Name, string Type, decimal MaximumFlowLitersMinute, bool IsActive, string? Notes);
public sealed record AutoFillRequest(Guid WaterTankId, Guid WaterPumpId, Guid WaterSourceId, decimal StartAtPercent, decimal StopAtPercent, bool IsEnabled);
public sealed record SolarArrayRequest(string Name, decimal RatedPowerWatts, int PanelCount, bool IsActive);
public sealed record BatteryRequest(string Name, decimal CapacityWattHours, decimal NominalVoltage, decimal MinimumSafeChargePercent, decimal CurrentChargePercent, string Status);
public sealed record ControllerRequest(string Name, Guid SolarPanelArrayId, Guid SolarBatteryId, decimal RatedCurrentAmps, string Status);
public sealed record BaselineRequest(Guid? IrrigationZoneId, string Name, decimal LitersPerEvent, decimal AnomalyThresholdPercent, DateTime ValidFromUtc, bool IsActive);
public sealed record WorkOrderRequest(string Kind, string Title, string EquipmentType, string EquipmentId, string? TechnicianName, string? TechnicianEmail, DateTime CommitmentAtUtc, string Status, string? Notes);
public sealed record NotificationRuleRequest(string Name, string AlertType, string MinimumSeverity, string Channels, string Recipients, bool DailySummary, bool WeeklySummary, bool IsActive);
