using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/roles"), Authorize(Policy = Policies.Administrator)]
public sealed class RolesController(AppDbContext db, ITotpService? totp = null) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<RoleResponse>>> GetAll(CancellationToken ct)
    {
        var roles = await db.Roles.AsNoTracking().Include(x => x.RolePermissions).ThenInclude(x => x.Permission).OrderBy(x => x.Name).ToListAsync(ct);
        return Ok(roles.Select(x => new RoleResponse(x.Name ?? string.Empty, x.Description, x.RolePermissions.Select(y => y.Permission.Code).Order().ToArray())));
    }

    [HttpGet("permissions")]
    public async Task<ActionResult<IReadOnlyCollection<PermissionResponse>>> Permissions(CancellationToken ct) =>
        Ok(await db.Permissions.AsNoTracking().OrderBy(x => x.Code).Select(x => new PermissionResponse(x.Id, x.Code, x.Description)).ToListAsync(ct));

    [HttpPut("{roleName}/permissions")]
    public async Task<IActionResult> UpdatePermissions(string roleName, UpdateRolePermissionsRequest request, CancellationToken ct)
    {
        if (totp is not null)
        {
            var verification = await totp.VerifyCriticalOperationAsync(HttpContext, ct);
            if (!verification.Allowed) return StatusCode(StatusCodes.Status403Forbidden, new { message = verification.Error });
        }
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
}
