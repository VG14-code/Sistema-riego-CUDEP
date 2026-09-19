namespace SistemaRiego.Api.Models;

public enum CatalogKind
{
    SensorType = 1,
    DeviceType = 2,
    MeasurementUnit = 3,
    OperationalStatus = 4,
    ValveType = 5,
    PumpType = 6,
    WaterSource = 7,
    AlertType = 8,
    SuspensionReason = 9,
    ReadingFrequency = 10
}

public sealed class MasterCatalogItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public CatalogKind Kind { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Symbol { get; set; }
    public string? BaseUnitCode { get; set; }
    public decimal? ConversionFactorToBase { get; set; }
    public int? IntervalSeconds { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class GlobalParameter
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Key { get; set; }
    public required string Value { get; set; }
    public required string DataType { get; set; }
    public required string Category { get; set; }
    public required string Description { get; set; }
    public bool IsEditable { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedByUserId { get; set; }
}
