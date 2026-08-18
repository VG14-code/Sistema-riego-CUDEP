namespace SistemaRiego.Api.Models;

public sealed class SystemAlert
{
    public long Id { get; set; }
    public required string Fingerprint { get; set; }
    public required string Type { get; set; }
    public string Severity { get; set; } = "Advertencia";
    public string Status { get; set; } = "Activa";
    public string Origin { get; set; } = "Condición detectada";
    public required string Description { get; set; }
    public string? RelatedEntityType { get; set; }
    public string? RelatedEntityId { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? RelatedEntityName { get; set; }
    public DateTime RaisedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? AcknowledgedByUserId { get; set; }
    public string? AcknowledgedByEmail { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public int EscalationLevel { get; set; }
    public DateTime? LastNotifiedAtUtc { get; set; }
    public ICollection<NotificationDelivery> Deliveries { get; set; } = [];
}

public sealed class NotificationDelivery
{
    public long Id { get; set; }
    public long SystemAlertId { get; set; }
    public SystemAlert SystemAlert { get; set; } = null!;
    public string Channel { get; set; } = "Webhook";
    public string Status { get; set; } = "Pendiente";
    public int Attempt { get; set; } = 1;
    public DateTime AttemptedAtUtc { get; set; } = DateTime.UtcNow;
    public int? HttpStatusCode { get; set; }
    public string? Error { get; set; }
}

public sealed class MaintenancePlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string Frequency { get; set; } = "Único";
    public int? IntervalDays { get; set; }
    public required string EquipmentType { get; set; }
    public required string EquipmentId { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public DateTime? LastPerformedAtUtc { get; set; }
    public DateTime? NextDueAtUtc { get; set; }
    public string Status { get; set; } = "Pendiente";
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToEmail { get; set; }
    public string? Notes { get; set; }
}

public sealed class MaintenanceActivity
{
    public long Id { get; set; }
    public Guid? MaintenancePlanId { get; set; }
    public MaintenancePlan? MaintenancePlan { get; set; }
    public required string Title { get; set; }
    public required string EquipmentType { get; set; }
    public required string EquipmentId { get; set; }
    public string Status { get; set; } = "Pendiente";
    public DateTime ScheduledAtUtc { get; set; }
    public DateTime? PerformedAtUtc { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToEmail { get; set; }
    public string? Notes { get; set; }
}

public sealed class MaintenanceIncident
{
    public long Id { get; set; }
    public long? SystemAlertId { get; set; }
    public SystemAlert? SystemAlert { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string EquipmentType { get; set; }
    public required string EquipmentId { get; set; }
    public string Severity { get; set; } = "Advertencia";
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? EquipmentName { get; set; }
    public string Status { get; set; } = "Pendiente";
    public string Origin { get; set; } = "Manual";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToEmail { get; set; }
    public string? Notes { get; set; }
}
