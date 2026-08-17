using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/device-catalogs"), Authorize(Policy = Policies.Operator)]
public sealed class DeviceCatalogsController(AppDbContext db) : ControllerBase
{
    [HttpGet("brands")]
    public async Task<IActionResult> Brands(CancellationToken ct) => Ok(await db.DeviceBrands.AsNoTracking().OrderBy(x => x.Name)
        .Select(x => new DeviceBrandResponse(x.Id, x.Code, x.Name, x.Description, x.IsActive, x.Models.Count)).ToListAsync(ct));

    [HttpPost("brands"), Authorize(Policy = Policies.Technician)]
    public async Task<IActionResult> CreateBrand(DeviceBrandRequest request, CancellationToken ct)
    {
        var code = Code(request.Code);
        if (await db.DeviceBrands.AnyAsync(x => x.Code == code, ct)) return Conflict(Message("La marca ya existe."));
        var item = new DeviceBrand { Code = code, Name = request.Name.Trim(), Description = Trim(request.Description), IsActive = request.IsActive };
        db.Add(item); await Save("DEVICE_BRAND_CREATED", code, ct); return Ok(item);
    }

    [HttpPut("brands/{id:guid}"), Authorize(Policy = Policies.Technician)]
    public async Task<IActionResult> UpdateBrand(Guid id, DeviceBrandRequest request, CancellationToken ct)
    {
        var item = await db.DeviceBrands.FindAsync([id], ct); if (item is null) return NotFound();
        var code = Code(request.Code);
        if (await db.DeviceBrands.AnyAsync(x => x.Id != id && x.Code == code, ct)) return Conflict(Message("La marca ya existe."));
        item.Code = code; item.Name = request.Name.Trim(); item.Description = Trim(request.Description); item.IsActive = request.IsActive;
        await Save("DEVICE_BRAND_UPDATED", code, ct); return NoContent();
    }

    [HttpDelete("brands/{id:guid}"), Authorize(Policy = Policies.Administrator)]
    public async Task<IActionResult> DeleteBrand(Guid id, CancellationToken ct)
    {
        var item = await db.DeviceBrands.Include(x => x.Models).SingleOrDefaultAsync(x => x.Id == id, ct); if (item is null) return NotFound();
        if (item.Models.Count != 0) return Conflict(Message("No se puede eliminar una marca con modelos asociados."));
        db.Remove(item); await Save("DEVICE_BRAND_DELETED", item.Code, ct); return NoContent();
    }

    [HttpGet("models")]
    public async Task<IActionResult> Models(CancellationToken ct) => Ok(await db.DeviceModels.AsNoTracking().OrderBy(x => x.DeviceBrand.Name).ThenBy(x => x.Name)
        .Select(x => new DeviceModelResponse(x.Id, x.DeviceBrandId, x.DeviceBrand.Name, x.DeviceTypeId, x.DeviceType.Name, x.Code, x.Name, x.Description, x.IsActive, x.Devices.Count)).ToListAsync(ct));

    [HttpPost("models"), Authorize(Policy = Policies.Technician)]
    public async Task<IActionResult> CreateModel(DeviceModelRequest request, CancellationToken ct)
    {
        var error = await ValidateModel(request, null, ct); if (error is not null) return BadRequest(Message(error));
        var item = new DeviceModel { DeviceBrandId = request.DeviceBrandId, DeviceTypeId = request.DeviceTypeId, Code = Code(request.Code), Name = request.Name.Trim(), Description = Trim(request.Description), IsActive = request.IsActive };
        db.Add(item); await Save("DEVICE_MODEL_CREATED", item.Code, ct); return Ok(item);
    }

    [HttpPut("models/{id:guid}"), Authorize(Policy = Policies.Technician)]
    public async Task<IActionResult> UpdateModel(Guid id, DeviceModelRequest request, CancellationToken ct)
    {
        var item = await db.DeviceModels.FindAsync([id], ct); if (item is null) return NotFound();
        var error = await ValidateModel(request, id, ct); if (error is not null) return BadRequest(Message(error));
        item.DeviceBrandId = request.DeviceBrandId; item.DeviceTypeId = request.DeviceTypeId; item.Code = Code(request.Code); item.Name = request.Name.Trim(); item.Description = Trim(request.Description); item.IsActive = request.IsActive;
        await Save("DEVICE_MODEL_UPDATED", item.Code, ct); return NoContent();
    }

    [HttpDelete("models/{id:guid}"), Authorize(Policy = Policies.Administrator)]
    public async Task<IActionResult> DeleteModel(Guid id, CancellationToken ct)
    {
        var item = await db.DeviceModels.Include(x => x.Devices).SingleOrDefaultAsync(x => x.Id == id, ct); if (item is null) return NotFound();
        if (item.Devices.Count != 0) return Conflict(Message("No se puede eliminar un modelo utilizado por dispositivos."));
        db.Remove(item); await Save("DEVICE_MODEL_DELETED", item.Code, ct); return NoContent();
    }

    private async Task<string?> ValidateModel(DeviceModelRequest request, Guid? currentId, CancellationToken ct)
    {
        if (!await db.DeviceBrands.AnyAsync(x => x.Id == request.DeviceBrandId && x.IsActive, ct)) return "Marca inválida.";
        if (!await db.MasterCatalogItems.AnyAsync(x => x.Id == request.DeviceTypeId && x.Kind == CatalogKind.DeviceType && x.IsActive, ct)) return "Tipo de dispositivo inválido.";
        var code = Code(request.Code);
        if (await db.DeviceModels.AnyAsync(x => x.Id != currentId && x.Code == code, ct)) return "El código del modelo ya existe.";
        return null;
    }

    private async Task Save(string type, string detail, CancellationToken ct) { db.AccessAudits.Add(new AccessAudit { EventType = type, Detail = detail }); await db.SaveChangesAsync(ct); }
    private static object Message(string message) => new { message };
    private static string Code(string value) => value.Trim().ToUpperInvariant().Replace(' ', '_');
    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
