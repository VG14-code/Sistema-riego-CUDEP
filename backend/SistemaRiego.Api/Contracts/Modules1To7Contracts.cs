using System.ComponentModel.DataAnnotations;

namespace SistemaRiego.Api.Contracts;

public sealed record CenterRequest([Required,MaxLength(30)] string Code,[Required,MaxLength(140)] string Name,[MaxLength(200)] string? Location,[MaxLength(160)] string? Contact,bool IsActive=true);
public sealed record FarmRequest(Guid UniversityCenterId,[Required,MaxLength(30)] string Code,[Required,MaxLength(140)] string Name,[MaxLength(200)] string? Location,decimal? Latitude,decimal? Longitude,bool IsActive=true);
public sealed record SoilTypeRequest([Required,MaxLength(30)] string Code,[Required,MaxLength(100)] string Name,decimal FieldCapacityPercent,decimal SaturationPercent,decimal InfiltrationMillimetersHour,[MaxLength(300)] string? Description,bool IsActive=true);
public sealed record BlockRequest(Guid FarmId,Guid? SoilTypeId,[Required,MaxLength(30)] string Code,[Required,MaxLength(120)] string Name,decimal AreaHectares,[MaxLength(300)] string? Description,bool IsActive=true);
public sealed record SectorRequest(Guid FarmBlockId,[Required,MaxLength(30)] string Code,[Required,MaxLength(120)] string Name,decimal AreaHectares,decimal SlopePercent,bool IsActive=true,string? BoundaryGeoJson=null);
public sealed record ZoneRequest(Guid IrrigationSectorId,[Required,MaxLength(30)] string Code,[Required,MaxLength(120)] string Name,decimal AreaHectares,Guid OperationalStatusId,Guid? PrimarySensorId,Guid? ValveDeviceId,decimal? Latitude,decimal? Longitude,bool IsActive=true,string? BoundaryGeoJson=null,IReadOnlyCollection<Guid>? SensorIds=null,IReadOnlyCollection<Guid>? ValveDeviceIds=null);

public sealed record TelemetryRequest(Guid SensorId,Guid? IrrigationZoneId,DateTime? CapturedAtUtc,decimal Value,decimal? BatteryPercent,int? SignalStrength,[Required,MaxLength(100)] string MessageId,[MaxLength(20)] string? Transport);
public sealed record ReadingResponse(long Id,Guid SensorId,string SensorName,string? ZoneName,DateTime CapturedAtUtc,decimal Value,string? UnitSymbol,decimal? BatteryPercent,int? SignalStrength,bool IsValid,string ValidationStatus,string Transport);
public sealed record CommandRequest(Guid DeviceId,[Required,MaxLength(50)] string CommandType,[MaxLength(1000)] string? Payload);

public sealed record CropTypeRequest([Required,MaxLength(30)] string Code,[Required,MaxLength(100)] string Name,bool IsActive=true);
public sealed record CropRequest(Guid CropTypeId,[Required,MaxLength(30)] string Code,[Required,MaxLength(120)] string Name,[MaxLength(160)] string? ScientificName,[MaxLength(400)] string? Description,bool IsActive=true);
public sealed record StageRequest(Guid CropId,[Required,MaxLength(100)] string Name,int Sequence,int EstimatedDays,[MaxLength(300)] string? Description);
public sealed record RequirementRequest(Guid CropId,Guid? PhenologicalStageId,Guid? SoilTypeId,decimal MinimumMoisturePercent,decimal TargetMoisturePercent,decimal MaximumMoisturePercent,decimal BaseVolumeLiters,int FrequencyHours,int BaseDurationMinutes,decimal? MinimumTemperatureCelsius,decimal? MaximumTemperatureCelsius,TimeOnly? AllowedFrom,TimeOnly? AllowedUntil,bool IsActive=true,decimal? MinimumAmbientHumidityPercent=null,decimal? MaximumAmbientHumidityPercent=null);
public sealed record CycleRequest(Guid CropId,Guid IrrigationZoneId,Guid? CurrentStageId,[Required,MaxLength(140)] string Name,DateOnly SowingDate,DateOnly ExpectedHarvestDate,DateOnly? ActualHarvestDate,decimal AreaHectares,int PlantCount,[Required,MaxLength(30)] string Status,[MaxLength(500)] string? Notes);

public sealed record IrrigationRecommendationResponse(Guid CycleId,string CycleName,string Crop,string Zone,decimal? CurrentMoisture,decimal MinimumMoisture,decimal TargetMoisture,string Decision,int SuggestedMinutes,decimal SuggestedLiters,string Explanation);
public sealed record SystemDashboardResponse(int ActiveSensors,int ActiveZones,int ActiveDevices,int InvalidReadings,int ActiveCropCycles,decimal AverageMoisture,DateTime GeneratedAtUtc,IReadOnlyCollection<ReadingResponse> LatestReadings,IReadOnlyCollection<ActivityItemResponse> RecentActivity,int ZonesIrrigating=0,decimal? TankLevelPercent=null,string PumpStatus="Sin datos",decimal? BatteryPercent=null,decimal TodayConsumptionLiters=0,int ActiveAlerts=0);
public sealed record ActivityItemResponse(string Type,string Detail,DateTime OccurredAtUtc);
public sealed record PagedReadingResponse(IReadOnlyCollection<ReadingResponse> Items,int Page,int PageSize,int Total,int TotalPages);
public sealed record TelemetryAggregateResponse(Guid SensorId,string SensorName,string? UnitSymbol,int Count,decimal Minimum,decimal Maximum,decimal Average,DateTime FromUtc,DateTime ToUtc);
public sealed record CropCycleResponse(Guid Id,string Name,Guid CropId,string Crop,Guid IrrigationZoneId,string Zone,Guid? CurrentStageId,string CurrentStage,DateOnly SowingDate,DateOnly ExpectedHarvestDate,DateOnly? ActualHarvestDate,decimal AreaHectares,int PlantCount,string Status,string? Notes);
