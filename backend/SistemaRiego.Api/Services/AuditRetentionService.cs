using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Data;

namespace SistemaRiego.Api.Services;

public sealed class AuditRetentionService(IServiceScopeFactory scopeFactory, ILogger<AuditRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        do { await PurgeAsync(stoppingToken); } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task<int> PurgeAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var configured = await db.GlobalParameters.AsNoTracking().Where(x => x.Key == "AUDIT_RETENTION_DAYS").Select(x => x.Value).SingleOrDefaultAsync(ct);
        var days = int.TryParse(configured, out var parsed) ? Math.Clamp(parsed, 30, 3650) : 730;
        var count = await db.AuditEntries.Where(x => x.OccurredAtUtc < DateTime.UtcNow.AddDays(-days)).ExecuteDeleteAsync(ct);
        if (count > 0) logger.LogInformation("Se eliminaron {Count} registros de auditoría vencidos", count);
        return count;
    }
}
