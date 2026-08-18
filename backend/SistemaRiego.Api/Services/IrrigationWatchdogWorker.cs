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

    private async Task Inspect(CancellationToken ct)
    {
        using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var alerts = scope.ServiceProvider.GetRequiredService<IAlertService>(); var now = DateTime.UtcNow;
        var expired = await db.IoTCommands.Where(x => (x.Status == "Pendiente" || x.Status == "Publicado") && x.ExpiresAtUtc <= now).ToListAsync(ct);
        foreach (var command in expired)
        {
            command.Status = "Expirado"; command.FailedAtUtc = now; command.FailureReason = "No se recibió ACK MQTT antes del timeout.";
            await alerts.RaiseAsync(new AlertSignal($"MQTT:{command.Id}:TIMEOUT", "Falla de dispositivo", "Crítica", $"Comando {command.CommandType} {command.Id} expiró sin ACK.", "Dispositivo IoT", command.DeviceId.ToString()), ct);
            if (command.IrrigationRunId is long id)
            {
                var run = await db.IrrigationRuns.FindAsync([id], ct);
                if (run is not null && run.Status != "Detenido") { run.Status = "Fallido"; run.EndedAtUtc = now; }
                db.OperationalEvents.Add(new OperationalEvent { Category = "Seguridad", EventType = "MQTT_ACK_TIMEOUT", Severity = "Error", IrrigationZoneId = command.IrrigationZoneId, IrrigationRunId = id, Detail = $"Comando {command.CommandType} {command.Id} expiró sin ACK." });
            }
        }
        await db.SaveChangesAsync(ct);

        var due = await db.IrrigationRuns.Where(x => x.Status == "En curso" && x.StartedAtUtc != null).ToListAsync(ct);
        var service = scope.ServiceProvider.GetRequiredService<IIrrigationCommandService>();
        foreach (var run in due.Where(x => x.StartedAtUtc!.Value.AddMinutes(x.PlannedDurationMinutes) <= now))
        {
            run.Status = "Cierre pendiente"; await db.SaveChangesAsync(ct);
            try
            {
                await service.SendAsync(run.IrrigationZoneId, run, "CERRAR_VALVULA", null, ct);
                db.OperationalEvents.Add(new OperationalEvent { Category = "Automatización", EventType = "IRRIGATION_EXPIRATION_CLOSE_REQUESTED", IrrigationZoneId = run.IrrigationZoneId, IrrigationRunId = run.Id, Detail = "Duración máxima alcanzada; cierre MQTT solicitado." });
            }
            catch (Exception exception)
            {
                run.Status = "Fallido"; run.EndedAtUtc = now;
                db.OperationalEvents.Add(new OperationalEvent { Category = "Seguridad", EventType = "IRRIGATION_CLOSE_FAILED", Severity = "Crítico", IrrigationZoneId = run.IrrigationZoneId, IrrigationRunId = run.Id, Detail = exception.Message });
                await alerts.RaiseAsync(new AlertSignal($"IRRIGATION:{run.Id}:CLOSE_FAILED", "Falla de dispositivo", "Crítica", $"No fue posible cerrar el riego {run.Id}: {exception.Message}", "Zona", run.IrrigationZoneId.ToString()), ct);
            }
            await db.SaveChangesAsync(ct);
        }
    }
}
