using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Services;

public sealed class IoTHealthOptions
{
    public const string SectionName = "IoTHealth";
    // Sin valor configurado manda SENSOR_OFFLINE_MINUTES. Definirlo en appsettings
    // es una anulacion explicita por entorno: Development lo baja a segundos para
    // que la deteccion sea observable en las pruebas de navegador.
    public int? OfflineAfterSeconds { get; init; }
    public int CheckIntervalSeconds { get; init; } = 5;
    public const int DefaultOfflineSeconds = 20;

    // Precedencia: anulacion explicita del entorno, luego el parametro global
    // SENSOR_OFFLINE_MINUTES, y por ultimo el valor de referencia del codigo.
    public static int ResolveOfflineSeconds(int? configuredSeconds, string? parameterMinutes)
    {
        if (configuredSeconds is int configured) return Math.Max(5, configured);
        return int.TryParse(parameterMinutes, out var minutes) && minutes > 0 ? Math.Max(5, minutes * 60) : DefaultOfflineSeconds;
    }
}

public sealed class IoTHealthWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<IoTHealthOptions> healthOptions,
    ILogger<IoTHealthWorker> logger) : BackgroundService
{
    private readonly IoTHealthOptions options = healthOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(2, options.CheckIntervalSeconds)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await UpdateStatusesAsync(stoppingToken);
    }

    private async Task UpdateStatusesAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var alerts = scope.ServiceProvider.GetRequiredService<IAlertService>();
        var statuses = await db.MasterCatalogItems
            .Where(x => x.Kind == CatalogKind.OperationalStatus && (x.Code == "ACTIVE" || x.Code == "OFFLINE"))
            .ToDictionaryAsync(x => x.Code, x => x.Id, cancellationToken);
        if (!statuses.TryGetValue("OFFLINE", out var offlineId))
            return;

        var cutoff = DateTime.UtcNow.AddSeconds(-await OfflineSecondsAsync(db, cancellationToken));
        var changed = 0;
        foreach (var node in await db.IoTNodes.Where(x => x.IsActive && (!x.LastCommunicationUtc.HasValue || x.LastCommunicationUtc < cutoff) && x.OperationalStatusId != offlineId).ToListAsync(cancellationToken))
        {
            node.OperationalStatusId = offlineId;
            node.UpdatedAtUtc = DateTime.UtcNow;
            await alerts.RaiseAsync(new AlertSignal($"IOT:NODE:{node.Id}:OFFLINE", "Conexión", "Advertencia", $"Nodo {node.Name} sin heartbeat.", "Nodo IoT", node.Id.ToString()), cancellationToken);
            changed++;
        }
        foreach (var device in await db.IoTDevices.Where(x => x.IsActive && (!x.LastCommunicationUtc.HasValue || x.LastCommunicationUtc < cutoff) && x.OperationalStatusId != offlineId).ToListAsync(cancellationToken))
        {
            device.OperationalStatusId = offlineId;
            device.UpdatedAtUtc = DateTime.UtcNow;
            await alerts.RaiseAsync(new AlertSignal($"IOT:DEVICE:{device.Id}:OFFLINE", "Conexión", "Advertencia", $"Dispositivo {device.Name} sin heartbeat.", "Dispositivo IoT", device.Id.ToString()), cancellationToken);
            changed++;
        }
        foreach (var sensor in await db.IoTSensors
            .Where(x => x.IsActive
                && x.OperationalStatusId != offlineId
                && !db.SensorReadings.Any(r => r.SensorId == x.Id && r.ReceivedAtUtc >= cutoff))
            .ToListAsync(cancellationToken))
        {
            sensor.OperationalStatusId = offlineId;
            sensor.UpdatedAtUtc = DateTime.UtcNow;
            await alerts.RaiseAsync(new AlertSignal($"IOT:SENSOR:{sensor.Id}:OFFLINE", "Conexión", "Advertencia", $"Sensor {sensor.Name} sin lecturas recientes.", "Sensor", sensor.Id.ToString()), cancellationToken);
            changed++;
        }

        if (changed > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Heartbeat IoT marcó {Count} elementos como OFFLINE (límite {Cutoff:o})", changed, cutoff);
        }
    }

    private async Task<int> OfflineSecondsAsync(AppDbContext db, CancellationToken ct)
    {
        if (options.OfflineAfterSeconds is int configured) return Math.Max(5, configured);
        var raw = await db.GlobalParameters.AsNoTracking().Where(x => x.Key == "SENSOR_OFFLINE_MINUTES").Select(x => x.Value).SingleOrDefaultAsync(ct);
        return IoTHealthOptions.ResolveOfflineSeconds(null, raw);
    }
}
