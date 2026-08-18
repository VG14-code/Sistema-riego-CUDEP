namespace SistemaRiego.Api.Models;

public sealed class IrrigationRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IrrigationZoneId { get; set; }
    public IrrigationZone IrrigationZone { get; set; } = null!;
    public Guid? CropWaterRequirementId { get; set; }
    public CropWaterRequirement? CropWaterRequirement { get; set; }
    public required string Name { get; set; }
    public decimal MinimumMoisturePercent { get; set; }
    public decimal TargetMoisturePercent { get; set; }
    public decimal HysteresisPercent { get; set; } = 2;
    public int Priority { get; set; } = 1;
    public int MaximumDurationMinutes { get; set; } = 30;
    public TimeOnly AllowedFrom { get; set; } = new(5, 0);
    public TimeOnly AllowedUntil { get; set; } = new(8, 0);
    public string AllowedDays { get; set; } = "1,2,3,4,5,6,7";
    public bool IsEnabled { get; set; } = true;
    public bool RequiresSufficientEnergy { get; set; } = true;
    public DateTime? SuspendedUntilUtc { get; set; }
    public DateTime? LastEvaluatedAtUtc { get; set; }
    public string LastDecision { get; set; } = "Pendiente";
    public string? LastReason { get; set; }
}

public sealed class WaterTank
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public decimal CapacityLiters { get; set; }
    public decimal CurrentLevelLiters { get; set; }
    public decimal MinimumSafePercent { get; set; } = 15;
    public decimal MaximumFillPercent { get; set; } = 95;
    public string Status { get; set; } = "Disponible";
    public DateTime LastLevelReadingUtc { get; set; } = DateTime.UtcNow;
    public ICollection<WaterPump> Pumps { get; set; } = [];
}

public sealed class WaterPump
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WaterTankId { get; set; }
    public string Code { get; set; } = "BOMBA-ABAST-01";
    public Guid? IoTDeviceId { get; set; }
    public IoTDevice? IoTDevice { get; set; }
    public WaterTank WaterTank { get; set; } = null!;
    public required string Name { get; set; }
    public string Status { get; set; } = "Detenida";
    public bool IsRunning { get; set; }
    public int MaximumRunMinutes { get; set; } = 45;
    public int MinimumRestMinutes { get; set; } = 10;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? LastStoppedAtUtc { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public string? FailureReason { get; set; }
    public bool HasUnacknowledgedFault { get; set; }
    public decimal RatedFlowLitersMinute { get; set; } = 36;
    public decimal NominalValveFlowLitersMinute { get; set; } = 12;
    public decimal MinimumPressureBar { get; set; } = 1.2m;
    public decimal MaximumCurrentAmps { get; set; } = 12;
    public decimal LastPressureBar { get; set; }
    public decimal LastMotorCurrentAmps { get; set; }
    public DateTime? LastTelemetryAtUtc { get; set; }
}

public sealed class WaterSupplyEvent
{
    public long Id { get; set; }
    public Guid WaterPumpId { get; set; }
    public WaterPump WaterPump { get; set; } = null!;
    public string EventType { get; set; } = "Abastecimiento";
    public string Status { get; set; } = "En curso";
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
    public decimal InitialLevelLiters { get; set; }
    public decimal? FinalLevelLiters { get; set; }
    public decimal SuppliedLiters { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public string? Detail { get; set; }
}

public sealed class IrrigationRun
{
    public long Id { get; set; }
    public Guid IrrigationZoneId { get; set; }
    public IrrigationZone IrrigationZone { get; set; } = null!;
    public Guid? IrrigationRuleId { get; set; }
    public IrrigationRule? IrrigationRule { get; set; }
    public string Mode { get; set; } = "Manual";
    public string Status { get; set; } = "En curso";
    public int PlannedDurationMinutes { get; set; }
    public decimal FlowRateLitersMinute { get; set; }
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public string? RequestedByEmail { get; set; }
    public required string Reason { get; set; }
    public string? Observations { get; set; }
}

public sealed class ValveRuntimeState
{
    public Guid DeviceId { get; set; } public IoTDevice Device { get; set; } = null!; public Guid? IrrigationZoneId { get; set; }
    public string State { get; set; } = "Desconocido"; public bool IsOpen { get; set; } public Guid? LastCommandId { get; set; }
    public DateTime? LastAckAtUtc { get; set; } public DateTime? LastReconciledAtUtc { get; set; }
}

public sealed class WaterConsumptionRecord
{
    public long Id { get; set; }
    public long IrrigationRunId { get; set; }
    public IrrigationRun IrrigationRun { get; set; } = null!;
    public Guid IrrigationZoneId { get; set; }
    public IrrigationZone IrrigationZone { get; set; } = null!;
    public string Source { get; set; } = "Estimado";
    public bool IsMeasured { get; set; }
    public decimal FlowRateLitersMinute { get; set; }
    public decimal DurationMinutes { get; set; }
    public decimal VolumeLiters { get; set; }
    public decimal? RecommendedVolumeLiters { get; set; }
    public decimal? DeviationPercent { get; set; }
    public decimal EstimatedCost { get; set; }
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class OperationalEvent
{
    public long Id { get; set; }
    public string Category { get; set; } = "Riego";
    public required string EventType { get; set; }
    public string Severity { get; set; } = "Informativo";
    public Guid? IrrigationZoneId { get; set; }
    public IrrigationZone? IrrigationZone { get; set; }
    public long? IrrigationRunId { get; set; }
    public IrrigationRun? IrrigationRun { get; set; }
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
    public required string Detail { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
