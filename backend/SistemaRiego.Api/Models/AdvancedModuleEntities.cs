namespace SistemaRiego.Api.Models;

public sealed class HydraulicConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IrrigationZoneId { get; set; }
    public IrrigationZone IrrigationZone { get; set; } = null!;
    public Guid? WaterSourceId { get; set; }
    public WaterSource? WaterSource { get; set; }
    public Guid? WaterTankId { get; set; }
    public WaterTank? WaterTank { get; set; }
    public Guid? WaterPumpId { get; set; }
    public WaterPump? WaterPump { get; set; }
    public decimal PipeDiameterMillimeters { get; set; }
    public decimal PipeLengthMeters { get; set; }
    public decimal DesignFlowLitersMinute { get; set; }
    public decimal MinimumPressureBar { get; set; }
    public decimal MaximumPressureBar { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class IrrigationRuleEvaluation
{
    public long Id { get; set; }
    public Guid IrrigationRuleId { get; set; }
    public IrrigationRule IrrigationRule { get; set; } = null!;
    public DateTime EvaluatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsSimulation { get; set; }
    public decimal? MoisturePercent { get; set; }
    public required string Decision { get; set; }
    public string? Reason { get; set; }
    public bool StartedIrrigation { get; set; }
}

public sealed class IrrigationRuleVersion
{
    public long Id { get; set; }
    public Guid IrrigationRuleId { get; set; }
    public IrrigationRule IrrigationRule { get; set; } = null!;
    public int Version { get; set; }
    public required string SnapshotJson { get; set; }
    public string? ChangeReason { get; set; }
    public string? ChangedByEmail { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class IrrigationSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IrrigationZoneId { get; set; }
    public IrrigationZone IrrigationZone { get; set; } = null!;
    public required string Name { get; set; }
    public DateTime NextRunAtUtc { get; set; }
    public string Recurrence { get; set; } = "Único";
    public int? IntervalDays { get; set; }
    public int DurationMinutes { get; set; }
    public decimal FlowRateLitersMinute { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastRunAtUtc { get; set; }
    public string? LastResult { get; set; }
    public string? CreatedByEmail { get; set; }
}

public sealed class WaterSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string Type { get; set; } = "Pozo";
    public decimal MaximumFlowLitersMinute { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}

public sealed class AutomaticFillConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WaterTankId { get; set; }
    public WaterTank WaterTank { get; set; } = null!;
    public Guid WaterPumpId { get; set; }
    public WaterPump WaterPump { get; set; } = null!;
    public Guid WaterSourceId { get; set; }
    public WaterSource WaterSource { get; set; } = null!;
    public decimal StartAtPercent { get; set; } = 25;
    public decimal StopAtPercent { get; set; } = 90;
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastEvaluatedAtUtc { get; set; }
    public string? LastDecision { get; set; }
}

public sealed class WaterEfficiencyBaseline
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? IrrigationZoneId { get; set; }
    public IrrigationZone? IrrigationZone { get; set; }
    public string Name { get; set; } = "Línea base";
    public decimal LitersPerEvent { get; set; }
    public decimal AnomalyThresholdPercent { get; set; } = 25;
    public DateTime ValidFromUtc { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}

public sealed class MaintenanceWorkOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Number { get; set; } = "OT-" + DateTime.UtcNow.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    public string Kind { get; set; } = "Preventivo";
    public required string Title { get; set; }
    public required string EquipmentType { get; set; }
    public required string EquipmentId { get; set; }
    public string? TechnicianName { get; set; }
    public string? TechnicianEmail { get; set; }
    public DateTime CommitmentAtUtc { get; set; }
    public string Status { get; set; } = "Pendiente";
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
}

public sealed class NotificationRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string AlertType { get; set; } = "Todos";
    public string MinimumSeverity { get; set; } = "Advertencia";
    public string Channels { get; set; } = "Correo";
    public string Recipients { get; set; } = "";
    public bool DailySummary { get; set; }
    public bool WeeklySummary { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class IntegrationExecution
{
    public long Id { get; set; }
    public string Integration { get; set; } = "n8n";
    public string Operation { get; set; } = "Notificación";
    public string Status { get; set; } = "Pendiente";
    public int? HttpStatusCode { get; set; }
    public string? Detail { get; set; }
    public DateTime ExecutedAtUtc { get; set; } = DateTime.UtcNow;
}
