using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/iot"), Authorize(Policy = Policies.Operator)]
public sealed class IoTController(AppDbContext db) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<IoTSummaryResponse>> Summary(CancellationToken ct)
    {
        var offlineId = await db.MasterCatalogItems.Where(x => x.Kind == CatalogKind.OperationalStatus && x.Code == "OFFLINE").Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        return Ok(new IoTSummaryResponse(
            await db.IoTNodes.CountAsync(x => x.IsActive, ct),
            await db.IoTDevices.CountAsync(x => x.IsActive, ct),
            await db.IoTSensors.CountAsync(ct),
            await db.IoTSensors.CountAsync(x => x.IsActive, ct),
            offlineId is null ? 0 : await db.IoTNodes.CountAsync(x => x.OperationalStatusId == offlineId, ct) + await db.IoTDevices.CountAsync(x => x.OperationalStatusId == offlineId, ct) + await db.IoTSensors.CountAsync(x => x.OperationalStatusId == offlineId, ct),
            await db.SensorCalibrations.Select(x => x.SensorId).Distinct().CountAsync(ct)));
    }

    [HttpGet("nodes")]
    public async Task<ActionResult<IReadOnlyCollection<IoTNodeResponse>>> Nodes(CancellationToken ct) => Ok((await db.IoTNodes.AsNoTracking().Include(x => x.OperationalStatus).Include(x => x.Devices).OrderBy(x => x.Name).ToListAsync(ct)).Select(NodeResponse));

    [HttpPost("nodes"), Authorize(Policy = Policies.Technician)]
    public async Task<ActionResult<IoTNodeResponse>> CreateNode(IoTNodeRequest request, CancellationToken ct)
    {
        var validation = await ValidateCatalog(request.OperationalStatusId, CatalogKind.OperationalStatus, ct); if (validation is not null) return BadRequest(new { message = validation });
        var code = NormalizeCode(request.Code); if (await db.IoTNodes.AnyAsync(x => x.Code == code, ct)) return Conflict(new { message = "Ya existe un nodo con ese código." });
        var node = new IoTNode { Code = code, Name = request.Name.Trim(), Location = Trim(request.Location), IpAddress = Trim(request.IpAddress), MacAddress = Trim(request.MacAddress), CommunicationProtocol = request.CommunicationProtocol.Trim().ToUpperInvariant(), FirmwareVersion = Trim(request.FirmwareVersion), OperationalStatusId = request.OperationalStatusId, IsActive = request.IsActive };
        db.IoTNodes.Add(node); Audit("IOT_NODE_CREATED", node.Code); await db.SaveChangesAsync(ct); await db.Entry(node).Reference(x => x.OperationalStatus).LoadAsync(ct);
        return CreatedAtAction(nameof(Nodes), NodeResponse(node));
    }

    [HttpPut("nodes/{id:guid}"), Authorize(Policy = Policies.Technician)]
    public async Task<ActionResult<IoTNodeResponse>> UpdateNode(Guid id, IoTNodeRequest request, CancellationToken ct)
    {
        var node = await db.IoTNodes.Include(x => x.OperationalStatus).Include(x => x.Devices).SingleOrDefaultAsync(x => x.Id == id, ct); if (node is null) return NotFound();
        var validation = await ValidateCatalog(request.OperationalStatusId, CatalogKind.OperationalStatus, ct); if (validation is not null) return BadRequest(new { message = validation });
        var code = NormalizeCode(request.Code); if (await db.IoTNodes.AnyAsync(x => x.Code == code && x.Id != id, ct)) return Conflict(new { message = "Ya existe un nodo con ese código." });
        node.Code = code; node.Name = request.Name.Trim(); node.Location = Trim(request.Location); node.IpAddress = Trim(request.IpAddress); node.MacAddress = Trim(request.MacAddress); node.CommunicationProtocol = request.CommunicationProtocol.Trim().ToUpperInvariant(); node.FirmwareVersion = Trim(request.FirmwareVersion); node.OperationalStatusId = request.OperationalStatusId; node.IsActive = request.IsActive; node.UpdatedAtUtc = DateTime.UtcNow;
        Audit("IOT_NODE_UPDATED", node.Code); await db.SaveChangesAsync(ct); await db.Entry(node).Reference(x => x.OperationalStatus).LoadAsync(ct); return Ok(NodeResponse(node));
    }

    [HttpPatch("nodes/{id:guid}/deactivate"), Authorize(Policy = Policies.Technician)]
    public async Task<IActionResult> DeactivateNode(Guid id, CancellationToken ct) => await Deactivate(db.IoTNodes, id, "IOT_NODE_DEACTIVATED", ct);

    [HttpGet("devices")]
    public async Task<ActionResult<IReadOnlyCollection<IoTDeviceResponse>>> Devices(CancellationToken ct) => Ok((await db.IoTDevices.AsNoTracking().Include(x => x.DeviceType).Include(x => x.OperationalStatus).Include(x => x.Node).Include(x => x.DeviceModel).Include(x => x.Sensors).OrderBy(x => x.Name).ToListAsync(ct)).Select(DeviceResponse));

    [HttpPost("devices"), Authorize(Policy = Policies.Technician)]
    public async Task<ActionResult<IoTDeviceResponse>> CreateDevice(IoTDeviceRequest request, CancellationToken ct)
    {
        var validation = await ValidateDeviceRequest(request, null, ct); if (validation is not null) return BadRequest(new { message = validation });
        var code = NormalizeCode(request.Code); if (await db.IoTDevices.AnyAsync(x => x.Code == code || x.SerialNumber == request.SerialNumber.Trim(), ct)) return Conflict(new { message = "El código o número de serie ya está registrado." });
        var item = new IoTDevice { Code = code, Name = request.Name.Trim(), SerialNumber = request.SerialNumber.Trim(), Manufacturer = Trim(request.Manufacturer), Model = Trim(request.Model), InstallationLocation = Trim(request.InstallationLocation), InstallationDate = request.InstallationDate, DeviceTypeId = request.DeviceTypeId, OperationalStatusId = request.OperationalStatusId, NodeId = request.NodeId, DeviceModelId = request.DeviceModelId, Owner = Trim(request.Owner), InventoryStatus = request.InventoryStatus.Trim(), PurchaseDate = request.PurchaseDate, WarrantyUntil = request.WarrantyUntil, AcquisitionCost = request.AcquisitionCost, Currency = request.Currency.Trim().ToUpperInvariant(), IsActive = request.IsActive };
        db.IoTDevices.Add(item); Audit("IOT_DEVICE_CREATED", item.Code); await db.SaveChangesAsync(ct); return CreatedAtAction(nameof(Devices), await LoadDeviceResponse(item.Id, ct));
    }

    [HttpPut("devices/{id:guid}"), Authorize(Policy = Policies.Technician)]
    public async Task<ActionResult<IoTDeviceResponse>> UpdateDevice(Guid id, IoTDeviceRequest request, CancellationToken ct)
    {
        var item = await db.IoTDevices.SingleOrDefaultAsync(x => x.Id == id, ct); if (item is null) return NotFound();
        var validation = await ValidateDeviceRequest(request, id, ct); if (validation is not null) return BadRequest(new { message = validation });
        var code = NormalizeCode(request.Code); if (await db.IoTDevices.AnyAsync(x => x.Id != id && (x.Code == code || x.SerialNumber == request.SerialNumber.Trim()), ct)) return Conflict(new { message = "El código o número de serie ya está registrado." });
        item.Code = code; item.Name = request.Name.Trim(); item.SerialNumber = request.SerialNumber.Trim(); item.Manufacturer = Trim(request.Manufacturer); item.Model = Trim(request.Model); item.InstallationLocation = Trim(request.InstallationLocation); item.InstallationDate = request.InstallationDate; item.DeviceTypeId = request.DeviceTypeId; item.OperationalStatusId = request.OperationalStatusId; item.NodeId = request.NodeId; item.DeviceModelId = request.DeviceModelId; item.Owner = Trim(request.Owner); item.InventoryStatus = request.InventoryStatus.Trim(); item.PurchaseDate = request.PurchaseDate; item.WarrantyUntil = request.WarrantyUntil; item.AcquisitionCost = request.AcquisitionCost; item.Currency = request.Currency.Trim().ToUpperInvariant(); item.IsActive = request.IsActive; item.UpdatedAtUtc = DateTime.UtcNow;
        Audit("IOT_DEVICE_UPDATED", item.Code); await db.SaveChangesAsync(ct); return Ok(await LoadDeviceResponse(id, ct));
    }

    [HttpPatch("devices/{id:guid}/deactivate"), Authorize(Policy = Policies.Technician)]
    public async Task<IActionResult> DeactivateDevice(Guid id, CancellationToken ct) => await Deactivate(db.IoTDevices, id, "IOT_DEVICE_DEACTIVATED", ct);

    [HttpGet("sensors")]
    public async Task<ActionResult<IReadOnlyCollection<IoTSensorResponse>>> Sensors(CancellationToken ct) => Ok((await SensorQuery().OrderBy(x => x.Name).ToListAsync(ct)).Select(SensorResponse));

    [HttpPost("sensors"), Authorize(Policy = Policies.Technician)]
    public async Task<ActionResult<IoTSensorResponse>> CreateSensor(IoTSensorRequest request, CancellationToken ct)
    {
        var validation = await ValidateSensorRequest(request, null, ct); if (validation is not null) return BadRequest(new { message = validation });
        var code = NormalizeCode(request.Code); if (await db.IoTSensors.AnyAsync(x => x.Code == code || x.SerialNumber == request.SerialNumber.Trim(), ct)) return Conflict(new { message = "El código o número de serie ya está registrado." });
        var item = new IoTSensor { Code = code, Name = request.Name.Trim(), SerialNumber = request.SerialNumber.Trim(), Model = Trim(request.Model), Channel = Trim(request.Channel), MinimumValue = request.MinimumValue, MaximumValue = request.MaximumValue, CalibrationOffset = request.CalibrationOffset, SensorTypeId = request.SensorTypeId, MeasurementUnitId = request.MeasurementUnitId, OperationalStatusId = request.OperationalStatusId, DeviceId = request.DeviceId, IsActive = request.IsActive };
        db.IoTSensors.Add(item); Audit("IOT_SENSOR_CREATED", item.Code); await db.SaveChangesAsync(ct); return CreatedAtAction(nameof(Sensors), await LoadSensorResponse(item.Id, ct));
    }

    [HttpPut("sensors/{id:guid}"), Authorize(Policy = Policies.Technician)]
    public async Task<ActionResult<IoTSensorResponse>> UpdateSensor(Guid id, IoTSensorRequest request, CancellationToken ct)
    {
        var item = await db.IoTSensors.SingleOrDefaultAsync(x => x.Id == id, ct); if (item is null) return NotFound();
        var validation = await ValidateSensorRequest(request, id, ct); if (validation is not null) return BadRequest(new { message = validation });
        var code = NormalizeCode(request.Code); if (await db.IoTSensors.AnyAsync(x => x.Id != id && (x.Code == code || x.SerialNumber == request.SerialNumber.Trim()), ct)) return Conflict(new { message = "El código o número de serie ya está registrado." });
        item.Code = code; item.Name = request.Name.Trim(); item.SerialNumber = request.SerialNumber.Trim(); item.Model = Trim(request.Model); item.Channel = Trim(request.Channel); item.MinimumValue = request.MinimumValue; item.MaximumValue = request.MaximumValue; item.CalibrationOffset = request.CalibrationOffset; item.SensorTypeId = request.SensorTypeId; item.MeasurementUnitId = request.MeasurementUnitId; item.OperationalStatusId = request.OperationalStatusId; item.DeviceId = request.DeviceId; item.IsActive = request.IsActive; item.UpdatedAtUtc = DateTime.UtcNow;
        Audit("IOT_SENSOR_UPDATED", item.Code); await db.SaveChangesAsync(ct); return Ok(await LoadSensorResponse(id, ct));
    }

    [HttpPatch("sensors/{id:guid}/deactivate"), Authorize(Policy = Policies.Technician)]
    public async Task<IActionResult> DeactivateSensor(Guid id, CancellationToken ct) => await Deactivate(db.IoTSensors, id, "IOT_SENSOR_DEACTIVATED", ct);

    [HttpGet("calibrations")]
    public async Task<ActionResult<IReadOnlyCollection<SensorCalibrationResponse>>> Calibrations(CancellationToken ct) => Ok(await db.SensorCalibrations.AsNoTracking().Include(x => x.Sensor).OrderByDescending(x => x.CalibratedAtUtc).Select(x => new SensorCalibrationResponse(x.Id, x.SensorId, x.Sensor.Name, x.CalibratedAtUtc, x.ReferenceValue, x.MeasuredValue, x.AppliedOffset, x.Notes)).ToListAsync(ct));

    [HttpPost("calibrations"), Authorize(Policy = Policies.Technician)]
    public async Task<ActionResult<SensorCalibrationResponse>> Calibrate(SensorCalibrationRequest request, CancellationToken ct)
    {
        var sensor = await db.IoTSensors.SingleOrDefaultAsync(x => x.Id == request.SensorId && x.IsActive, ct); if (sensor is null) return BadRequest(new { message = "El sensor seleccionado no existe o está inactivo." });
        var offset = request.ReferenceValue - request.MeasuredValue; var item = new SensorCalibration { SensorId = sensor.Id, CalibratedAtUtc = request.CalibratedAtUtc ?? DateTime.UtcNow, ReferenceValue = request.ReferenceValue, MeasuredValue = request.MeasuredValue, AppliedOffset = offset, Notes = Trim(request.Notes), CalibratedByUserId = CurrentUserId() };
        sensor.CalibrationOffset = offset; sensor.UpdatedAtUtc = DateTime.UtcNow; db.SensorCalibrations.Add(item); Audit("IOT_SENSOR_CALIBRATED", sensor.Code); await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Calibrations), new SensorCalibrationResponse(item.Id, sensor.Id, sensor.Name, item.CalibratedAtUtc, item.ReferenceValue, item.MeasuredValue, item.AppliedOffset, item.Notes));
    }

    private IQueryable<IoTSensor> SensorQuery() => db.IoTSensors.AsNoTracking().Include(x => x.SensorType).Include(x => x.MeasurementUnit).Include(x => x.OperationalStatus).Include(x => x.Device).Include(x => x.Calibrations);
    private async Task<IoTSensorResponse> LoadSensorResponse(Guid id, CancellationToken ct) => SensorResponse(await SensorQuery().SingleAsync(x => x.Id == id, ct));
    private async Task<IoTDeviceResponse> LoadDeviceResponse(Guid id, CancellationToken ct) => DeviceResponse(await db.IoTDevices.AsNoTracking().Include(x => x.DeviceType).Include(x => x.OperationalStatus).Include(x => x.Node).Include(x => x.DeviceModel).Include(x => x.Sensors).SingleAsync(x => x.Id == id, ct));
    private async Task<string?> ValidateDeviceRequest(IoTDeviceRequest r, Guid? id, CancellationToken ct)
    {
        if (r.AcquisitionCost < 0 || string.IsNullOrWhiteSpace(r.InventoryStatus) || r.Currency.Trim().Length != 3) return "Inventario, costo y moneda no son válidos.";
        if (r.WarrantyUntil.HasValue && r.PurchaseDate.HasValue && r.WarrantyUntil < r.PurchaseDate) return "La garantía no puede vencer antes de la compra.";
        var typeError = await ValidateCatalog(r.DeviceTypeId, CatalogKind.DeviceType, ct); if (typeError is not null) return typeError;
        var statusError = await ValidateCatalog(r.OperationalStatusId, CatalogKind.OperationalStatus, ct); if (statusError is not null) return statusError;
        if (r.NodeId is not null && !await db.IoTNodes.AnyAsync(x => x.Id == r.NodeId && x.IsActive, ct)) return "El nodo seleccionado no existe o está inactivo.";
        if (r.DeviceModelId is not null && !await db.DeviceModels.AnyAsync(x => x.Id == r.DeviceModelId && x.DeviceTypeId == r.DeviceTypeId && x.IsActive, ct)) return "El modelo no existe, está inactivo o no corresponde al tipo de dispositivo.";
        return null;
    }
    private async Task<string?> ValidateSensorRequest(IoTSensorRequest r, Guid? id, CancellationToken ct)
    {
        if (r.MaximumValue <= r.MinimumValue) return "El valor máximo debe ser mayor que el mínimo.";
        var sensorError = await ValidateCatalog(r.SensorTypeId, CatalogKind.SensorType, ct); if (sensorError is not null) return sensorError;
        var unitError = await ValidateCatalog(r.MeasurementUnitId, CatalogKind.MeasurementUnit, ct); if (unitError is not null) return unitError;
        var statusError = await ValidateCatalog(r.OperationalStatusId, CatalogKind.OperationalStatus, ct); if (statusError is not null) return statusError;
        if (r.DeviceId is not null && !await db.IoTDevices.AnyAsync(x => x.Id == r.DeviceId && x.IsActive, ct)) return "El dispositivo seleccionado no existe o está inactivo.";
        return null;
    }
    private async Task<string?> ValidateCatalog(Guid id, CatalogKind kind, CancellationToken ct) => await db.MasterCatalogItems.AnyAsync(x => x.Id == id && x.Kind == kind && x.IsActive, ct) ? null : $"El catálogo {kind} seleccionado no existe o está inactivo.";
    private async Task<IActionResult> Deactivate<T>(DbSet<T> set, Guid id, string eventType, CancellationToken ct) where T : class
    {
        var item = await set.FindAsync([id], ct); if (item is null) return NotFound();
        var active = item.GetType().GetProperty(nameof(IoTNode.IsActive))!; active.SetValue(item, false); item.GetType().GetProperty(nameof(IoTNode.UpdatedAtUtc))!.SetValue(item, DateTime.UtcNow);
        var code = item.GetType().GetProperty(nameof(IoTNode.Code))!.GetValue(item)?.ToString() ?? id.ToString(); Audit(eventType, code); await db.SaveChangesAsync(ct); return NoContent();
    }
    private void Audit(string eventType, string code) => db.AccessAudits.Add(new AccessAudit { UserId = CurrentUserId(), EventType = eventType, Detail = code });
    private Guid? CurrentUserId() => Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
    private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant().Replace(' ', '_');
    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static IoTNodeResponse NodeResponse(IoTNode x) => new(x.Id, x.Code, x.Name, x.Location, x.IpAddress, x.MacAddress, x.CommunicationProtocol, x.FirmwareVersion, x.OperationalStatusId, x.OperationalStatus.Name, x.IsActive, x.LastCommunicationUtc, x.Devices.Count);
    private static IoTDeviceResponse DeviceResponse(IoTDevice x) => new(x.Id, x.Code, x.Name, x.SerialNumber, x.Manufacturer, x.Model, x.InstallationLocation, x.InstallationDate, x.DeviceTypeId, x.DeviceType.Name, x.OperationalStatusId, x.OperationalStatus.Name, x.NodeId, x.Node?.Name, x.IsActive, x.LastCommunicationUtc, x.Sensors.Count, x.DeviceModelId, x.DeviceModel?.Name, x.Owner, x.InventoryStatus, x.PurchaseDate, x.WarrantyUntil, x.AcquisitionCost, x.Currency);
    private static IoTSensorResponse SensorResponse(IoTSensor x) => new(x.Id, x.Code, x.Name, x.SerialNumber, x.Model, x.Channel, x.MinimumValue, x.MaximumValue, x.CalibrationOffset, x.SensorTypeId, x.SensorType.Name, x.MeasurementUnitId, x.MeasurementUnit.Name, x.MeasurementUnit.Symbol, x.OperationalStatusId, x.OperationalStatus.Name, x.DeviceId, x.Device?.Name, x.IsActive, x.LastReadingUtc, x.Calibrations.OrderByDescending(c => c.CalibratedAtUtc).Select(c => (DateTime?)c.CalibratedAtUtc).FirstOrDefault());
}

