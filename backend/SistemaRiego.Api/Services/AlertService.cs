using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Hubs;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Services;

public sealed class AlertOptions
{
    public const string SectionName = "Alerts";
    public string WebhookUrl { get; init; } = string.Empty;
    public int EscalationMinutes { get; init; } = 15;
    public int IncidentMinutes { get; init; } = 30;
    public int EvaluationSeconds { get; init; } = 30;
}
public sealed record AlertSignal(string Fingerprint, string Type, string Severity, string Description, string? EntityType = null, string? EntityId = null, string Origin = "Condición detectada");
public interface IAlertService
{
    Task<SystemAlert> RaiseAsync(AlertSignal signal, CancellationToken ct);
    Task NotifyAsync(SystemAlert alert, CancellationToken ct);
}
public sealed class AlertService(AppDbContext db, IHubContext<TelemetryHub> hub, IHttpClientFactory clients, IOptions<AlertOptions> options, ILogger<AlertService> logger) : IAlertService
{
    public async Task<SystemAlert> RaiseAsync(AlertSignal signal, CancellationToken ct)
    {
        var existing = await db.SystemAlerts.SingleOrDefaultAsync(x => x.Fingerprint == signal.Fingerprint && (x.Status == "Activa" || x.Status == "Reconocida"), ct);
        if (existing is not null) return existing;
        var alert = new SystemAlert { Fingerprint = signal.Fingerprint, Type = signal.Type, Severity = signal.Severity, Description = signal.Description, RelatedEntityType = signal.EntityType, RelatedEntityId = signal.EntityId, Origin = signal.Origin, LastNotifiedAtUtc = DateTime.UtcNow };
        db.SystemAlerts.Add(alert); await db.SaveChangesAsync(ct);
        await hub.Clients.All.SendAsync(TelemetryHub.AlertRaised, Project(alert), ct);
        await NotifyAsync(alert, ct); return alert;
    }
    public async Task NotifyAsync(SystemAlert alert, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.Value.WebhookUrl)) return;
        var delivery = new NotificationDelivery { SystemAlertId = alert.Id, Attempt = alert.EscalationLevel + 1 }; db.NotificationDeliveries.Add(delivery);
        try
        {
            var response = await clients.CreateClient(nameof(AlertService)).PostAsJsonAsync(options.Value.WebhookUrl, Project(alert), ct);
            delivery.HttpStatusCode = (int)response.StatusCode; delivery.Status = response.IsSuccessStatusCode ? "Entregado" : "Fallido";
            if (!response.IsSuccessStatusCode) delivery.Error = $"HTTP {(int)response.StatusCode}";
        }
        catch (Exception ex) { delivery.Status = "Fallido"; delivery.Error = ex.Message[..Math.Min(ex.Message.Length, 500)]; logger.LogWarning(ex, "Falló webhook para alerta {AlertId}", alert.Id); }
        await db.SaveChangesAsync(ct);
    }
    private static object Project(SystemAlert x) => new { x.Id, x.Type, x.Severity, x.Status, x.Origin, x.Description, x.RelatedEntityType, x.RelatedEntityId, x.RaisedAtUtc, x.EscalationLevel };
}

public sealed class AlertEscalationWorker(IServiceScopeFactory scopes, IOptions<AlertOptions> options, ILogger<AlertEscalationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, options.Value.EvaluationSeconds)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await Evaluate(stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "Falló evaluación de escalamiento de alertas."); }
        }
    }
    private async Task Evaluate(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IAlertEscalationProcessor>().EvaluateAsync(DateTime.UtcNow, ct);
    }
}
