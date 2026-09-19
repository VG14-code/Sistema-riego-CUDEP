using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/catalogs"), Authorize(Policy = PermissionPolicies.CatalogsRead)]
public sealed class MasterDataController(AppDbContext db) : ControllerBase
{
    [HttpGet("{kind}")]
    public async Task<ActionResult<IReadOnlyCollection<CatalogItemResponse>>> Get(string kind, CancellationToken ct)
    {
        if (!TryKind(kind, out var parsed)) return BadRequest(new { message = "Tipo de catálogo inválido." });
        var items = await db.MasterCatalogItems.AsNoTracking().Where(x => x.Kind == parsed).OrderBy(x => x.Name).ToListAsync(ct);
        return Ok(items.Select(ToResponse));
    }

    [HttpPost("{kind}"), Authorize(Policy = PermissionPolicies.CatalogsManage)]
    public async Task<ActionResult<CatalogItemResponse>> Create(string kind, CatalogItemRequest request, CancellationToken ct)
    {
        if (!TryKind(kind, out var parsed)) return BadRequest(new { message = "Tipo de catálogo inválido." });
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.MasterCatalogItems.AnyAsync(x => x.Kind == parsed && x.Code == code, ct)) return Conflict(new { message = "El código ya existe en este catálogo." });
        var conversionError = ValidateConversion(parsed, request); if (conversionError is not null) return BadRequest(new { message = conversionError });
        var item = new MasterCatalogItem { Kind = parsed, Code = code, Name = request.Name.Trim(), Description = request.Description?.Trim(), Symbol = request.Symbol?.Trim(), IsActive = request.IsActive, BaseUnitCode = parsed == CatalogKind.MeasurementUnit ? NormalizeOptionalCode(request.BaseUnitCode ?? code) : null, ConversionFactorToBase = parsed == CatalogKind.MeasurementUnit ? request.ConversionFactorToBase ?? 1 : null };
        db.MasterCatalogItems.Add(item);
        db.AccessAudits.Add(new AccessAudit { UserId = CurrentUserId(), EventType = "CATALOG_CREATED", Detail = $"{parsed}: {code}" });
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { kind }, ToResponse(item));
    }

    [HttpPut("{kind}/{id:guid}"), Authorize(Policy = PermissionPolicies.CatalogsManage)]
    public async Task<ActionResult<CatalogItemResponse>> Update(string kind, Guid id, CatalogItemRequest request, CancellationToken ct)
    {
        if (!TryKind(kind, out var parsed)) return BadRequest(new { message = "Tipo de catálogo inválido." });
        var item = await db.MasterCatalogItems.SingleOrDefaultAsync(x => x.Id == id && x.Kind == parsed, ct);
        if (item is null) return NotFound();
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.MasterCatalogItems.AnyAsync(x => x.Kind == parsed && x.Code == code && x.Id != id, ct)) return Conflict(new { message = "El código ya existe en este catálogo." });
        var conversionError = ValidateConversion(parsed, request); if (conversionError is not null) return BadRequest(new { message = conversionError });
        item.Code = code; item.Name = request.Name.Trim(); item.Description = request.Description?.Trim(); item.Symbol = request.Symbol?.Trim(); item.IsActive = request.IsActive; item.BaseUnitCode = parsed == CatalogKind.MeasurementUnit ? NormalizeOptionalCode(request.BaseUnitCode ?? code) : null; item.ConversionFactorToBase = parsed == CatalogKind.MeasurementUnit ? request.ConversionFactorToBase ?? 1 : null; item.UpdatedAtUtc = DateTime.UtcNow;
        db.AccessAudits.Add(new AccessAudit { UserId = CurrentUserId(), EventType = "CATALOG_UPDATED", Detail = $"{parsed}: {code}" });
        await db.SaveChangesAsync(ct);
        return Ok(ToResponse(item));
    }

    [HttpDelete("{kind}/{id:guid}"), Authorize(Policy = PermissionPolicies.CatalogsDelete)]
    public async Task<IActionResult> Delete(string kind, Guid id, CancellationToken ct)
    {
        if (!TryKind(kind, out var parsed)) return BadRequest(new { message = "Tipo de catálogo inválido." });
        var item = await db.MasterCatalogItems.SingleOrDefaultAsync(x => x.Id == id && x.Kind == parsed, ct);
        if (item is null) return NotFound();
        db.MasterCatalogItems.Remove(item);
        db.AccessAudits.Add(new AccessAudit { UserId = CurrentUserId(), EventType = "CATALOG_DELETED", Detail = $"{parsed}: {item.Code}" });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("measurement-units/convert")]
    public async Task<ActionResult<UnitConversionResponse>> Convert(UnitConversionRequest request, CancellationToken ct)
    {
        var units = await db.MasterCatalogItems.AsNoTracking().Where(x => (x.Id == request.FromUnitId || x.Id == request.ToUnitId) && x.Kind == CatalogKind.MeasurementUnit && x.IsActive).ToListAsync(ct);
        var from = units.SingleOrDefault(x => x.Id == request.FromUnitId); var to = units.SingleOrDefault(x => x.Id == request.ToUnitId);
        if (from is null || to is null) return BadRequest(new { message = "Debe seleccionar unidades activas y válidas." });
        if (string.IsNullOrWhiteSpace(from.BaseUnitCode) || !string.Equals(from.BaseUnitCode, to.BaseUnitCode, StringComparison.OrdinalIgnoreCase)) return BadRequest(new { message = "Las unidades no pertenecen a la misma magnitud." });
        if (from.ConversionFactorToBase is null or <= 0 || to.ConversionFactorToBase is null or <= 0) return BadRequest(new { message = "Las unidades no tienen factores de conversión válidos." });
        var converted = decimal.Round(request.Value * from.ConversionFactorToBase.Value / to.ConversionFactorToBase.Value, 8, MidpointRounding.AwayFromZero);
        return Ok(new UnitConversionResponse(request.Value, from.Symbol ?? from.Name, converted, to.Symbol ?? to.Name));
    }

    private static string? ValidateConversion(CatalogKind kind, CatalogItemRequest request) => kind == CatalogKind.MeasurementUnit && request.ConversionFactorToBase is <= 0 ? "El factor de conversión debe ser mayor que cero." : null;
    private static string? NormalizeOptionalCode(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant().Replace(' ', '_');
    private Guid? CurrentUserId() => Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
    private static bool TryKind(string value, out CatalogKind kind) => Enum.TryParse(value, true, out kind) && Enum.IsDefined(kind);
    private static CatalogItemResponse ToResponse(MasterCatalogItem x) => new(x.Id, x.Kind, x.Code, x.Name, x.Description, x.Symbol, x.IsActive, x.BaseUnitCode, x.ConversionFactorToBase);
}
