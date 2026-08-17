namespace SistemaRiego.Api.Models;

public sealed class UniversityCenter
{
    public Guid Id { get; set; } = Guid.NewGuid(); public required string Code { get; set; } public required string Name { get; set; }
    public string? Location { get; set; } public string? Contact { get; set; } public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow; public ICollection<Farm> Farms { get; set; } = [];
}
public sealed class Farm
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid UniversityCenterId { get; set; } public UniversityCenter UniversityCenter { get; set; } = null!;
    public required string Code { get; set; } public required string Name { get; set; } public string? Location { get; set; }
    public decimal? Latitude { get; set; } public decimal? Longitude { get; set; } public bool IsActive { get; set; } = true; public ICollection<FarmBlock> Blocks { get; set; } = [];
}
public sealed class SoilType
{
    public Guid Id { get; set; } = Guid.NewGuid(); public required string Code { get; set; } public required string Name { get; set; }
    public decimal FieldCapacityPercent { get; set; } public decimal SaturationPercent { get; set; } public decimal InfiltrationMillimetersHour { get; set; }
    public string? Description { get; set; } public bool IsActive { get; set; } = true;
}
public sealed class FarmBlock
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid FarmId { get; set; } public Farm Farm { get; set; } = null!;
    public Guid? SoilTypeId { get; set; } public SoilType? SoilType { get; set; } public required string Code { get; set; } public required string Name { get; set; }
    public decimal AreaHectares { get; set; } public string? Description { get; set; } public bool IsActive { get; set; } = true; public ICollection<IrrigationSector> Sectors { get; set; } = [];
}
public sealed class IrrigationSector
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid FarmBlockId { get; set; } public FarmBlock FarmBlock { get; set; } = null!;
    public required string Code { get; set; } public required string Name { get; set; } public decimal AreaHectares { get; set; } public decimal SlopePercent { get; set; }
    public bool IsActive { get; set; } = true; public ICollection<IrrigationZone> Zones { get; set; } = [];
}
public sealed class IrrigationZone
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid IrrigationSectorId { get; set; } public IrrigationSector IrrigationSector { get; set; } = null!;
    public required string Code { get; set; } public required string Name { get; set; } public decimal AreaHectares { get; set; }
    public Guid OperationalStatusId { get; set; } public MasterCatalogItem OperationalStatus { get; set; } = null!;
    public Guid? PrimarySensorId { get; set; } public IoTSensor? PrimarySensor { get; set; } public Guid? ValveDeviceId { get; set; } public IoTDevice? ValveDevice { get; set; }
    public decimal? Latitude { get; set; } public decimal? Longitude { get; set; } public bool IsActive { get; set; } = true;
}

public sealed class SensorReading
{
    public long Id { get; set; } public Guid SensorId { get; set; } public IoTSensor Sensor { get; set; } = null!; public Guid? IrrigationZoneId { get; set; } public IrrigationZone? IrrigationZone { get; set; }
    public DateTime CapturedAtUtc { get; set; } public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow; public decimal Value { get; set; }
    public decimal? BatteryPercent { get; set; } public int? SignalStrength { get; set; } public required string MessageId { get; set; }
    public bool IsValid { get; set; } = true; public string ValidationStatus { get; set; } = "Válida"; public string Transport { get; set; } = "HTTP";
}
public sealed class IoTCommand
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid DeviceId { get; set; } public IoTDevice Device { get; set; } = null!;
    public required string CommandType { get; set; } public string? Payload { get; set; } public string Status { get; set; } = "Pendiente";
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow; public DateTime? ConfirmedAtUtc { get; set; } public Guid? RequestedByUserId { get; set; }
}

public sealed class CropType
{
    public Guid Id { get; set; } = Guid.NewGuid(); public required string Code { get; set; } public required string Name { get; set; } public bool IsActive { get; set; } = true;
}
public sealed class Crop
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid CropTypeId { get; set; } public CropType CropType { get; set; } = null!;
    public required string Code { get; set; } public required string Name { get; set; } public string? ScientificName { get; set; } public string? Description { get; set; }
    public bool IsActive { get; set; } = true; public ICollection<PhenologicalStage> Stages { get; set; } = [];
}
public sealed class PhenologicalStage
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid CropId { get; set; } public Crop Crop { get; set; } = null!;
    public required string Name { get; set; } public int Sequence { get; set; } public int EstimatedDays { get; set; } public string? Description { get; set; }
}
public sealed class CropWaterRequirement
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid CropId { get; set; } public Crop Crop { get; set; } = null!;
    public Guid? PhenologicalStageId { get; set; } public PhenologicalStage? PhenologicalStage { get; set; } public Guid? SoilTypeId { get; set; } public SoilType? SoilType { get; set; }
    public decimal MinimumMoisturePercent { get; set; } public decimal TargetMoisturePercent { get; set; } public decimal MaximumMoisturePercent { get; set; }
    public decimal BaseVolumeLiters { get; set; } public int FrequencyHours { get; set; } public int BaseDurationMinutes { get; set; }
    public decimal? MinimumTemperatureCelsius { get; set; } public decimal? MaximumTemperatureCelsius { get; set; }
    public TimeOnly? AllowedFrom { get; set; } public TimeOnly? AllowedUntil { get; set; } public bool IsActive { get; set; } = true;
}
public sealed class CropCycle
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid CropId { get; set; } public Crop Crop { get; set; } = null!;
    public Guid IrrigationZoneId { get; set; } public IrrigationZone IrrigationZone { get; set; } = null!; public Guid? CurrentStageId { get; set; } public PhenologicalStage? CurrentStage { get; set; }
    public required string Name { get; set; } public DateOnly SowingDate { get; set; } public DateOnly ExpectedHarvestDate { get; set; } public DateOnly? ActualHarvestDate { get; set; }
    public decimal AreaHectares { get; set; } public int PlantCount { get; set; } public string Status { get; set; } = "Planificado"; public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
