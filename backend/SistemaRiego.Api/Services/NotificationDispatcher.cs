using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Services;

public interface INotificationDispatcher
{
    Task DispatchAlertAsync(SystemAlert alert, CancellationToken ct);
    Task DispatchSummaryAsync(string period, DateTime fromUtc, string operationKey, CancellationToken ct);
}

public sealed class NotificationDispatcher(AppDbContext db, IHttpClientFactory clients, IEmailSender email, IOptions<AlertOptions> alertOptions, ILogger<NotificationDispatcher> logger) : INotificationDispatcher
{
    public async Task DispatchAlertAsync(SystemAlert alert, CancellationToken ct)
    {
        var rules = await db.NotificationRules.AsNoTracking().Where(x => x.IsActive && (x.AlertType == "Todos" || x.AlertType == alert.Type)).ToListAsync(ct);
        var level = Severity(alert.Severity); rules = rules.Where(x => level >= Severity(x.MinimumSeverity)).ToList();
        if (rules.Count == 0 && !string.IsNullOrWhiteSpace(alertOptions.Value.WebhookUrl))
        {
            await SendWebhook(alert, "Webhook", alertOptions.Value.WebhookUrl, $"[{alert.Severity}] {alert.Type}: {alert.Description}", ct);
            await db.SaveChangesAsync(ct); return;
        }
        foreach (var rule in rules)
            foreach (var channel in Channels(rule.Channels))
                await SendAlertChannel(rule, alert, channel, ct);
        alert.LastNotifiedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task DispatchSummaryAsync(string period, DateTime fromUtc, string operationKey, CancellationToken ct)
    {
        var weekly = period.Equals("weekly", StringComparison.OrdinalIgnoreCase);
        var rules = await db.NotificationRules.AsNoTracking().Where(x => x.IsActive && (weekly ? x.WeeklySummary : x.DailySummary)).ToListAsync(ct);
        if (rules.Count == 0) return;
        var alerts = await db.SystemAlerts.AsNoTracking().Where(x => x.RaisedAtUtc >= fromUtc).OrderByDescending(x => x.RaisedAtUtc).ToListAsync(ct);
        var label = weekly ? "semanal" : "diario";
        var body = $"Resumen {label}: {alerts.Count} alertas; {alerts.Count(x => x.Status == "Activa")} activas; {alerts.Count(x => Severity(x.Severity) >= 2)} críticas. " +
                   string.Join(" · ", alerts.GroupBy(x => x.Type).OrderByDescending(x => x.Count()).Take(8).Select(x => $"{x.Key}: {x.Count()}"));
        foreach (var rule in rules)
            foreach (var channel in Channels(rule.Channels))
                await SendSummaryChannel(rule, channel, body, operationKey, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task SendAlertChannel(NotificationRule rule, SystemAlert alert, string channel, CancellationToken ct)
    {
        if (IsEmail(channel))
        {
            foreach (var recipient in Recipients(rule.Recipients))
            {
                var delivery = Delivery(alert, "Correo"); db.NotificationDeliveries.Add(delivery);
                try { await email.SendNotificationAsync(recipient, $"[{alert.Severity}] {alert.Type}", alert.Description, ct); delivery.Status = "Entregado"; }
                catch (Exception ex) { delivery.Status = "Fallido"; delivery.Error = Limit(ex.Message); logger.LogWarning(ex, "Falló correo de alerta {AlertId} a {Recipient}", alert.Id, recipient); }
            }
            return;
        }
        var url = await ChannelUrl(channel, ct); if (url is null) { var missing = Delivery(alert, channel); missing.Status = "Fallido"; missing.Error = $"Falta configurar URL para {channel}."; db.Add(missing); return; }
        await SendWebhook(alert, channel, url, $"[{alert.Severity}] {alert.Type}: {alert.Description}", ct);
    }

    private async Task SendSummaryChannel(NotificationRule rule, string channel, string body, string operationKey, CancellationToken ct)
    {
        var execution = new IntegrationExecution { Integration = channel, Operation = operationKey };
        db.IntegrationExecutions.Add(execution);
        try
        {
            if (IsEmail(channel))
            {
                foreach (var recipient in Recipients(rule.Recipients)) await email.SendNotificationAsync(recipient, operationKey, body, ct);
                execution.Status = "Exitoso"; execution.Detail = $"{Recipients(rule.Recipients).Count} destinatario(s).";
            }
            else
            {
                var url = await ChannelUrl(channel, ct) ?? throw new InvalidOperationException($"Falta configurar URL para {channel}.");
                using var response = await clients.CreateClient(nameof(NotificationDispatcher)).PostAsJsonAsync(url, new { source = "SistemaRiego", eventType = "ALERT_SUMMARY", period = operationKey, text = body, sentAtUtc = DateTime.UtcNow }, ct);
                execution.HttpStatusCode = (int)response.StatusCode; execution.Status = response.IsSuccessStatusCode ? "Exitoso" : "Fallido"; execution.Detail = response.ReasonPhrase;
            }
        }
        catch (Exception ex) { execution.Status = "Fallido"; execution.Detail = Limit(ex.Message); logger.LogWarning(ex, "Falló {Channel} para {Operation}", channel, operationKey); }
    }

    private async Task SendWebhook(SystemAlert alert, string channel, string url, string text, CancellationToken ct)
    {
        var delivery = Delivery(alert, channel); db.NotificationDeliveries.Add(delivery);
        var history = new IntegrationExecution { Integration = channel, Operation = $"Alerta {alert.Id}" }; db.IntegrationExecutions.Add(history);
        try
        {
            using var response = await clients.CreateClient(nameof(NotificationDispatcher)).PostAsJsonAsync(url, new { source = "SistemaRiego", eventType = "ALERT", text, alert = new { alert.Id, alert.Type, alert.Severity, alert.Status, alert.Origin, alert.Description, alert.RelatedEntityType, alert.RelatedEntityId, alert.RaisedAtUtc, alert.EscalationLevel } }, ct);
            delivery.HttpStatusCode = history.HttpStatusCode = (int)response.StatusCode; delivery.Status = response.IsSuccessStatusCode ? "Entregado" : "Fallido"; history.Status = response.IsSuccessStatusCode ? "Exitoso" : "Fallido"; history.Detail = response.ReasonPhrase; if (!response.IsSuccessStatusCode) delivery.Error = $"HTTP {(int)response.StatusCode}";
        }
        catch (Exception ex) { delivery.Status = history.Status = "Fallido"; delivery.Error = history.Detail = Limit(ex.Message); logger.LogWarning(ex, "Falló {Channel} para alerta {AlertId}", channel, alert.Id); }
    }

    private async Task<string?> ChannelUrl(string channel, CancellationToken ct)
    {
        var key = channel.Equals("n8n", StringComparison.OrdinalIgnoreCase) ? "N8N_WEBHOOK_URL" : channel.Equals("Telegram", StringComparison.OrdinalIgnoreCase) ? "TELEGRAM_WEBHOOK_URL" : channel.Equals("Teams", StringComparison.OrdinalIgnoreCase) ? "TEAMS_WEBHOOK_URL" : channel.Equals("Webhook", StringComparison.OrdinalIgnoreCase) ? null : null;
        var value = key is null ? alertOptions.Value.WebhookUrl : await db.GlobalParameters.Where(x => x.Key == key).Select(x => x.Value).SingleOrDefaultAsync(ct);
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) ? uri.ToString() : null;
    }
    private static NotificationDelivery Delivery(SystemAlert alert, string channel) => new() { SystemAlertId = alert.Id, Channel = channel, Attempt = alert.EscalationLevel + 1 };
    private static List<string> Channels(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    private static List<string> Recipients(string value) => value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    private static bool IsEmail(string channel) => channel.Equals("Correo", StringComparison.OrdinalIgnoreCase) || channel.Equals("Email", StringComparison.OrdinalIgnoreCase);
    private static int Severity(string value) => value.Equals("Crítica", StringComparison.OrdinalIgnoreCase) || value.Equals("Critica", StringComparison.OrdinalIgnoreCase) ? 2 : value.Equals("Advertencia", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    private static string Limit(string value) => value[..Math.Min(value.Length, 500)];
}

public sealed class NotificationSummaryWorker(IServiceScopeFactory scopes, ILogger<NotificationSummaryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
                var zoneName = await Parameter(db, "SYSTEM_TIMEZONE", "America/Guatemala", stoppingToken); TimeZoneInfo zone; try { zone = TimeZoneInfo.FindSystemTimeZoneById(zoneName); } catch { zone = TimeZoneInfo.Utc; }
                var nowUtc = DateTime.UtcNow; var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, zone); var dailyHour = int.TryParse(await Parameter(db, "NOTIFICATION_DAILY_HOUR", "7", stoppingToken), out var hour) ? Math.Clamp(hour, 0, 23) : 7;
                var dailyKey = $"Resumen diario {local:yyyy-MM-dd}";
                if (local.Hour == dailyHour && !await db.IntegrationExecutions.AnyAsync(x => x.Operation == dailyKey, stoppingToken)) await dispatcher.DispatchSummaryAsync("daily", nowUtc.AddDays(-1), dailyKey, stoppingToken);
                var weeklyKey = $"Resumen semanal {local:yyyy-MM-dd}";
                if (local.DayOfWeek == DayOfWeek.Monday && local.Hour == dailyHour && !await db.IntegrationExecutions.AnyAsync(x => x.Operation == weeklyKey, stoppingToken)) await dispatcher.DispatchSummaryAsync("weekly", nowUtc.AddDays(-7), weeklyKey, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Falló la generación automática de resúmenes de alertas."); }
        }
    }
    private static async Task<string> Parameter(AppDbContext db, string key, string fallback, CancellationToken ct) => await db.GlobalParameters.Where(x => x.Key == key).Select(x => x.Value).SingleOrDefaultAsync(ct) ?? fallback;
}
