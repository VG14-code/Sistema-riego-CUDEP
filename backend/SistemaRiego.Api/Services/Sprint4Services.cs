using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Services;

public sealed record PumpStationTelemetry(string PumpCode, DateTime CapturedAtUtc, decimal LevelLiters, decimal PressureBar, decimal MotorCurrentAmps, bool IsPumpRunning, string MessageId);
public sealed record EnergyTelemetry(DateTime CapturedAtUtc, decimal GenerationWatts, decimal BatteryPercent, decimal ConsumptionWatts, decimal BatteryVoltage, string MessageId);
public sealed record FlowTelemetry(string IrrigationZoneCode, DateTime CapturedAtUtc, decimal FlowLitersMinute, string MessageId);

public interface IPumpCommandService { Task<IoTCommand> SendAsync(WaterPump pump, string commandType, Guid? userId, CancellationToken ct); }
public sealed class PumpCommandService(AppDbContext db, IMqttCommandPublisher mqtt) : IPumpCommandService
{
    public async Task<IoTCommand> SendAsync(WaterPump pump, string commandType, Guid? userId, CancellationToken ct)
    {
        if (pump.IoTDeviceId is null) throw new InvalidOperationException("La bomba no tiene dispositivo IoT asociado.");
        var now = DateTime.UtcNow;
        var command = new IoTCommand { DeviceId = pump.IoTDeviceId.Value, CommandType = commandType, Status = "Pendiente", RequestedAtUtc = now, ExpiresAtUtc = now.AddSeconds(15), RequestedByUserId = userId };
        db.IoTCommands.Add(command); await db.SaveChangesAsync(ct);
        var payload = new { commandId = command.Id, commandType, pumpId = pump.Id };
        command.Payload = System.Text.Json.JsonSerializer.Serialize(payload);
        try { await mqtt.PublishPumpCommandAsync(command.DeviceId, payload, ct); command.Status = "Publicado"; }
        catch (Exception ex) { command.Status = "Fallido"; command.FailedAtUtc = DateTime.UtcNow; command.FailureReason = ex.Message; }
        await db.SaveChangesAsync(ct);
        if (command.Status == "Fallido") throw new InvalidOperationException("No fue posible publicar el comando de bomba en MQTT.");
        return command;
    }
}

public sealed class PumpAckService(AppDbContext db)
{
    public async Task<Guid?> ProcessAsync(Guid deviceId, string payload, CancellationToken ct)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(payload); var root = doc.RootElement;
        var commandId = root.TryGetProperty("commandId", out var value) && Guid.TryParse(value.ToString(), out var parsed) ? parsed : (Guid?)null;
        var command = commandId.HasValue ? await db.IoTCommands.SingleOrDefaultAsync(x => x.Id == commandId && x.DeviceId == deviceId, ct) : null;
        if (command is null) return null;
        var pump = await db.WaterPumps.Include(x => x.WaterTank).SingleOrDefaultAsync(x => x.IoTDeviceId == deviceId, ct);
        if (pump is null) return null;
        var now = DateTime.UtcNow; command.Status = "Confirmado"; command.ConfirmedAtUtc = now;
        var running = root.TryGetProperty("isRunning", out value) && value.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False ? value.GetBoolean() : command.CommandType == "ENCENDER_BOMBA";
        pump.IsRunning = running; pump.Status = running ? "Encendida" : "Detenida";
        if (running) pump.StartedAtUtc ??= now;
        else { pump.LastStoppedAtUtc = now; pump.StartedAtUtc = null; await CompleteSupplyEvent(pump, now, ct); }
        db.OperationalEvents.Add(new OperationalEvent { Category = "Abastecimiento", EventType = running ? "PUMP_START_ACK" : "PUMP_STOP_ACK", Detail = $"{pump.Name}: estado confirmado por MQTT/ACK." });
        await db.SaveChangesAsync(ct); return command.Id;
    }
    private async Task CompleteSupplyEvent(WaterPump pump, DateTime now, CancellationToken ct)
    {
        var evt = await db.WaterSupplyEvents.Where(x => x.WaterPumpId == pump.Id && x.Status == "En curso").OrderByDescending(x => x.StartedAtUtc).FirstOrDefaultAsync(ct);
        if (evt is null) return;
        evt.Status = "Completado"; evt.EndedAtUtc = now; evt.FinalLevelLiters = pump.WaterTank.CurrentLevelLiters; evt.SuppliedLiters = Math.Max(0, pump.WaterTank.CurrentLevelLiters - evt.InitialLevelLiters);
    }
}

public interface ISprint4TelemetryService
{
    Task IngestStationAsync(PumpStationTelemetry telemetry, CancellationToken ct);
    Task IngestEnergyAsync(EnergyTelemetry telemetry, CancellationToken ct);
    Task IngestFlowAsync(FlowTelemetry telemetry, CancellationToken ct);
}
public sealed class Sprint4TelemetryService(AppDbContext db, IPumpCommandService pumpCommands, IAlertService? alerts = null) : ISprint4TelemetryService
{
    public async Task IngestStationAsync(PumpStationTelemetry x, CancellationToken ct)
    {
        if (await db.PumpStationReadings.AnyAsync(r => r.MessageId == x.MessageId, ct)) return;
        var pump = await db.WaterPumps.Include(p => p.WaterTank).SingleAsync(p => p.Code == x.PumpCode, ct);
        var tank = pump.WaterTank; tank.CurrentLevelLiters = Math.Clamp(x.LevelLiters, 0, tank.CapacityLiters); tank.LastLevelReadingUtc = x.CapturedAtUtc;
        pump.LastPressureBar = x.PressureBar; pump.LastMotorCurrentAmps = x.MotorCurrentAmps; pump.LastTelemetryAtUtc = x.CapturedAtUtc; pump.IsRunning = x.IsPumpRunning;
        db.PumpStationReadings.Add(new PumpStationReading { WaterPumpId = pump.Id, WaterTankId = tank.Id, CapturedAtUtc = x.CapturedAtUtc, LevelLiters = tank.CurrentLevelLiters, PressureBar = x.PressureBar, MotorCurrentAmps = x.MotorCurrentAmps, IsPumpRunning = x.IsPumpRunning, MessageId = x.MessageId });
        var percent = tank.CapacityLiters == 0 ? 0 : tank.CurrentLevelLiters / tank.CapacityLiters * 100;
        var fault = percent <= tank.MinimumSafePercent ? "Nivel bajo: protección contra trabajo en seco" : x.MotorCurrentAmps > pump.MaximumCurrentAmps ? "Sobrecorriente detectada" : null;
        if (fault is not null)
        {
            tank.Status = "Nivel bajo"; pump.HasUnacknowledgedFault = true; pump.FailureReason = fault; pump.Status = x.IsPumpRunning ? "Parada de seguridad pendiente" : "Falla";
            db.OperationalEvents.Add(new OperationalEvent { Category = "Seguridad", EventType = "PUMP_SAFETY_TRIP", Severity = "Crítico", Detail = $"{pump.Name}: {fault}." });
            await db.SaveChangesAsync(ct);
            if (alerts is not null) await alerts.RaiseAsync(new AlertSignal($"PUMP:{pump.Id}:{(percent <= tank.MinimumSafePercent ? "DRY_RUN" : "OVERCURRENT")}", "Falla de dispositivo", "Crítica", $"{pump.Name}: {fault}.", "Bomba", pump.Id.ToString()), ct);
            if (x.IsPumpRunning) await pumpCommands.SendAsync(pump, "APAGAR_BOMBA", null, ct);
            return;
        }
        tank.Status = "Disponible"; pump.Status = pump.HasUnacknowledgedFault ? (x.IsPumpRunning ? "Parada de seguridad pendiente" : "Falla") : x.IsPumpRunning ? "Encendida" : "Detenida"; await db.SaveChangesAsync(ct);
    }
    public async Task IngestEnergyAsync(EnergyTelemetry x, CancellationToken ct)
    {
        if (await db.EnergyReadings.AnyAsync(r => r.MessageId == x.MessageId, ct)) return;
        var controller = await db.ChargeControllers.Include(c => c.SolarBattery).OrderBy(c => c.Name).FirstAsync(ct);
        controller.SolarBattery.CurrentChargePercent = Math.Clamp(x.BatteryPercent, 0, 100);
        controller.SolarBattery.Status = x.BatteryPercent <= controller.SolarBattery.MinimumSafeChargePercent ? "Batería baja" : "Disponible";
        db.EnergyReadings.Add(new EnergyReading { ChargeControllerId = controller.Id, CapturedAtUtc = x.CapturedAtUtc, GenerationWatts = x.GenerationWatts, BatteryPercent = x.BatteryPercent, ConsumptionWatts = x.ConsumptionWatts, BatteryVoltage = x.BatteryVoltage, MessageId = x.MessageId });
        if (x.BatteryPercent <= controller.SolarBattery.MinimumSafeChargePercent) { db.OperationalEvents.Add(new OperationalEvent { Category = "Energía", EventType = "LOW_BATTERY", Severity = "Advertencia", Detail = $"Batería solar en {x.BatteryPercent:0.0}%." }); if (alerts is not null) await alerts.RaiseAsync(new AlertSignal($"ENERGY:{controller.Id}:LOW_BATTERY", "Energía", "Advertencia", $"Batería solar en {x.BatteryPercent:0.0}%.", "Controlador solar", controller.Id.ToString()), ct); }
        var localHour = x.CapturedAtUtc.ToLocalTime().Hour;
        if (localHour is >= 7 and <= 17 && x.GenerationWatts < 5) { db.OperationalEvents.Add(new OperationalEvent { Category = "Energía", EventType = "NO_DAYLIGHT_GENERATION", Severity = "Advertencia", Detail = "No se detectó generación solar durante horario diurno." }); if (alerts is not null) await alerts.RaiseAsync(new AlertSignal($"ENERGY:{controller.Id}:NO_GENERATION", "Energía", "Advertencia", "No se detectó generación solar durante horario diurno.", "Controlador solar", controller.Id.ToString()), ct); }
        await db.SaveChangesAsync(ct);
    }
    public async Task IngestFlowAsync(FlowTelemetry x, CancellationToken ct)
    {
        if (await db.FlowReadings.AnyAsync(r => r.MessageId == x.MessageId, ct)) return;
        var zoneId = await db.IrrigationZones.Where(z => z.Code == x.IrrigationZoneCode).Select(z => z.Id).SingleAsync(ct);
        db.FlowReadings.Add(new FlowReading { IrrigationZoneId = zoneId, CapturedAtUtc = x.CapturedAtUtc, FlowLitersMinute = Math.Max(0, x.FlowLitersMinute), MessageId = x.MessageId }); await db.SaveChangesAsync(ct);
    }
}

public interface IWaterCapacityService { Task<int> GetMaximumValveCountAsync(CancellationToken ct); }
public sealed class WaterCapacityService(AppDbContext db) : IWaterCapacityService
{
    public async Task<int> GetMaximumValveCountAsync(CancellationToken ct)
    {
        var configured = int.TryParse(await db.GlobalParameters.Where(x => x.Key == "MAX_SIMULTANEOUS_VALVES").Select(x => x.Value).SingleOrDefaultAsync(ct), out var max) ? Math.Max(1, max) : 2;
        var pump = await db.WaterPumps.Include(x => x.WaterTank).OrderBy(x => x.Name).FirstOrDefaultAsync(ct);
        var safety = await db.SystemSafetyStates.FindAsync([1], ct);
        if (pump is null || safety?.EmergencyStopActive == true || pump.HasUnacknowledgedFault || pump.WaterTank.CurrentLevelLiters <= pump.WaterTank.CapacityLiters * pump.WaterTank.MinimumSafePercent / 100) return 0;
        var byFlow = Math.Max(1, (int)Math.Floor(pump.RatedFlowLitersMinute / Math.Max(1, pump.NominalValveFlowLitersMinute)));
        var byPressure = pump.LastPressureBar <= 0 ? configured : Math.Max(1, (int)Math.Floor(pump.LastPressureBar / Math.Max(.1m, pump.MinimumPressureBar)));
        return Math.Min(configured, Math.Min(byFlow, byPressure));
    }
}

public interface IConsumptionCalculator { Task<WaterConsumptionRecord> BuildAsync(IrrigationRun run, DateTime endedAtUtc, CancellationToken ct); }
public sealed class ConsumptionCalculator(AppDbContext db, IAlertService? alerts = null) : IConsumptionCalculator
{
    public async Task<WaterConsumptionRecord> BuildAsync(IrrigationRun run, DateTime endedAtUtc, CancellationToken ct)
    {
        var started = run.StartedAtUtc ?? run.RequestedAtUtc; var minutes = Math.Clamp((decimal)(endedAtUtc - started).TotalMinutes, .01m, run.PlannedDurationMinutes);
        var readings = await db.FlowReadings.Where(x => x.IrrigationZoneId == run.IrrigationZoneId && x.CapturedAtUtc >= started && x.CapturedAtUtc <= endedAtUtc).Select(x => x.FlowLitersMinute).ToListAsync(ct);
        var measured = readings.Count > 0; var flow = measured ? readings.Average() : run.FlowRateLitersMinute; var volume = Math.Round(flow * minutes, 2);
        var recommended = run.IrrigationRuleId.HasValue ? await db.IrrigationRules.Where(x => x.Id == run.IrrigationRuleId).Select(x => x.CropWaterRequirement == null ? (decimal?)null : x.CropWaterRequirement.BaseVolumeLiters).SingleOrDefaultAsync(ct) : null;
        var tariff = decimal.TryParse(await db.GlobalParameters.Where(x => x.Key == "WATER_TARIFF_PER_M3").Select(x => x.Value).SingleOrDefaultAsync(ct), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var cost) ? cost : 3.5m;
        var record = new WaterConsumptionRecord { IrrigationRunId = run.Id, IrrigationZoneId = run.IrrigationZoneId, Source = measured ? "Caudal real simulado" : "Estimado", IsMeasured = measured, FlowRateLitersMinute = flow, DurationMinutes = minutes, VolumeLiters = volume, RecommendedVolumeLiters = recommended, DeviationPercent = recommended > 0 ? Math.Round((volume - recommended.Value) / recommended.Value * 100, 2) : null, EstimatedCost = Math.Round(volume / 1000 * tariff, 4), RecordedAtUtc = endedAtUtc };
        if (alerts is not null && Math.Abs(record.DeviationPercent ?? 0) >= 25) await alerts.RaiseAsync(new AlertSignal($"FLOW:{run.Id}:DEVIATION", "Flujo", "Advertencia", $"El riego {run.Id} se desvió {record.DeviationPercent:0.0}% del requerimiento agronómico.", "Zona", run.IrrigationZoneId.ToString()), ct);
        return record;
    }
}
