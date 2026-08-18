namespace SistemaRiego.Api.Models;

public sealed class PumpStationReading
{
    public long Id { get; set; }
    public Guid WaterTankId { get; set; }
    public WaterTank WaterTank { get; set; } = null!;
    public Guid WaterPumpId { get; set; }
    public WaterPump WaterPump { get; set; } = null!;
    public DateTime CapturedAtUtc { get; set; }
    public decimal LevelLiters { get; set; }
    public decimal PressureBar { get; set; }
    public decimal MotorCurrentAmps { get; set; }
    public bool IsPumpRunning { get; set; }
    public required string MessageId { get; set; }
}

public sealed class SystemSafetyState
{
    public int Id { get; set; } = 1;
    public bool EmergencyStopActive { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }
    public DateTime? ClearedAtUtc { get; set; }
    public string? Detail { get; set; }
}

public sealed class SolarPanelArray
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public decimal RatedPowerWatts { get; set; }
    public int PanelCount { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class SolarBattery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public decimal CapacityWattHours { get; set; }
    public decimal NominalVoltage { get; set; }
    public decimal MinimumSafeChargePercent { get; set; } = 20;
    public decimal CurrentChargePercent { get; set; } = 80;
    public string Status { get; set; } = "Disponible";
}

public sealed class ChargeController
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SolarPanelArrayId { get; set; }
    public SolarPanelArray SolarPanelArray { get; set; } = null!;
    public Guid SolarBatteryId { get; set; }
    public SolarBattery SolarBattery { get; set; } = null!;
    public required string Name { get; set; }
    public decimal RatedCurrentAmps { get; set; }
    public string Status { get; set; } = "Operativo";
}

public sealed class EnergyReading
{
    public long Id { get; set; }
    public Guid ChargeControllerId { get; set; }
    public ChargeController ChargeController { get; set; } = null!;
    public DateTime CapturedAtUtc { get; set; }
    public decimal GenerationWatts { get; set; }
    public decimal BatteryPercent { get; set; }
    public decimal ConsumptionWatts { get; set; }
    public decimal BatteryVoltage { get; set; }
    public required string MessageId { get; set; }
}

public sealed class FlowReading
{
    public long Id { get; set; }
    public Guid IrrigationZoneId { get; set; }
    public IrrigationZone IrrigationZone { get; set; } = null!;
    public DateTime CapturedAtUtc { get; set; }
    public decimal FlowLitersMinute { get; set; }
    public required string MessageId { get; set; }
}
