namespace SistemaRiego.Api.Models;

public sealed class IoTNode
{
    public Guid Id { get; set; } = Guid.NewGuid(); public required string Code { get; set; } public required string Name { get; set; }
    public string? Location { get; set; } public string? IpAddress { get; set; } public string? MacAddress { get; set; }
    public string CommunicationProtocol { get; set; } = "HTTP"; public string? FirmwareVersion { get; set; }
    public Guid OperationalStatusId { get; set; } public MasterCatalogItem OperationalStatus { get; set; } = null!;
    public bool IsActive { get; set; } = true; public DateTime? LastCommunicationUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow; public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<IoTDevice> Devices { get; set; } = [];
}

public sealed class IoTDevice
{
    public Guid Id { get; set; } = Guid.NewGuid(); public required string Code { get; set; } public required string Name { get; set; }
    public required string SerialNumber { get; set; } public string? Manufacturer { get; set; } public string? Model { get; set; }
    public Guid? DeviceModelId { get; set; } public DeviceModel? DeviceModel { get; set; }
    public string? InstallationLocation { get; set; } public DateOnly? InstallationDate { get; set; }
    public Guid DeviceTypeId { get; set; } public MasterCatalogItem DeviceType { get; set; } = null!;
    public Guid OperationalStatusId { get; set; } public MasterCatalogItem OperationalStatus { get; set; } = null!;
    public Guid? NodeId { get; set; } public IoTNode? Node { get; set; }
    public string? Owner { get; set; } public string InventoryStatus { get; set; } = "Instalado"; public DateOnly? PurchaseDate { get; set; } public DateOnly? WarrantyUntil { get; set; } public decimal? AcquisitionCost { get; set; } public string Currency { get; set; } = "GTQ";
    public bool IsActive { get; set; } = true; public DateTime? LastCommunicationUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow; public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<IoTSensor> Sensors { get; set; } = [];
}

public sealed class IoTSensor
{
    public Guid Id { get; set; } = Guid.NewGuid(); public required string Code { get; set; } public required string Name { get; set; }
    public required string SerialNumber { get; set; } public string? Model { get; set; } public string? Channel { get; set; }
    public decimal MinimumValue { get; set; } public decimal MaximumValue { get; set; } = 100; public decimal CalibrationOffset { get; set; }
    public Guid SensorTypeId { get; set; } public MasterCatalogItem SensorType { get; set; } = null!;
    public Guid MeasurementUnitId { get; set; } public MasterCatalogItem MeasurementUnit { get; set; } = null!;
    public Guid OperationalStatusId { get; set; } public MasterCatalogItem OperationalStatus { get; set; } = null!;
    public Guid? DeviceId { get; set; } public IoTDevice? Device { get; set; }
    public Guid? ReadingFrequencyId { get; set; } public MasterCatalogItem? ReadingFrequency { get; set; }
    public Guid? IrrigationZoneId { get; set; } public IrrigationZone? IrrigationZone { get; set; }
    public bool IsActive { get; set; } = true; public DateTime? LastReadingUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow; public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<SensorCalibration> Calibrations { get; set; } = [];
}

public sealed class SensorCalibration
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid SensorId { get; set; } public IoTSensor Sensor { get; set; } = null!;
    public DateTime CalibratedAtUtc { get; set; } = DateTime.UtcNow; public decimal ReferenceValue { get; set; }
    public decimal MeasuredValue { get; set; } public decimal AppliedOffset { get; set; } public string? Notes { get; set; }
    public string? CalibrationPattern { get; set; } public string? TechnicianName { get; set; } public DateOnly? NextCalibrationDate { get; set; }
    public Guid? CalibratedByUserId { get; set; }
}

