using System.ComponentModel.DataAnnotations;

namespace SistemaRiego.Api.Contracts;

public sealed record ManualAlertRequest([Required] string Type, [Required] string Severity, [Required, MaxLength(600)] string Description, string? RelatedEntityType, string? RelatedEntityId);
public sealed record MaintenancePlanRequest([Required] string Name, [Required] string Frequency, int? IntervalDays, [Required] string EquipmentType, [Required] string EquipmentId, DateTime ScheduledAtUtc, Guid? AssignedToUserId, string? AssignedToEmail, string? Notes, string Status = "Pendiente");
public sealed record MaintenanceActivityRequest(Guid? MaintenancePlanId, [Required] string Title, [Required] string EquipmentType, [Required] string EquipmentId, DateTime ScheduledAtUtc, Guid? AssignedToUserId, string? AssignedToEmail, string? Notes, string Status = "Pendiente", DateTime? PerformedAtUtc = null);
public sealed record MaintenanceIncidentRequest([Required] string Title, [Required] string Description, [Required] string EquipmentType, [Required] string EquipmentId, string Severity, Guid? AssignedToUserId, string? AssignedToEmail, string? Notes, string Status = "Pendiente");
public sealed record MaintenanceIncidentUpdate(string Status, Guid? AssignedToUserId, string? AssignedToEmail, string? Notes);
