namespace SistemaRiego.Api.Models;

public sealed class DeviceInstallation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public IoTDevice Device { get; set; } = null!;
    public Guid? IrrigationZoneId { get; set; }
    public IrrigationZone? IrrigationZone { get; set; }
    public string Location { get; set; } = string.Empty;
    public DateTime InstalledAtUtc { get; set; }
    public Guid? InstalledByUserId { get; set; }
    public string InstallerName { get; set; } = string.Empty;
    public DateTime? RemovedAtUtc { get; set; }
    public string? Notes { get; set; }
}

public sealed class RemoteConfigurationCommand
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NodeId { get; set; }
    public IoTNode Node { get; set; } = null!;
    public string CommandType { get; set; } = string.Empty;
    public string Payload { get; set; } = "{}";
    public string Status { get; set; } = "Pendiente";
    public int Attempts { get; set; }
    public int MaximumAttempts { get; set; } = 3;
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public string? LastError { get; set; }
    public Guid? RequestedByUserId { get; set; }
}

public sealed class FirmwareHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NodeId { get; set; }
    public IoTNode Node { get; set; } = null!;
    public string Version { get; set; } = string.Empty;
    public string? PreviousVersion { get; set; }
    public string Status { get; set; } = "Registrada";
    public DateTime RegisteredAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AppliedAtUtc { get; set; }
    public string? Notes { get; set; }
}

public sealed class InventoryMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public IoTDevice Device { get; set; } = null!;
    public string MovementType { get; set; } = "Actualización";
    public string? PreviousStatus { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public string? PreviousOwner { get; set; }
    public string? NewOwner { get; set; }
    public string? Notes { get; set; }
    public Guid? PerformedByUserId { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class CropRotationPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IrrigationZoneId { get; set; }
    public IrrigationZone IrrigationZone { get; set; } = null!;
    public Guid CropId { get; set; }
    public Crop Crop { get; set; } = null!;
    public Guid? PreviousCycleId { get; set; }
    public CropCycle? PreviousCycle { get; set; }
    public DateOnly PlannedStartDate { get; set; }
    public DateOnly PlannedEndDate { get; set; }
    public string Status { get; set; } = "Planificada";
    public string? CompatibilityNotes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

