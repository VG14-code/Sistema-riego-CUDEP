using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/territory"), Authorize(Policy = Policies.Technician)]
public sealed class TerritoryMaintenanceController(AppDbContext db) : ControllerBase
{
    [HttpPut("centers/{id:guid}")]
    public async Task<IActionResult> UpdateCenter(Guid id, CenterRequest request, CancellationToken ct)
    {
        var item = await db.UniversityCenters.FindAsync([id], ct);
        if (item is null) return NotFound(Message("El centro no existe."));
        var code = Code(request.Code);
        if (await db.UniversityCenters.AnyAsync(x => x.Id != id && x.Code == code, ct)) return Conflict(Message("El código del centro ya existe."));
        item.Code = code; item.Name = request.Name.Trim(); item.Location = Trim(request.Location); item.Contact = Trim(request.Contact); item.IsActive = request.IsActive;
        Audit("CENTER_UPDATED", item.Code); await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpPut("farms/{id:guid}")]
    public async Task<IActionResult> UpdateFarm(Guid id, FarmRequest request, CancellationToken ct)
    {
        var item = await db.Farms.FindAsync([id], ct);
        if (item is null) return NotFound(Message("La finca no existe."));
        if (!await db.UniversityCenters.AnyAsync(x => x.Id == request.UniversityCenterId, ct)) return BadRequest(Message("El centro seleccionado no existe."));
        var code = Code(request.Code);
        if (await db.Farms.AnyAsync(x => x.Id != id && x.Code == code, ct)) return Conflict(Message("El código de la finca ya existe."));
        item.UniversityCenterId = request.UniversityCenterId; item.Code = code; item.Name = request.Name.Trim(); item.Location = Trim(request.Location); item.Latitude = request.Latitude; item.Longitude = request.Longitude; item.IsActive = request.IsActive;
        Audit("FARM_UPDATED", item.Code); await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpPut("blocks/{id:guid}")]
    public async Task<IActionResult> UpdateBlock(Guid id, BlockRequest request, CancellationToken ct)
    {
        var item = await db.FarmBlocks.FindAsync([id], ct);
        if (item is null) return NotFound(Message("El bloque no existe."));
        if (request.AreaHectares <= 0) return BadRequest(Message("El área debe ser mayor que cero."));
        if (!await db.Farms.AnyAsync(x => x.Id == request.FarmId, ct)) return BadRequest(Message("La finca seleccionada no existe."));
        if (request.SoilTypeId is Guid soilId && !await db.SoilTypes.AnyAsync(x => x.Id == soilId, ct)) return BadRequest(Message("El tipo de suelo seleccionado no existe."));
        var code = Code(request.Code);
        if (await db.FarmBlocks.AnyAsync(x => x.Id != id && x.Code == code, ct)) return Conflict(Message("El código del bloque ya existe."));
        item.FarmId = request.FarmId; item.SoilTypeId = request.SoilTypeId; item.Code = code; item.Name = request.Name.Trim(); item.AreaHectares = request.AreaHectares; item.Description = Trim(request.Description); item.IsActive = request.IsActive;
        Audit("BLOCK_UPDATED", item.Code); await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpPut("sectors/{id:guid}")]
    public async Task<IActionResult> UpdateSector(Guid id, SectorRequest request, CancellationToken ct)
    {
        var item = await db.IrrigationSectors.FindAsync([id], ct);
        if (item is null) return NotFound(Message("El sector no existe."));
        if (request.AreaHectares <= 0) return BadRequest(Message("El área debe ser mayor que cero."));
        if (!await db.FarmBlocks.AnyAsync(x => x.Id == request.FarmBlockId, ct)) return BadRequest(Message("El bloque seleccionado no existe."));
        var code = Code(request.Code);
        if (await db.IrrigationSectors.AnyAsync(x => x.Id != id && x.Code == code, ct)) return Conflict(Message("El código del sector ya existe."));
        item.FarmBlockId = request.FarmBlockId; item.Code = code; item.Name = request.Name.Trim(); item.AreaHectares = request.AreaHectares; item.SlopePercent = request.SlopePercent; item.IsActive = request.IsActive;
        Audit("SECTOR_UPDATED", item.Code); await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpPut("zones/{id:guid}")]
    public async Task<IActionResult> UpdateZone(Guid id, ZoneRequest request, CancellationToken ct)
    {
        var item = await db.IrrigationZones.FindAsync([id], ct);
        if (item is null) return NotFound(Message("La zona no existe."));
        if (request.AreaHectares <= 0) return BadRequest(Message("El área debe ser mayor que cero."));
        if (!await db.IrrigationSectors.AnyAsync(x => x.Id == request.IrrigationSectorId, ct)) return BadRequest(Message("El sector seleccionado no existe."));
        if (!await db.MasterCatalogItems.AnyAsync(x => x.Id == request.OperationalStatusId && x.Kind == CatalogKind.OperationalStatus, ct)) return BadRequest(Message("El estado operativo no existe."));
        if (request.PrimarySensorId is Guid sensorId && !await db.IoTSensors.AnyAsync(x => x.Id == sensorId, ct)) return BadRequest(Message("El sensor principal no existe."));
        if (request.ValveDeviceId is Guid valveId && !await db.IoTDevices.AnyAsync(x => x.Id == valveId, ct)) return BadRequest(Message("El dispositivo de válvula no existe."));
        var code = Code(request.Code);
        if (await db.IrrigationZones.AnyAsync(x => x.Id != id && x.Code == code, ct)) return Conflict(Message("El código de la zona ya existe."));
        item.IrrigationSectorId = request.IrrigationSectorId; item.Code = code; item.Name = request.Name.Trim(); item.AreaHectares = request.AreaHectares; item.OperationalStatusId = request.OperationalStatusId; item.PrimarySensorId = request.PrimarySensorId; item.ValveDeviceId = request.ValveDeviceId; item.Latitude = request.Latitude; item.Longitude = request.Longitude; item.IsActive = request.IsActive;
        Audit("ZONE_UPDATED", item.Code); await db.SaveChangesAsync(ct); return NoContent();
    }

    private void Audit(string type, string detail) => db.AccessAudits.Add(new AccessAudit { EventType = type, Detail = detail });
    private static object Message(string value) => new { message = value };
    private static string Code(string value) => value.Trim().ToUpperInvariant().Replace(' ', '_');
    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
