using System.ComponentModel.DataAnnotations;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Contracts;

public sealed record CatalogItemRequest(
    [Required, MaxLength(50)] string Code,
    [Required, MaxLength(120)] string Name,
    [MaxLength(300)] string? Description,
    [MaxLength(20)] string? Symbol,
    bool IsActive = true,
    [MaxLength(50)] string? BaseUnitCode = null,
    decimal? ConversionFactorToBase = null,
    int? IntervalSeconds = null);

public sealed record CatalogItemResponse(Guid Id, CatalogKind Kind, string Code, string Name, string? Description, string? Symbol, bool IsActive, string? BaseUnitCode, decimal? ConversionFactorToBase, int? IntervalSeconds);

public sealed record ParameterRequest(
    [Required, MaxLength(500)] string Value,
    [Required, MaxLength(30)] string DataType,
    [Required, MaxLength(80)] string Category,
    [Required, MaxLength(300)] string Description,
    bool IsEditable = true);

public sealed record ParameterResponse(Guid Id, string Key, string Value, string DataType, string Category, string Description, bool IsEditable, DateTime UpdatedAtUtc);

public sealed record AuditResponse(long Id, Guid? UserId, string? UserEmail, string EventType, string Detail, DateTime OccurredAtUtc, string? IpAddress);

public sealed record UnitConversionRequest(Guid FromUnitId, Guid ToUnitId, decimal Value);
public sealed record UnitConversionResponse(decimal OriginalValue, string FromUnit, decimal ConvertedValue, string ToUnit);
