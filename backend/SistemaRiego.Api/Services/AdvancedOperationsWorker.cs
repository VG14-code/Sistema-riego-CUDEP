using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Services;

/// <summary>Ejecuta programaciones autorizadas y controla el llenado por nivel con protecciones y comandos MQTT confirmables.</summary>
public sealed class AdvancedOperationsWorker(IServiceScopeFactory scopes, ILogger<AdvancedOperationsWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await ProcessSchedules(scope.ServiceProvider, stoppingToken);
                await ProcessAutomaticFill(scope.ServiceProvider, stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception) { logger.LogError(exception, "Falló el ciclo de programaciones y llenado automático."); await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
        }
    }

    public static async Task ProcessSchedules(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<AppDbContext>(); var commands = services.GetRequiredService<IIrrigationCommandService>(); var capacity = services.GetRequiredService<IWaterCapacityService>(); var now = DateTime.UtcNow;
        var schedules = await db.IrrigationSchedules.Include(x => x.IrrigationZone).Where(x => x.IsActive && x.NextRunAtUtc <= now).OrderBy(x => x.NextRunAtUtc).Take(20).ToListAsync(ct);
        // El maximo de valvulas simultaneas se comprueba igual que en el motor de reglas:
        // antes solo se miraba que no fuera cero y dos programaciones vencidas abrian dos
        // zonas a la vez aunque el limite fuera una, superando el caudal previsto.
        var maximum = await capacity.GetMaximumValveCountAsync(ct);
        var occupied = await ActiveValveCount(db, ct);
        foreach (var schedule in schedules)
        {
            if (!schedule.IrrigationZone.IsActive) { Postpone(schedule, now, "Zona inactiva"); continue; }
            if (await db.IrrigationRuns.AnyAsync(x => x.IrrigationZoneId == schedule.IrrigationZoneId && (x.Status == "En curso" || x.Status == "Esperando ACK" || x.Status == "Cierre pendiente"), ct)) { Postpone(schedule, now, "La zona ya tiene un riego activo"); continue; }
            if (maximum <= 0) { Postpone(schedule, now, "Sin capacidad hidráulica segura"); continue; }
            var valveCount = await ZoneValveCount(db, schedule.IrrigationZoneId, ct);
            if (valveCount == 0) { Postpone(schedule, now, "La zona no tiene válvulas asignadas"); continue; }
            if (occupied + valveCount > maximum) { Postpone(schedule, now, $"Capacidad simultánea alcanzada ({occupied}/{maximum} válvulas abiertas)"); continue; }
            var required = schedule.DurationMinutes * schedule.FlowRateLitersMinute;
            var tanks = await db.WaterTanks.Where(x => x.Status != "Inactivo").ToListAsync(ct); var available = tanks.Sum(x => Math.Max(0, x.CurrentLevelLiters - x.CapacityLiters * x.MinimumSafePercent / 100));
            if (available < required) { Postpone(schedule, now, "Reserva de agua insuficiente"); continue; }
            var run = new IrrigationRun { IrrigationZoneId = schedule.IrrigationZoneId, Mode = "Programado", Status = "Esperando ACK", PlannedDurationMinutes = schedule.DurationMinutes, FlowRateLitersMinute = schedule.FlowRateLitersMinute, RequestedAtUtc = now, RequestedByEmail = schedule.CreatedByEmail, Reason = $"Programación: {schedule.Name}" };
            db.IrrigationRuns.Add(run); await db.SaveChangesAsync(ct);
            try
            {
                await commands.SendAsync(schedule.IrrigationZoneId, run, "ABRIR_VALVULA", null, ct);
                occupied += valveCount;
                Complete(schedule, now, $"Riego {run.Id} solicitado automáticamente");
                db.OperationalEvents.Add(new OperationalEvent { Category = "Riego programado", EventType = "SCHEDULED_IRRIGATION_REQUESTED", IrrigationZoneId = schedule.IrrigationZoneId, IrrigationRunId = run.Id, Detail = $"{schedule.Name}: orden MQTT enviada; esperando ACK." });
            }
            catch (Exception exception)
            {
                run.Status = "Fallido"; run.EndedAtUtc = DateTime.UtcNow; Postpone(schedule, now, $"Falló el comando: {exception.Message}");
                db.OperationalEvents.Add(new OperationalEvent { Category = "Riego programado", EventType = "SCHEDULED_IRRIGATION_FAILED", Severity = "Error", IrrigationZoneId = schedule.IrrigationZoneId, IrrigationRunId = run.Id, Detail = exception.Message });
            }
        }
        await db.SaveChangesAsync(ct);
    }

    public static async Task ProcessAutomaticFill(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<AppDbContext>(); var commands = services.GetRequiredService<IPumpCommandService>(); var now = DateTime.UtcNow;
        var configurations = await db.AutomaticFillConfigurations.Include(x => x.WaterTank).Include(x => x.WaterPump).Include(x => x.WaterSource).Where(x => x.IsEnabled).ToListAsync(ct);
        foreach (var item in configurations)
        {
            var pump = item.WaterPump; var tank = item.WaterTank; var percent = tank.CapacityLiters <= 0 ? 0 : tank.CurrentLevelLiters / tank.CapacityLiters * 100; item.LastEvaluatedAtUtc = now;
            if (!item.WaterSource.IsActive || item.WaterSource.MaximumFlowLitersMinute <= 0) { item.LastDecision = "Fuente inactiva o sin caudal"; continue; }
            if (tank.Status == "Inactivo") { item.LastDecision = "Tanque inactivo"; continue; }
            if (pump.IoTDeviceId is null) { item.LastDecision = "Bomba sin dispositivo IoT"; continue; }
            if (pump.HasUnacknowledgedFault || pump.LockedUntilUtc > now) { item.LastDecision = "Bomba bloqueada por seguridad"; continue; }
            if (percent <= item.StartAtPercent && !pump.IsRunning)
            {
                if (await Pending(db, pump.IoTDeviceId.Value, "ENCENDER_BOMBA", now, ct)) { item.LastDecision = "Esperando ACK de encendido"; continue; }
                try
                {
                    var command = await commands.SendAsync(pump, "ENCENDER_BOMBA", null, ct);
                    db.WaterSupplyEvents.Add(new WaterSupplyEvent { WaterPumpId = pump.Id, EventType = "Llenado automático", Status = "En curso", InitialLevelLiters = tank.CurrentLevelLiters, Detail = $"Fuente: {item.WaterSource.Name}; umbral {item.StartAtPercent:0.##}%.", StartCommandId = command.Id });
                    item.LastDecision = "Orden automática de encendido enviada";
                }
                catch (Exception exception) { item.LastDecision = $"Falló encendido: {exception.Message}"; }
            }
            else if (percent >= item.StopAtPercent && pump.IsRunning)
            {
                if (await Pending(db, pump.IoTDeviceId.Value, "APAGAR_BOMBA", now, ct)) { item.LastDecision = "Esperando ACK de parada"; continue; }
                try
                {
                    var command = await commands.SendAsync(pump, "APAGAR_BOMBA", null, ct);
                    var supply = await db.WaterSupplyEvents.Where(x => x.WaterPumpId == pump.Id && x.Status == "En curso").OrderByDescending(x => x.StartedAtUtc).FirstOrDefaultAsync(ct); if (supply is not null) supply.StopCommandId = command.Id;
                    item.LastDecision = "Orden automática de parada enviada";
                }
                catch (Exception exception) { item.LastDecision = $"Falló parada: {exception.Message}"; }
            }
            else item.LastDecision = pump.IsRunning ? "Llenando automáticamente" : "Nivel dentro del rango";
        }
        await db.SaveChangesAsync(ct);
    }

    private static Task<bool> Pending(AppDbContext db, Guid deviceId, string command, DateTime now, CancellationToken ct) => db.IoTCommands.AnyAsync(x => x.DeviceId == deviceId && x.CommandType == command && (x.Status == "Pendiente" || x.Status == "Publicado") && x.ExpiresAtUtc > now, ct);
    private static async Task<int> ActiveValveCount(AppDbContext db, CancellationToken ct)
    {
        var zones = await db.IrrigationRuns.Where(x => x.Status == "En curso" || x.Status == "Esperando ACK" || x.Status == "Cierre pendiente").Select(x => x.IrrigationZoneId).Distinct().ToListAsync(ct);
        return await ZoneValveCount(db, zones, ct);
    }
    private static Task<int> ZoneValveCount(AppDbContext db, Guid zoneId, CancellationToken ct) => ZoneValveCount(db, [zoneId], ct);
    private static async Task<int> ZoneValveCount(AppDbContext db, IReadOnlyCollection<Guid> zones, CancellationToken ct)
    {
        if (zones.Count == 0) return 0;
        var linked = await db.IrrigationZoneValves.Where(x => zones.Contains(x.IrrigationZoneId)).Select(x => x.DeviceId).Distinct().CountAsync(ct);
        var legacy = await db.IrrigationZones.Where(x => zones.Contains(x.Id) && x.ValveDeviceId != null && !db.IrrigationZoneValves.Any(v => v.IrrigationZoneId == x.Id)).Select(x => x.ValveDeviceId).Distinct().CountAsync(ct);
        return linked + legacy;
    }
    private static void Complete(IrrigationSchedule schedule, DateTime now, string result) { schedule.LastRunAtUtc = now; schedule.LastResult = result; if (schedule.Recurrence == "Recurrente" && schedule.IntervalDays > 0) { do schedule.NextRunAtUtc = schedule.NextRunAtUtc.AddDays(schedule.IntervalDays.Value); while (schedule.NextRunAtUtc <= now); } else schedule.IsActive = false; }
    private static void Postpone(IrrigationSchedule schedule, DateTime now, string result) { schedule.LastResult = result; schedule.NextRunAtUtc = now.AddMinutes(5); }
}