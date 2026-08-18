using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Services;

public interface IAlertEscalationProcessor { Task<int> EvaluateAsync(DateTime now, CancellationToken ct); }
public sealed class AlertEscalationProcessor(AppDbContext db, IAlertService alerts, IOptions<AlertOptions> options) : IAlertEscalationProcessor
{
    public async Task<int> EvaluateAsync(DateTime now, CancellationToken ct)
    {
        var escalationMinutes = int.TryParse(await db.GlobalParameters.Where(x => x.Key == "ALERT_ESCALATION_MINUTES").Select(x => x.Value).SingleOrDefaultAsync(ct), out var configuredEscalation) ? Math.Max(1, configuredEscalation) : Math.Max(1, options.Value.EscalationMinutes);
        var incidentMinutes = int.TryParse(await db.GlobalParameters.Where(x => x.Key == "MAINTENANCE_INCIDENT_MINUTES").Select(x => x.Value).SingleOrDefaultAsync(ct), out var configuredIncident) ? Math.Max(1, configuredIncident) : Math.Max(1, options.Value.IncidentMinutes);
        var threshold = now.AddMinutes(-escalationMinutes);
        var due = await db.SystemAlerts.Where(x => x.Severity == "Crítica" && x.Status == "Activa" && x.RaisedAtUtc <= threshold && (x.LastNotifiedAtUtc == null || x.LastNotifiedAtUtc <= threshold)).ToListAsync(ct);
        foreach (var alert in due)
        {
            alert.EscalationLevel++; alert.LastNotifiedAtUtc = now;
            if (alert.RaisedAtUtc <= now.AddMinutes(-incidentMinutes) && !await db.MaintenanceIncidents.AnyAsync(x => x.SystemAlertId == alert.Id, ct))
                db.MaintenanceIncidents.Add(new MaintenanceIncident { SystemAlertId = alert.Id, Title = $"Incidencia: {alert.Type}", Description = alert.Description, EquipmentType = alert.RelatedEntityType ?? "Sistema", EquipmentId = alert.RelatedEntityId ?? "GLOBAL", Severity = alert.Severity, Origin = "Automática desde alerta" });
            await db.SaveChangesAsync(ct); await alerts.NotifyAsync(alert, ct);
        }
        return due.Count;
    }
}
