using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Services;

public sealed class IrrigationWatchdogWorker(IServiceScopeFactory scopes, ILogger<IrrigationWatchdogWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Inspect(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception) { logger.LogError(exception, "Falló la inspección del watchdog de riego."); }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    internal async Task Inspect(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var alerts = scope.ServiceProvider.GetRequiredService<IAlertService>();
        var now = DateTime.UtcNow;
        var maximumAttempts = await ParameterInt(db, "AUTOMATION_MAX_COMMAND_ATTEMPTS", 3, ct);
        var expired = await db.IoTCommands.Include(x => x.Device).Where(x => (x.Status == "Pendiente" || x.Status == "Publicado") && x.ExpiresAtUtc <= now).ToListAsync(ct);
        foreach (var command in expired)
        {
            command.Status = "Expirado";
            command.FailedAtUtc = now;
            command.FailureReason = "No se recibió ACK MQTT antes del timeout.";
            await alerts.RaiseAsync(new AlertSignal($"MQTT:{command.Id}:TIMEOUT", "Falla de dispositivo", "Crítica", $"El dispositivo {command.Device.Name} no respondió al comando {command.CommandType} dentro del tiempo esperado.\nDetalle técnico: Comando {command.CommandType} {command.Id} expiró sin ACK.", "Dispositivo IoT", command.DeviceId.ToString()), ct);
            if (command.IrrigationRunId is long id)
            {
                var run = await db.IrrigationRuns.Include(x => x.IrrigationRule).SingleOrDefaultAsync(x => x.Id == id, ct);
                if (run is not null && run.Status != "Detenido")
                {
                    run.Status = "Fallido";
                    run.EndedAtUtc = now;
                    if (command.CommandType == "ABRIR_VALVULA" && run.Mode == "Automático" && run.IrrigationRule is not null)
                        await SuspendRuleAfterRepeatedFailures(db, run, command.Device.Name, maximumAttempts, now, ct);
                }
                db.OperationalEvents.Add(new OperationalEvent { Category = "Seguridad", EventType = "MQTT_ACK_TIMEOUT", Severity = "Error", IrrigationZoneId = command.IrrigationZoneId, IrrigationRunId = id, Detail = $"Comando {command.CommandType} {command.Id} expiró sin ACK." });
            }
        }
        await db.SaveChangesAsync(ct);

        var due = await db.IrrigationRuns.Where(x => x.Status == "En curso" && x.StartedAtUtc != null).ToListAsync(ct);
        var service = scope.ServiceProvider.GetRequiredService<IIrrigationCommandService>();
        foreach (var run in due.Where(x => x.StartedAtUtc!.Value.AddMinutes(x.PlannedDurationMinutes) <= now))
        {
            run.Status = "Cierre pendiente";
            await db.SaveChangesAsync(ct);
            try
            {
                await service.SendAsync(run.IrrigationZoneId, run, "CERRAR_VALVULA", null, ct);
                db.OperationalEvents.Add(new OperationalEvent { Category = "Automatización", EventType = "IRRIGATION_EXPIRATION_CLOSE_REQUESTED", IrrigationZoneId = run.IrrigationZoneId, IrrigationRunId = run.Id, Detail = "Duración máxima alcanzada; cierre MQTT solicitado." });
            }
            catch (Exception exception)
            {
                run.Status = "Fallido";
                run.EndedAtUtc = now;
                db.OperationalEvents.Add(new OperationalEvent { Category = "Seguridad", EventType = "IRRIGATION_CLOSE_FAILED", Severity = "Crítico", IrrigationZoneId = run.IrrigationZoneId, IrrigationRunId = run.Id, Detail = exception.Message });
                await alerts.RaiseAsync(new AlertSignal($"IRRIGATION:{run.Id}:CLOSE_FAILED", "Falla de dispositivo", "Crítica", $"No fue posible cerrar el riego {run.Id}: {exception.Message}", "Zona", run.IrrigationZoneId.ToString()), ct);
            }
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task SuspendRuleAfterRepeatedFailures(AppDbContext db, IrrigationRun currentRun, string deviceName, int maximumAttempts, DateTime now, CancellationToken ct)
    {
        var rule = currentRun.IrrigationRule!;
        if (rule.SuspendedUntilUtc == DateTime.MaxValue) return;

        var ruleRunIds = await db.IrrigationRuns
            .Where(x => x.IrrigationRuleId == rule.Id && x.Mode == "Automático")
            .Select(x => x.Id)
            .ToListAsync(ct);
        var lastConfirmedAt = await db.IoTCommands
            .Where(x => x.CommandType == "ABRIR_VALVULA" && x.Status == "Confirmado" && x.IrrigationRunId.HasValue && ruleRunIds.Contains(x.IrrigationRunId.Value))
            .OrderByDescending(x => x.RequestedAtUtc)
            .Select(x => (DateTime?)x.RequestedAtUtc)
            .FirstOrDefaultAsync(ct);
        var previousFailures = await db.IoTCommands.CountAsync(x =>
            x.CommandType == "ABRIR_VALVULA" &&
            x.Status == "Expirado" &&
            x.IrrigationRunId.HasValue &&
            ruleRunIds.Contains(x.IrrigationRunId.Value) &&
            (!lastConfirmedAt.HasValue || x.RequestedAtUtc > lastConfirmedAt.Value), ct);
        var attempts = previousFailures + 1;
        if (attempts < maximumAttempts) return;

        rule.SuspendedUntilUtc = DateTime.MaxValue;
        rule.LastEvaluatedAtUtc = now;
        rule.LastDecision = "Suspendida por fallos MQTT";
        rule.LastReason = $"{attempts} intentos automáticos consecutivos sin ACK de {deviceName}. Requiere reconexión confirmada o reactivación manual.";
        db.OperationalEvents.Add(new OperationalEvent
        {
            Category = "Automatización",
            EventType = "AUTOMATION_RULE_SUSPENDED_MQTT_FAILURES",
            Severity = "Crítico",
            IrrigationZoneId = currentRun.IrrigationZoneId,
            IrrigationRunId = currentRun.Id,
            Detail = rule.LastReason
        });
    }

    private static async Task<int> ParameterInt(AppDbContext db, string key, int fallback, CancellationToken ct) =>
        int.TryParse(await db.GlobalParameters.Where(x => x.Key == key).Select(x => x.Value).SingleOrDefaultAsync(ct), out var value)
            ? Math.Clamp(value, 1, 20)
            : fallback;
}
