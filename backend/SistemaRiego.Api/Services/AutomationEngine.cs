using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Services;

public interface IAutomationEngine { Task<AutomationEvaluation> EvaluateAsync(CancellationToken ct); }
public sealed record AutomationDecision(Guid Id, string Name, string LastDecision, string? LastReason, bool Started);
public sealed record AutomationEvaluation(DateTime EvaluatedAtUtc, IReadOnlyList<AutomationDecision> Results);

public sealed class AutomationEngine(AppDbContext db, IIrrigationCommandService commands, ILogger<AutomationEngine> logger, IWaterCapacityService? capacityService = null) : IAutomationEngine
{
    public async Task<AutomationEvaluation> EvaluateAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow; var local = now.ToLocalTime(); var time = TimeOnly.FromDateTime(local); var day = ((int)local.DayOfWeek + 6) % 7 + 1;
        var rules = await db.IrrigationRules.Include(x => x.IrrigationZone).Where(x => x.IsEnabled).OrderBy(x => x.Priority).ThenBy(x => x.Id).ToListAsync(ct);
        var results = new List<AutomationDecision>(); var maximum = capacityService is null ? await ParameterInt("MAX_SIMULTANEOUS_VALVES", 2, ct) : await capacityService.GetMaximumValveCountAsync(ct); var occupied = await ActiveValveCount(ct);
        foreach (var group in rules.GroupBy(x => x.IrrigationZoneId))
        {
            var winner = group.OrderBy(x => x.Priority).ThenBy(x => x.Id).First();
            foreach (var ignored in group.Where(x => x.Id != winner.Id))
            {
                ignored.LastEvaluatedAtUtc = now; ignored.LastDecision = "Omitida por prioridad"; ignored.LastReason = $"La regla {winner.Name} tiene prioridad {winner.Priority}.";
                results.Add(new(ignored.Id, ignored.Name, ignored.LastDecision, ignored.LastReason, false));
            }
            var reading = await db.SensorReadings.Where(x => x.IrrigationZoneId == winner.IrrigationZoneId && x.IsValid).OrderByDescending(x => x.CapturedAtUtc).FirstOrDefaultAsync(ct);
            var blocked = winner.SuspendedUntilUtc > now || !winner.AllowedDays.Split(',').Contains(day.ToString()) || !InsideWindow(time, winner.AllowedFrom, winner.AllowedUntil);
            var active = await db.IrrigationRuns.AnyAsync(x => x.IrrigationZoneId == winner.IrrigationZoneId && (x.Status == "En curso" || x.Status == "Esperando ACK" || x.Status == "Cierre pendiente"), ct);
            var valveCount = await ZoneValveCount(winner.IrrigationZoneId, ct);
            var capacity = valveCount > 0 && occupied + valveCount <= maximum;
            var minimumBattery = await ParameterInt("MIN_AUTOMATION_BATTERY_PERCENT", 25, ct);
            var battery = await db.EnergyReadings.OrderByDescending(x => x.CapturedAtUtc).Select(x => (decimal?)x.BatteryPercent).FirstOrDefaultAsync(ct);
            var energyOk = !winner.RequiresSufficientEnergy || battery is null || battery >= minimumBattery;
            var irrigate = !blocked && !active && capacity && energyOk && reading is not null && reading.Value < winner.MinimumMoisturePercent;
            winner.LastEvaluatedAtUtc = now;
            winner.LastDecision = irrigate ? "Regar" : blocked ? "Fuera de ventana" : active ? "Riego activo" : valveCount == 0 ? "Sin válvula" : !capacity ? (capacityService is null ? "Límite global" : "Capacidad hidráulica insuficiente") : !energyOk ? "Energía insuficiente" : "No regar";
            winner.LastReason = reading is null ? "No hay una lectura válida." : $"Humedad {reading.Value:0.0}% frente al mínimo {winner.MinimumMoisturePercent:0.0}%.";
            if (irrigate)
            {
                var run = new IrrigationRun { IrrigationZoneId = winner.IrrigationZoneId, IrrigationRuleId = winner.Id, Mode = "Automático", Status = "Esperando ACK", PlannedDurationMinutes = winner.MaximumDurationMinutes, FlowRateLitersMinute = 12, RequestedAtUtc = now, Reason = winner.LastReason };
                db.IrrigationRuns.Add(run); await db.SaveChangesAsync(ct);
                try
                {
                    await commands.SendAsync(winner.IrrigationZoneId, run, "ABRIR_VALVULA", null, ct); occupied += valveCount;
                    db.OperationalEvents.Add(new OperationalEvent { Category = "Automatización", EventType = "AUTOMATIC_IRRIGATION_REQUESTED", IrrigationZoneId = winner.IrrigationZoneId, IrrigationRunId = run.Id, Detail = $"{winner.Name}: orden MQTT enviada; esperando ACK." });
                }
                catch (Exception exception)
                {
                    run.Status = "Fallido"; run.EndedAtUtc = DateTime.UtcNow; winner.LastDecision = "Falló comando"; winner.LastReason = exception.Message;
                    db.OperationalEvents.Add(new OperationalEvent { Category = "Automatización", EventType = "AUTOMATIC_IRRIGATION_FAILED", Severity = "Error", IrrigationZoneId = winner.IrrigationZoneId, IrrigationRunId = run.Id, Detail = exception.Message });
                    logger.LogError(exception, "No se pudo iniciar el riego automático {RunId}", run.Id);
                    irrigate = false;
                }
            }
            results.Add(new(winner.Id, winner.Name, winner.LastDecision, winner.LastReason, irrigate));
        }
        await db.SaveChangesAsync(ct); return new(now, results.OrderBy(x => x.Name).ToList());
    }

    private static bool InsideWindow(TimeOnly value, TimeOnly from, TimeOnly until) => from <= until ? value >= from && value <= until : value >= from || value <= until;
    private async Task<int> ActiveValveCount(CancellationToken ct)
    {
        var zones = await db.IrrigationRuns.Where(x => x.Status == "En curso" || x.Status == "Esperando ACK" || x.Status == "Cierre pendiente").Select(x => x.IrrigationZoneId).Distinct().ToListAsync(ct);
        var linked = await db.IrrigationZoneValves.Where(x => zones.Contains(x.IrrigationZoneId)).Select(x => x.DeviceId).Distinct().CountAsync(ct);
        var legacy = await db.IrrigationZones.Where(x => zones.Contains(x.Id) && x.ValveDeviceId != null && !db.IrrigationZoneValves.Any(v => v.IrrigationZoneId == x.Id)).Select(x => x.ValveDeviceId).Distinct().CountAsync(ct);
        return linked + legacy;
    }
    private async Task<int> ZoneValveCount(Guid zoneId, CancellationToken ct)
    {
        var count = await db.IrrigationZoneValves.CountAsync(x => x.IrrigationZoneId == zoneId, ct);
        return count > 0 ? count : await db.IrrigationZones.CountAsync(x => x.Id == zoneId && x.ValveDeviceId != null, ct);
    }
    private async Task<int> ParameterInt(string key, int fallback, CancellationToken ct) => int.TryParse(await db.GlobalParameters.Where(x => x.Key == key).Select(x => x.Value).SingleOrDefaultAsync(ct), out var value) ? Math.Max(1, value) : fallback;
}

public sealed class AutomationSchedulerWorker(IServiceScopeFactory scopes, ILogger<AutomationSchedulerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var interval = int.TryParse(await db.GlobalParameters.Where(x => x.Key == "AUTOMATION_INTERVAL_SECONDS").Select(x => x.Value).SingleOrDefaultAsync(stoppingToken), out var value) ? Math.Max(5, value) : 10;
                await scope.ServiceProvider.GetRequiredService<IAutomationEngine>().EvaluateAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception) { logger.LogError(exception, "Error en el ciclo del scheduler de automatización."); await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        }
    }
}
