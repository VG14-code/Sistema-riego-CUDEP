using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/catalogs"), Authorize(Policy = Policies.Operator)]
public sealed class MasterDataController(AppDbContext db) : ControllerBase
{
    [HttpGet("{kind}")]
    public async Task<ActionResult<IReadOnlyCollection<CatalogItemResponse>>> Get(string kind, CancellationToken ct)
    {
        if (!TryKind(kind, out var parsed)) return BadRequest(new { message = "Tipo de catálogo inválido." });
        var items = await db.MasterCatalogItems.AsNoTracking().Where(x => x.Kind == parsed).OrderBy(x => x.Name).ToListAsync(ct);
        return Ok(items.Select(ToResponse));
    }

    [HttpPost("{kind}"), Authorize(Policy = Policies.Technician)]
    public async Task<ActionResult<CatalogItemResponse>> Create(string kind, CatalogItemRequest request, CancellationToken ct)
    {
        if (!TryKind(kind, out var parsed)) return BadRequest(new { message = "Tipo de catálogo inválido." });
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.MasterCatalogItems.AnyAsync(x => x.Kind == parsed && x.Code == code, ct)) return Conflict(new { message = "El código ya existe en este catálogo." });
        var item = new MasterCatalogItem { Kind = parsed, Code = code, Name = request.Name.Trim(), Description = request.Description?.Trim(), Symbol = request.Symbol?.Trim(), IsActive = request.IsActive };
        db.MasterCatalogItems.Add(item);
        db.AccessAudits.Add(new AccessAudit { UserId = CurrentUserId(), EventType = "CATALOG_CREATED", Detail = $"{parsed}: {code}" });
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { kind }, ToResponse(item));
    }

    [HttpPut("{kind}/{id:guid}"), Authorize(Policy = Policies.Technician)]
    public async Task<ActionResult<CatalogItemResponse>> Update(string kind, Guid id, CatalogItemRequest request, CancellationToken ct)
    {
        if (!TryKind(kind, out var parsed)) return BadRequest(new { message = "Tipo de catálogo inválido." });
        var item = await db.MasterCatalogItems.SingleOrDefaultAsync(x => x.Id == id && x.Kind == parsed, ct);
        if (item is null) return NotFound();
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.MasterCatalogItems.AnyAsync(x => x.Kind == parsed && x.Code == code && x.Id != id, ct)) return Conflict(new { message = "El código ya existe en este catálogo." });
        item.Code = code; item.Name = request.Name.Trim(); item.Description = request.Description?.Trim(); item.Symbol = request.Symbol?.Trim(); item.IsActive = request.IsActive; item.UpdatedAtUtc = DateTime.UtcNow;
        db.AccessAudits.Add(new AccessAudit { UserId = CurrentUserId(), EventType = "CATALOG_UPDATED", Detail = $"{parsed}: {code}" });
        await db.SaveChangesAsync(ct);
        return Ok(ToResponse(item));
    }

    [HttpDelete("{kind}/{id:guid}"), Authorize(Policy = Policies.Administrator)]
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

    private Guid? CurrentUserId() => Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
    private static bool TryKind(string value, out CatalogKind kind) => Enum.TryParse(value, true, out kind) && Enum.IsDefined(kind);
    private static CatalogItemResponse ToResponse(MasterCatalogItem x) => new(x.Id, x.Kind, x.Code, x.Name, x.Description, x.Symbol, x.IsActive);
}
