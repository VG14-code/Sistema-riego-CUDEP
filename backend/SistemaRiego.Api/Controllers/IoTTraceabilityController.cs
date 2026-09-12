using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/iot/traceability"), Authorize(Policy = PermissionPolicies.IoTRead)]
public sealed class IoTTraceabilityController(AppDbContext db, IRemoteConfigurationDispatcher? remoteDispatcher = null) : ControllerBase
{
    [HttpGet("installations")]
    public async Task<IActionResult> Installations(CancellationToken ct) => Ok(await db.DeviceInstallations.AsNoTracking()
        .OrderByDescending(x => x.InstalledAtUtc)
        .Select(x => new { x.Id, x.DeviceId, Device = x.Device.Name, x.IrrigationZoneId, Zone = x.IrrigationZone != null ? x.IrrigationZone.Name : null, x.Location, x.InstalledAtUtc, x.InstalledByUserId, x.InstallerName, x.RemovedAtUtc, x.Notes, IsCurrent = x.RemovedAtUtc == null })
        .ToListAsync(ct));

    [HttpPost("installations"), Authorize(Policy = PermissionPolicies.DevicesManage)]
    public async Task<IActionResult> CreateInstallation(DeviceInstallationRequest request, CancellationToken ct)
    {
        var error = await ValidateInstallation(request, null, ct); if (error is not null) return BadRequest(new { message = error });
        var item = Map(request, new DeviceInstallation { DeviceId = request.DeviceId, InstalledByUserId = UserId() });
        db.DeviceInstallations.Add(item); await Save("IOT_INSTALLATION_CREATED", request.DeviceId.ToString(), ct);
        return Ok(item);
    }

    [HttpPut("installations/{id:guid}"), Authorize(Policy = PermissionPolicies.DevicesManage)]
    public async Task<IActionResult> UpdateInstallation(Guid id, DeviceInstallationRequest request, CancellationToken ct)
    {
        var item = await db.DeviceInstallations.FindAsync([id], ct); if (item is null) return NotFound();
        var error = await ValidateInstallation(request, id, ct); if (error is not null) return BadRequest(new { message = error });
        Map(request, item); await Save("IOT_INSTALLATION_UPDATED", id.ToString(), ct); return NoContent();
    }

    [HttpGet("remote-configurations")]
    public async Task<IActionResult> RemoteConfigurations(CancellationToken ct) => Ok(await db.RemoteConfigurationCommands.AsNoTracking()
        .OrderByDescending(x => x.RequestedAtUtc).Take(100)
        .Select(x => new { x.Id, x.NodeId, Node = x.Node.Name, x.CommandType, x.Payload, x.Status, x.Attempts, x.MaximumAttempts, x.RequestedAtUtc, x.LastAttemptAtUtc, x.ConfirmedAtUtc, x.LastError })
        .ToListAsync(ct));

    [HttpPost("remote-configurations"), Authorize(Policy = PermissionPolicies.DevicesManage)]
    public async Task<IActionResult> QueueRemoteConfiguration(RemoteConfigurationRequest request, CancellationToken ct)
    {
        if (!await db.IoTNodes.AnyAsync(x => x.Id == request.NodeId && x.IsActive, ct)) return BadRequest(new { message = "Nodo inválido o inactivo." });
        var allowed = new[] { "CAMBIAR_FRECUENCIA", "CAMBIAR_LIMITES", "REINICIAR" };
        var type = request.CommandType.Trim().ToUpperInvariant();
        if (!allowed.Contains(type)) return BadRequest(new { message = "Comando remoto no permitido." });
        if (request.MaximumAttempts is < 1 or > 10) return BadRequest(new { message = "El máximo de intentos debe estar entre 1 y 10." });
        try { using var payload = JsonDocument.Parse(request.Payload); }
        catch (JsonException) { return BadRequest(new { message = "La carga de configuración debe ser un JSON válido." }); }
        var item = new RemoteConfigurationCommand { NodeId = request.NodeId, CommandType = type, Payload = request.Payload, MaximumAttempts = request.MaximumAttempts, RequestedByUserId = UserId() };
        db.RemoteConfigurationCommands.Add(item); await Save("IOT_REMOTE_CONFIGURATION_QUEUED", type, ct); if (remoteDispatcher is not null) await remoteDispatcher.DispatchAsync(item.Id, ct); return Ok(item);
    }

    [HttpPost("remote-configurations/{id:guid}/ack"), Authorize(Policy = PermissionPolicies.DevicesManage)]
    public async Task<IActionResult> ConfirmRemoteConfiguration(Guid id, RemoteConfigurationAckRequest request, CancellationToken ct)
    {
        var item = await db.RemoteConfigurationCommands.FindAsync([id], ct); if (item is null) return NotFound();
        item.Attempts++; item.LastAttemptAtUtc = DateTime.UtcNow; item.LastError = request.Success ? null : request.Detail;
        item.Status = request.Success ? "Confirmada" : item.Attempts >= item.MaximumAttempts ? "Fallida" : "Pendiente";
        if (request.Success) item.ConfirmedAtUtc = DateTime.UtcNow;
        await Save("IOT_REMOTE_CONFIGURATION_ACK", $"{id}:{item.Status}", ct); return Ok(item);
    }

    [HttpGet("firmware")]
    public async Task<IActionResult> Firmware(CancellationToken ct) => Ok(await db.FirmwareHistories.AsNoTracking()
        .OrderByDescending(x => x.RegisteredAtUtc)
        .Select(x => new { x.Id, x.NodeId, Node = x.Node.Name, x.Version, x.PreviousVersion, x.Status, x.RegisteredAtUtc, x.AppliedAtUtc, x.Notes })
        .ToListAsync(ct));

    [HttpPost("firmware"), Authorize(Policy = PermissionPolicies.DevicesManage)]
    public async Task<IActionResult> RegisterFirmware(FirmwareRegistrationRequest request, CancellationToken ct)
    {
        var node = await db.IoTNodes.FindAsync([request.NodeId], ct); if (node is null) return BadRequest(new { message = "Nodo inválido." });
        var status = request.Status.Trim();
        if (status is not ("Registrada" or "Pendiente de hardware" or "Aplicada")) return BadRequest(new { message = "Estado de firmware inválido." });
        var item = new FirmwareHistory { NodeId = node.Id, Version = request.Version.Trim(), PreviousVersion = node.FirmwareVersion, Status = status, AppliedAtUtc = status == "Aplicada" ? request.AppliedAtUtc ?? DateTime.UtcNow : null, Notes = request.Notes?.Trim() };
        if (status == "Aplicada") node.FirmwareVersion = item.Version;
        db.FirmwareHistories.Add(item); await Save("IOT_FIRMWARE_REGISTERED", $"{node.Code}:{item.Version}:{status}", ct); return Ok(item);
    }

    [HttpGet("inventory")]
    public async Task<IActionResult> Inventory(CancellationToken ct) => Ok(await db.IoTDevices.AsNoTracking().OrderBy(x => x.InventoryStatus).ThenBy(x => x.Name)
        .Select(x => new { x.Id, x.Code, x.Name, x.SerialNumber, Type = x.DeviceType.Name, x.Owner, x.InventoryStatus, x.PurchaseDate, x.WarrantyUntil, x.AcquisitionCost, x.Currency, x.IsActive, WarrantyExpired = x.WarrantyUntil.HasValue && x.WarrantyUntil < DateOnly.FromDateTime(DateTime.UtcNow) })
        .ToListAsync(ct));

    public static readonly string[] InventoryStatuses = ["Instalado", "Disponible", "Dañado", "En mantenimiento"];

    [HttpGet("inventory/statuses")]
    public IActionResult Statuses() => Ok(InventoryStatuses);

    [HttpPatch("inventory/{deviceId:guid}"), Authorize(Policy = PermissionPolicies.DevicesManage)]
    public async Task<IActionResult> UpdateInventory(Guid deviceId, InventoryUpdateRequest request, CancellationToken ct)
    {
        var item = await db.IoTDevices.FindAsync([deviceId], ct); if (item is null) return NotFound();
        if (request.AcquisitionCost < 0 || request.Currency.Trim().Length != 3) return BadRequest(new { message = "Datos de inventario inválidos." });
        // Antes bastaba con que el estado no fuese vacio: "instalado", "Dañada" o
        // cualquier texto entraban tal cual y ningun filtro los agrupaba despues.
        var status = InventoryStatuses.FirstOrDefault(x => string.Equals(x, request.InventoryStatus?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (status is null) return BadRequest(new { message = $"El estado de inventario debe ser uno de: {string.Join(", ", InventoryStatuses)}." });
        if (request.WarrantyUntil.HasValue && request.PurchaseDate.HasValue && request.WarrantyUntil < request.PurchaseDate) return BadRequest(new { message = "La garantía no puede vencer antes de la compra." });
        item.Owner = request.Owner?.Trim(); item.InventoryStatus = status; item.PurchaseDate = request.PurchaseDate; item.WarrantyUntil = request.WarrantyUntil; item.AcquisitionCost = request.AcquisitionCost; item.Currency = request.Currency.Trim().ToUpperInvariant(); item.UpdatedAtUtc = DateTime.UtcNow;
        await Save("IOT_INVENTORY_UPDATED", item.Code, ct); return NoContent();
    }

    private async Task<string?> ValidateInstallation(DeviceInstallationRequest r, Guid? id, CancellationToken ct)
    {
        if (!await db.IoTDevices.AnyAsync(x => x.Id == r.DeviceId, ct)) return "Dispositivo inválido.";
        if (r.IrrigationZoneId.HasValue && !await db.IrrigationZones.AnyAsync(x => x.Id == r.IrrigationZoneId, ct)) return "Zona inválida.";
        if (r.RemovedAtUtc.HasValue && r.RemovedAtUtc < r.InstalledAtUtc) return "La fecha de retiro no puede ser anterior a la instalación.";
        if (!r.RemovedAtUtc.HasValue && await db.DeviceInstallations.AnyAsync(x => x.Id != id && x.DeviceId == r.DeviceId && x.RemovedAtUtc == null, ct)) return "El dispositivo ya tiene una instalación activa.";
        return null;
    }

    private static DeviceInstallation Map(DeviceInstallationRequest r, DeviceInstallation x) { x.DeviceId = r.DeviceId; x.IrrigationZoneId = r.IrrigationZoneId; x.Location = r.Location.Trim(); x.InstalledAtUtc = r.InstalledAtUtc; x.InstallerName = r.InstallerName.Trim(); x.RemovedAtUtc = r.RemovedAtUtc; x.Notes = r.Notes?.Trim(); return x; }
    private Guid? UserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private async Task Save(string type, string detail, CancellationToken ct) { db.AccessAudits.Add(new AccessAudit { UserId = UserId(), EventType = type, Detail = detail }); await db.SaveChangesAsync(ct); }
}


