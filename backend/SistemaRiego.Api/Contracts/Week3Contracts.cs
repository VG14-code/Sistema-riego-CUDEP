using System.ComponentModel.DataAnnotations;

namespace SistemaRiego.Api.Contracts;

public sealed record IoTNodeRequest(
    [Required, MaxLength(50)] string Code,
    [Required, MaxLength(120)] string Name,
    [MaxLength(180)] string? Location,
    [MaxLength(45)] string? IpAddress,
    [MaxLength(30)] string? MacAddress,
    [Required, MaxLength(30)] string CommunicationProtocol,
    [MaxLength(50)] string? FirmwareVersion,
    Guid OperationalStatusId,
    bool IsActive = true);

public sealed record IoTDeviceRequest(
    [Required, MaxLength(50)] string Code,
    [Required, MaxLength(120)] string Name,
    [Required, MaxLength(100)] string SerialNumber,
    [MaxLength(100)] string? Manufacturer,
    [MaxLength(100)] string? Model,
    [MaxLength(180)] string? InstallationLocation,
    DateOnly? InstallationDate,
    Guid DeviceTypeId,
    Guid OperationalStatusId,
    Guid? NodeId,
    bool IsActive = true,
    Guid? DeviceModelId = null,
    [MaxLength(150)] string? Owner = null,
    [MaxLength(30)] string InventoryStatus = "Instalado",
    DateOnly? PurchaseDate = null,
    DateOnly? WarrantyUntil = null,
    decimal? AcquisitionCost = null,
    [MaxLength(3)] string Currency = "GTQ");

public sealed record IoTSensorRequest(
    [Required, MaxLength(50)] string Code,
    [Required, MaxLength(120)] string Name,
    [Required, MaxLength(100)] string SerialNumber,
    [MaxLength(100)] string? Model,
    [MaxLength(40)] string? Channel,
    decimal MinimumValue,
    decimal MaximumValue,
    decimal CalibrationOffset,
    Guid SensorTypeId,
    Guid MeasurementUnitId,
    Guid OperationalStatusId,
    Guid? DeviceId,
    bool IsActive = true);

public sealed record SensorCalibrationRequest(
    Guid SensorId,
    DateTime? CalibratedAtUtc,
    decimal ReferenceValue,
    decimal MeasuredValue,
    [MaxLength(500)] string? Notes);

public sealed record IoTNodeResponse(Guid Id, string Code, string Name, string? Location, string? IpAddress, string? MacAddress, string CommunicationProtocol, string? FirmwareVersion, Guid OperationalStatusId, string OperationalStatus, bool IsActive, DateTime? LastCommunicationUtc, int DeviceCount);
public sealed record IoTDeviceResponse(Guid Id, string Code, string Name, string SerialNumber, string? Manufacturer, string? Model, string? InstallationLocation, DateOnly? InstallationDate, Guid DeviceTypeId, string DeviceType, Guid OperationalStatusId, string OperationalStatus, Guid? NodeId, string? NodeName, bool IsActive, DateTime? LastCommunicationUtc, int SensorCount, Guid? DeviceModelId, string? DeviceModel, string? Owner, string InventoryStatus, DateOnly? PurchaseDate, DateOnly? WarrantyUntil, decimal? AcquisitionCost, string Currency);
public sealed record IoTSensorResponse(Guid Id, string Code, string Name, string SerialNumber, string? Model, string? Channel, decimal MinimumValue, decimal MaximumValue, decimal CalibrationOffset, Guid SensorTypeId, string SensorType, Guid MeasurementUnitId, string MeasurementUnit, string? UnitSymbol, Guid OperationalStatusId, string OperationalStatus, Guid? DeviceId, string? DeviceName, bool IsActive, DateTime? LastReadingUtc, DateTime? LastCalibrationUtc);
public sealed record SensorCalibrationResponse(Guid Id, Guid SensorId, string SensorName, DateTime CalibratedAtUtc, decimal ReferenceValue, decimal MeasuredValue, decimal AppliedOffset, string? Notes);
public sealed record IoTSummaryResponse(int Nodes, int Devices, int Sensors, int ActiveSensors, int OfflineItems, int CalibratedSensors);

