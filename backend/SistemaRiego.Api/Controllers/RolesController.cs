using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/roles"), Authorize(Policy = PermissionPolicies.RolesManage)]
public sealed class RolesController(AppDbContext db, ITotpService? totp = null) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<RoleResponse>>> GetAll(CancellationToken ct)
    {
        var roles = await db.Roles.AsNoTracking().Include(x => x.UserRoles).Include(x => x.RolePermissions).ThenInclude(x => x.Permission).OrderBy(x => x.Name).ToListAsync(ct);
        return Ok(roles.Select(ToResponse));
    }

    [HttpPost]
    public async Task<ActionResult<RoleResponse>> Create(SaveRoleRequest request, CancellationToken ct)
    {
        if (!await VerifyTotp(ct)) return StatusCode(StatusCodes.Status403Forbidden, new { message = TotpError });
        var name = request.Name.Trim();
        var normalized = name.ToUpperInvariant();
        if (await db.Roles.AnyAsync(x => x.NormalizedName == normalized, ct)) return Conflict(new { message = "Ya existe un rol con ese nombre." });
        var role = new Role { Name = name, NormalizedName = normalized, Description = request.Description.Trim(), IsActive = request.IsActive, ConcurrencyStamp = Guid.NewGuid().ToString() };
        db.Roles.Add(role);
        db.AccessAudits.Add(new AccessAudit { EventType = "ROLE_CREATED", Detail = $"Rol: {name}; estado: {(role.IsActive ? "activo" : "inactivo")}" });
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetAll), new { id = role.Id }, ToResponse(role));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RoleResponse>> Update(Guid id, SaveRoleRequest request, CancellationToken ct)
    {
        if (!await VerifyTotp(ct)) return StatusCode(StatusCodes.Status403Forbidden, new { message = TotpError });
        var role = await db.Roles.Include(x => x.UserRoles).Include(x => x.RolePermissions).ThenInclude(x => x.Permission).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (role is null) return NotFound();
        var name = request.Name.Trim();
        var normalized = name.ToUpperInvariant();
        if (await db.Roles.AnyAsync(x => x.Id != id && x.NormalizedName == normalized, ct)) return Conflict(new { message = "Ya existe un rol con ese nombre." });
        if (IsAdministrator(role) && (!request.IsActive || !string.Equals(name, RoleNames.Administrator, StringComparison.Ordinal))) return BadRequest(new { message = "El rol Administrador no puede renombrarse ni desactivarse." });
        var previousName = role.Name;
        role.Name = name; role.NormalizedName = normalized; role.Description = request.Description.Trim(); role.IsActive = request.IsActive; role.ConcurrencyStamp = Guid.NewGuid().ToString();
        if (!role.IsActive) foreach (var session in await db.Sessions.Where(x => role.UserRoles.Select(y => y.UserId).Contains(x.UserId) && x.RevokedAtUtc == null).ToListAsync(ct)) session.RevokedAtUtc = DateTime.UtcNow;
        db.AccessAudits.Add(new AccessAudit { EventType = "ROLE_UPDATED", Detail = $"{previousName} → {name}; estado: {(role.IsActive ? "activo" : "inactivo")}" });
        await db.SaveChangesAsync(ct);
        return Ok(ToResponse(role));
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, UpdateRoleStatusRequest request, CancellationToken ct)
    {
        if (!await VerifyTotp(ct)) return StatusCode(StatusCodes.Status403Forbidden, new { message = TotpError });
        var role = await db.Roles.Include(x => x.UserRoles).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (role is null) return NotFound();
        if (IsAdministrator(role) && !request.IsActive) return BadRequest(new { message = "El rol Administrador no puede desactivarse." });
        role.IsActive = request.IsActive; role.ConcurrencyStamp = Guid.NewGuid().ToString();
        if (!role.IsActive) foreach (var session in await db.Sessions.Where(x => role.UserRoles.Select(y => y.UserId).Contains(x.UserId) && x.RevokedAtUtc == null).ToListAsync(ct)) session.RevokedAtUtc = DateTime.UtcNow;
        db.AccessAudits.Add(new AccessAudit { EventType = "ROLE_STATUS_CHANGED", Detail = $"{role.Name}: {(role.IsActive ? "activo" : "inactivo")}" });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!await VerifyTotp(ct)) return StatusCode(StatusCodes.Status403Forbidden, new { message = TotpError });
        var role = await db.Roles.Include(x => x.UserRoles).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (role is null) return NotFound();
        if (IsAdministrator(role)) return BadRequest(new { message = "El rol Administrador no puede eliminarse." });
        if (role.UserRoles.Count > 0) return Conflict(new { message = $"No se puede eliminar porque está asignado a {role.UserRoles.Count} usuario(s). Reasígnalos primero." });
        db.Roles.Remove(role);
        db.AccessAudits.Add(new AccessAudit { EventType = "ROLE_DELETED", Detail = $"Rol: {role.Name}" });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("permissions")]
    public async Task<ActionResult<IReadOnlyCollection<PermissionResponse>>> Permissions(CancellationToken ct) => Ok(await db.Permissions.AsNoTracking().OrderBy(x => x.Code).Select(x => new PermissionResponse(x.Id, x.Code, x.Description)).ToListAsync(ct));

    [HttpPut("{roleName}/permissions")]
    public async Task<IActionResult> UpdatePermissions(string roleName, UpdateRolePermissionsRequest request, CancellationToken ct)
    {
        if (!await VerifyTotp(ct)) return StatusCode(StatusCodes.Status403Forbidden, new { message = TotpError });
        var role = await db.Roles.Include(x => x.RolePermissions).SingleOrDefaultAsync(x => x.Name == roleName, ct);
        if (role is null) return NotFound();
        var codes = request.Permissions.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var permissions = await db.Permissions.Where(x => codes.Contains(x.Code)).ToListAsync(ct);
        if (permissions.Count != codes.Length) return BadRequest(new { message = "La matriz contiene permisos desconocidos." });
        db.RolePermissions.RemoveRange(role.RolePermissions);
        role.RolePermissions = permissions.Select(x => new RolePermission { RoleId = role.Id, PermissionId = x.Id }).ToList();
        db.AccessAudits.Add(new AccessAudit { EventType = "ROLE_PERMISSIONS_CHANGED", Detail = $"{role.Name}: {string.Join(", ", codes)}" });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private string? TotpError { get; set; }
    private async Task<bool> VerifyTotp(CancellationToken ct)
    {
        if (totp is null) return true;
        var verification = await totp.VerifyCriticalOperationAsync(HttpContext, ct);
        TotpError = verification.Error;
        return verification.Allowed;
    }
    private static bool IsAdministrator(Role role) => string.Equals(role.NormalizedName, RoleNames.Administrator.ToUpperInvariant(), StringComparison.Ordinal);
    private static RoleResponse ToResponse(Role role) => new(role.Id, role.Name ?? string.Empty, role.Description, role.IsActive, role.UserRoles.Count, role.RolePermissions.Select(x => x.Permission.Code).Order().ToArray());
}