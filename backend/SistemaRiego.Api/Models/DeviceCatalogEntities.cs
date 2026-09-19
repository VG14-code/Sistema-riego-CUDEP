namespace SistemaRiego.Api.Models;

public sealed class DeviceBrand
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<DeviceModel> Models { get; set; } = [];
}

public sealed class DeviceModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceBrandId { get; set; }
    public DeviceBrand DeviceBrand { get; set; } = null!;
    public Guid DeviceTypeId { get; set; }
    public MasterCatalogItem DeviceType { get; set; } = null!;
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Precision { get; set; }
    public string? Voltage { get; set; }
    public string? CommunicationProtocol { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<IoTDevice> Devices { get; set; } = [];
}
