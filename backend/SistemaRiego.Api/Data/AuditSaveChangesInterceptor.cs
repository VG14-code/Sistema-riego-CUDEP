using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Data;

public sealed class AuditSaveChangesInterceptor(IHttpContextAccessor httpContextAccessor) : SaveChangesInterceptor
{
    private static readonly HashSet<Type> AuditedTypes =
    [
        typeof(User), typeof(UserRole), typeof(RolePermission), typeof(MasterCatalogItem), typeof(GlobalParameter),
        typeof(IrrigationRule), typeof(IrrigationRun), typeof(IoTCommand), typeof(SystemAlert),
        typeof(MaintenancePlan), typeof(MaintenanceActivity), typeof(MaintenanceIncident),
        // Territorio y agronomia: sin estos tipos, crear o borrar una zona, un sector
        // o un cultivo no dejaba rastro y solo podia reconstruirse desde los logs.
        typeof(UniversityCenter), typeof(Farm), typeof(FarmBlock), typeof(IrrigationSector), typeof(IrrigationZone),
        typeof(SoilType), typeof(CropType), typeof(Crop), typeof(PhenologicalStage), typeof(CropWaterRequirement),
        typeof(CropCycle), typeof(CropRotationPlan),
        // Infraestructura IoT: inventario, instalacion, calibracion, firmware y
        // configuracion remota. Las lecturas (SensorReading) nunca se auditan: son
        // telemetria, no configuracion, y tienen su propia tabla y retencion.
        typeof(IoTNode), typeof(IoTDevice), typeof(IoTSensor), typeof(SensorCalibration),
        typeof(DeviceBrand), typeof(DeviceModel), typeof(DeviceInstallation),
        typeof(FirmwareHistory), typeof(RemoteConfigurationCommand)
        // IrrigationZoneSensor y IrrigationZoneValve quedan fuera a proposito:
        // SyncAssignmentsAsync borra y reinserta todas las asignaciones en cada
        // actualizacion de zona, asi que auditarlas generaria ruido sin cambio real.
        // La zona ya registra quien la modifico.
    ];

    // Marcas de latido que los procesos automaticos reescriben continuamente: la ingestion
    // de telemetria en cada mensaje MQTT y AutomationEngine al evaluar cada regla cada
    // 10 s (esas tres eran el 86 % de la bitacora). Una modificacion que solo las toca no
    // es un cambio de configuracion y no se audita; si viene acompanada de cualquier otro
    // campo, la entrada si se registra.
    private static readonly string[] HeartbeatProperties = ["LastReadingUtc", "LastCommunicationUtc", "LastEvaluatedAtUtc", "LastDecision", "LastReason"];

    private static readonly string[] SecretFragments = ["Password", "Token", "Authenticator", "RecoveryCode", "SecurityStamp", "ConcurrencyStamp"];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AddAuditEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        AddAuditEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddAuditEntries(DbContext? context)
    {
        if (context is null) return;
        context.ChangeTracker.DetectChanges();
        var http = httpContextAccessor.HttpContext;
        var actorId = Guid.TryParse(http?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : (Guid?)null;
        var actorEmail = http?.User.FindFirstValue(ClaimTypes.Email) ?? http?.User.Identity?.Name;
        var ip = http?.Connection.RemoteIpAddress?.ToString() ?? "Proceso interno";
        var correlationId = http?.TraceIdentifier ?? System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        var origin = http is null ? "Proceso automático" : "API";

        var audits = context.ChangeTracker.Entries()
            .Where(x => AuditedTypes.Contains(x.Entity.GetType()) && x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(x => x.State != EntityState.Modified || x.Properties.Any(p => p.IsModified && !HeartbeatProperties.Contains(p.Metadata.Name)))
            .Select(x => CreateEntry(x, actorId, actorEmail, ip, correlationId, origin))
            .ToList();
        if (audits.Count > 0) context.AddRange(audits);
    }

    private static AuditEntry CreateEntry(EntityEntry entry, Guid? actorId, string? actorEmail, string ip, string correlationId, string origin)
    {
        var action = entry.State switch { EntityState.Added => "Creación", EntityState.Deleted => "Eliminación", _ => "Actualización" };
        var key = string.Join(",", entry.Properties.Where(x => x.Metadata.IsPrimaryKey()).Select(x => Convert.ToString(x.CurrentValue ?? x.OriginalValue)));
        return new AuditEntry
        {
            UserId = actorId, UserEmail = actorEmail, ActionType = action, EntityType = entry.Metadata.ClrType.Name,
            EntityId = key, BeforeJson = entry.State == EntityState.Added ? null : Snapshot(entry, false),
            AfterJson = entry.State == EntityState.Deleted ? null : Snapshot(entry, true),
            Detail = $"{action} de {entry.Metadata.ClrType.Name}", IpAddress = ip,
            CorrelationId = correlationId, Origin = origin
        };
    }

    private static string Snapshot(EntityEntry entry, bool current)
    {
        var values = entry.Properties
            .Where(p => entry.State != EntityState.Modified || p.IsModified)
            .ToDictionary(p => p.Metadata.Name, p => SecretFragments.Any(s => p.Metadata.Name.Contains(s, StringComparison.OrdinalIgnoreCase)) ? "[PROTEGIDO]" : current ? p.CurrentValue : p.OriginalValue);
        return JsonSerializer.Serialize(values);
    }
}
