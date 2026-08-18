using System.ComponentModel.DataAnnotations;

namespace SistemaRiego.Api.Contracts;

public sealed record IrrigationRuleRequest(Guid IrrigationZoneId, Guid? CropWaterRequirementId, [Required,MaxLength(140)] string Name, decimal MinimumMoisturePercent, decimal TargetMoisturePercent, decimal HysteresisPercent, int Priority, int MaximumDurationMinutes, TimeOnly AllowedFrom, TimeOnly AllowedUntil, [Required] string AllowedDays, bool IsEnabled, bool RequiresSufficientEnergy = true);
public sealed record RuleToggleRequest(bool IsEnabled);
public sealed record TankLevelRequest(decimal LevelLiters, [MaxLength(240)] string? Detail);
public sealed record PumpCommandRequest([Required,MaxLength(240)] string Reason);
public sealed record ManualIrrigationRequest(Guid IrrigationZoneId, int DurationMinutes, decimal FlowRateLitersMinute, [Required,MaxLength(300)] string Reason, [MaxLength(500)] string? Observations);
public sealed record StopIrrigationRequest([MaxLength(500)] string? Observations);
