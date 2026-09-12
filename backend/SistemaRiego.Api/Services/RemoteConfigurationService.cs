using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Services;

public interface IRemoteConfigurationDispatcher
{
    Task DispatchAsync(Guid commandId, CancellationToken ct);
    Task<bool> AcknowledgeAsync(Guid commandId, string nodeCode, bool success, string? detail, CancellationToken ct);
}

public sealed class RemoteConfigurationDispatcher(
    AppDbContext db,
    IMqttCommandPublisher mqtt,
    ILogger<RemoteConfigurationDispatcher> logger) : IRemoteConfigurationDispatcher
{
    public async Task DispatchAsync(Guid commandId, CancellationToken ct)
    {
        var item = await db.RemoteConfigurationCommands.Include(x => x.Node).SingleOrDefaultAsync(x => x.Id == commandId, ct);
        if (item is null || item.Status is "Confirmada" or "Fallida") return;
        if (item.Attempts >= item.MaximumAttempts)
        {
            item.Status = "Fallida";
            item.LastError ??= "Se agotó el máximo de intentos sin recibir ACK MQTT.";
            await db.SaveChangesAsync(ct);
            return;
        }

        item.Attempts++;
        item.LastAttemptAtUtc = DateTime.UtcNow;
        item.Status = "Enviada";
        item.LastError = null;
        await db.SaveChangesAsync(ct);
        try
        {
            using var payload = JsonDocument.Parse(item.Payload);
            await mqtt.PublishRemoteConfigurationAsync(item.Node.Code, item.Id, item.CommandType, payload.RootElement.Clone(), ct);
        }
        catch (MqttBrokerUnavailableException exception)
        {
            // El comando no llego a salir: se devuelve el intento y queda en cola. LastAttemptAtUtc
            // se conserva para que el worker espere su ventana de reintento y no insista cada 5 s.
            item.Attempts--;
            item.Status = "Pendiente";
            item.LastError = "Broker MQTT no disponible; el intento no se contabilizó.";
            await db.SaveChangesAsync(ct);
            logger.LogWarning("Configuración remota {CommandId} en espera: {Reason}", item.Id, exception.Message);
        }
        catch (Exception exception)
        {
            item.Status = item.Attempts >= item.MaximumAttempts ? "Fallida" : "Pendiente";
            item.LastError = exception.Message.Length > 500 ? exception.Message[..500] : exception.Message;
            await db.SaveChangesAsync(ct);
            logger.LogWarning(exception, "Falló el intento {Attempt}/{Maximum} de configuración remota {CommandId}", item.Attempts, item.MaximumAttempts, item.Id);
        }
    }

    public async Task<bool> AcknowledgeAsync(Guid commandId, string nodeCode, bool success, string? detail, CancellationToken ct)
    {
        var item = await db.RemoteConfigurationCommands.Include(x => x.Node).SingleOrDefaultAsync(x => x.Id == commandId, ct);
        if (item is null || !string.Equals(item.Node.Code, nodeCode, StringComparison.OrdinalIgnoreCase)) return false;
        item.LastError = success ? null : detail;
        item.Status = success ? "Confirmada" : item.Attempts >= item.MaximumAttempts ? "Fallida" : "Pendiente";
        if (success) item.ConfirmedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }
}

public sealed class RemoteConfigurationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<RemoteConfigurationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var retryBefore = DateTime.UtcNow.AddSeconds(-10);
                var ids = await db.RemoteConfigurationCommands.AsNoTracking()
                    .Where(x => (x.Status == "Pendiente" || x.Status == "Enviada") && (!x.LastAttemptAtUtc.HasValue || x.LastAttemptAtUtc <= retryBefore))
                    .OrderBy(x => x.RequestedAtUtc).Select(x => x.Id).Take(20).ToListAsync(stoppingToken);
                var dispatcher = scope.ServiceProvider.GetRequiredService<IRemoteConfigurationDispatcher>();
                foreach (var id in ids) await dispatcher.DispatchAsync(id, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception) { logger.LogError(exception, "Falló el procesamiento de la cola de configuración remota."); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
