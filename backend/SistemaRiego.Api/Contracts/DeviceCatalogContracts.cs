using System.ComponentModel.DataAnnotations;

namespace SistemaRiego.Api.Contracts;

public sealed record DeviceBrandRequest([Required, MaxLength(50)] string Code, [Required, MaxLength(120)] string Name, [MaxLength(300)] string? Description, bool IsActive = true);
public sealed record DeviceModelRequest(Guid DeviceBrandId, Guid DeviceTypeId, [Required, MaxLength(50)] string Code, [Required, MaxLength(120)] string Name, [MaxLength(300)] string? Description, bool IsActive = true);
public sealed record DeviceBrandResponse(Guid Id, string Code, string Name, string? Description, bool IsActive, int ModelCount);
public sealed record DeviceModelResponse(Guid Id, Guid DeviceBrandId, string Brand, Guid DeviceTypeId, string DeviceType, string Code, string Name, string? Description, bool IsActive, int DeviceCount);
