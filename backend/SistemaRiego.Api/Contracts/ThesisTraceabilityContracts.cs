using System.ComponentModel.DataAnnotations;

namespace SistemaRiego.Api.Contracts;

public sealed record DeviceInstallationRequest(
    Guid DeviceId,
    Guid? IrrigationZoneId,
    [Required, MaxLength(180)] string Location,
    DateTime InstalledAtUtc,
    [Required, MaxLength(150)] string InstallerName,
    DateTime? RemovedAtUtc,
    [MaxLength(500)] string? Notes);

public sealed record RemoteConfigurationRequest(
    Guid NodeId,
    [Required, MaxLength(50)] string CommandType,
    [Required, MaxLength(2000)] string Payload,
    int MaximumAttempts = 3);

public sealed record RemoteConfigurationAckRequest(bool Success, [MaxLength(500)] string? Detail);

public sealed record FirmwareRegistrationRequest(
    Guid NodeId,
    [Required, MaxLength(50)] string Version,
    [Required, MaxLength(30)] string Status,
    DateTime? AppliedAtUtc,
    [MaxLength(500)] string? Notes);

public sealed record CropRotationRequest(
    Guid IrrigationZoneId,
    Guid CropId,
    Guid? PreviousCycleId,
    DateOnly PlannedStartDate,
    DateOnly PlannedEndDate,
    [Required, MaxLength(30)] string Status,
    [MaxLength(500)] string? CompatibilityNotes);

public sealed record EnvironmentalEvaluationResponse(
    Guid CycleId,
    string Cycle,
    string Zone,
    decimal? TemperatureCelsius,
    decimal? AmbientHumidityPercent,
    bool InsideAllowedSchedule,
    bool TemperatureAllowed,
    bool AmbientHumidityAllowed,
    bool IrrigationAllowed,
    IReadOnlyCollection<string> Reasons);

