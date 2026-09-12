using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/users"), Authorize(Policy = PermissionPolicies.UsersManage)]
public sealed class UsersController(AppDbContext db, IAuthService auth, ITotpService? totp, UserManager<User> userManager) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<UserSummary>>> GetAll(CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking().Include(x => x.UserRoles).ThenInclude(x => x.Role).OrderBy(x => x.FullName).ToListAsync(ct);
        return Ok(users.Select(x => new UserSummary(x.Id, x.Email ?? string.Empty, x.FullName, x.Status.ToString(), x.UserRoles.Select(y => y.Role.Name ?? string.Empty).Where(y => y.Length > 0).Order().ToArray(), x.MustChangePassword)));
    }

    // Sin este endpoint no habia forma de corregir un nombre mal escrito ni un
    // correo equivocado: el CRUD de usuarios solo permitia crear, cambiar estado y
    // asignar roles. Eliminar no se ofrece a proposito, porque auditoria, riegos y
    // sesiones referencian al usuario; para retirar a alguien se desactiva.
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateProfile(Guid id, UpdateUserProfileRequest request, CancellationToken ct)
    {
        if (!await VerifyTotp(ct)) return StatusCode(StatusCodes.Status403Forbidden, new { message = TotpError });
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null) return NotFound();

        var fullName = request.FullName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var normalized = email.ToUpperInvariant();
        if (fullName.Length < 3) return BadRequest(new { message = "El nombre debe tener al menos 3 caracteres." });
        if (await db.Users.AnyAsync(x => x.Id != id && x.NormalizedEmail == normalized, ct))
            return Conflict(new { message = "Ya existe otra cuenta con ese correo." });

        var previous = $"{user.FullName} <{user.Email}>";
        var emailChanged = !string.Equals(user.NormalizedEmail, normalized, StringComparison.Ordinal);
        user.FullName = fullName;
        user.Email = email;
        user.NormalizedEmail = normalized;
        user.UserName = email;
        user.NormalizedUserName = normalized;
        user.UpdatedAtUtc = DateTime.UtcNow;

        // Cambiar el correo cambia el identificador con el que se inicia sesion, asi
        // que las sesiones abiertas dejan de corresponder a la credencial vigente.
        if (emailChanged)
        {
            foreach (var session in await db.Sessions.Where(x => x.UserId == id && x.RevokedAtUtc == null).ToListAsync(ct)) session.RevokedAtUtc = DateTime.UtcNow;
            await userManager.UpdateSecurityStampAsync(user);
        }

        db.AccessAudits.Add(new AccessAudit { UserId = id, EventType = "USER_PROFILE_UPDATED", Detail = $"{previous} → {fullName} <{email}>{(emailChanged ? "; sesiones revocadas" : string.Empty)}" });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, UpdateUserStatusRequest request, CancellationToken ct)
    {
        if (!await VerifyTotp(ct)) return StatusCode(StatusCodes.Status403Forbidden, new { message = TotpError });
        var user = await db.Users.FindAsync([id], ct);
        if (user is null) return NotFound();
        user.Status = request.Status;
        user.UpdatedAtUtc = DateTime.UtcNow;
        if (request.Status != UserStatus.Active)
            foreach (var session in await db.Sessions.Where(x => x.UserId == id && x.RevokedAtUtc == null).ToListAsync(ct)) session.RevokedAtUtc = DateTime.UtcNow;
        db.AccessAudits.Add(new AccessAudit { UserId = id, EventType = "USER_STATUS_CHANGED", Detail = $"Estado: {request.Status}" });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/roles")]
    public async Task<IActionResult> Roles(Guid id, UpdateUserRolesRequest request, CancellationToken ct)
    {
        if (!await VerifyTotp(ct)) return StatusCode(StatusCodes.Status403Forbidden, new { message = TotpError });
        var user = await db.Users.Include(x => x.UserRoles).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null) return NotFound();
        var names = request.Roles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var roles = await db.Roles.Where(x => names.Contains(x.Name)).ToListAsync(ct);
        if (roles.Count != names.Length || roles.Count == 0) return BadRequest(new { message = "Debe indicar uno o más roles válidos." });
        db.UserRoles.RemoveRange(user.UserRoles);
        user.UserRoles = roles.Select(role => new UserRole { UserId = id, RoleId = role.Id }).ToList();
        db.AccessAudits.Add(new AccessAudit { UserId = id, EventType = "USER_ROLES_CHANGED", Detail = $"Roles: {string.Join(", ", roles.Select(x => x.Name))}" });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/permissions")]
    public async Task<ActionResult<IReadOnlyCollection<EffectivePermissionResponse>>> Permissions(Guid id, CancellationToken ct)
    {
        var user = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).ThenInclude(x => x.RolePermissions).ThenInclude(x => x.Permission).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null) return NotFound();
        var fromRoles = user.UserRoles.SelectMany(x => x.Role.RolePermissions.Select(y => y.Permission.Code)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var overrides = await db.UserPermissions.Include(x => x.Permission).Where(x => x.UserId == id)
            .ToDictionaryAsync(x => x.Permission.Code, x => x.IsGranted, StringComparer.OrdinalIgnoreCase, ct);
        var all = await db.Permissions.AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct);
        return Ok(all.Select(p =>
        {
            var byRole = fromRoles.Contains(p.Code);
            var hasOverride = overrides.TryGetValue(p.Code, out var overrideValue);
            return new EffectivePermissionResponse(p.Code, p.Description, byRole, hasOverride ? overrideValue : null, hasOverride ? overrideValue : byRole);
        }).ToArray());
    }

    [HttpPut("{id:guid}/permissions")]
    public async Task<IActionResult> UpdatePermissions(Guid id, UpdateUserPermissionsRequest request, CancellationToken ct)
    {
        if (!await VerifyTotp(ct)) return StatusCode(StatusCodes.Status403Forbidden, new { message = TotpError });
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null) return NotFound();
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();

        var codes = request.Overrides.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var permissions = await db.Permissions.Where(x => codes.Contains(x.Code)).ToListAsync(ct);
        if (permissions.Count != codes.Length) return BadRequest(new { message = "La lista contiene permisos desconocidos." });

        var existingByPermissionId = await db.UserPermissions.Where(x => x.UserId == id).ToDictionaryAsync(x => x.PermissionId, ct);
        foreach (var o in request.Overrides)
        {
            var permission = permissions.Single(x => string.Equals(x.Code, o.Code, StringComparison.OrdinalIgnoreCase));
            if (existingByPermissionId.TryGetValue(permission.Id, out var row))
            {
                row.IsGranted = o.IsGranted;
                row.GrantedByUserId = actorId;
                row.AssignedAtUtc = DateTime.UtcNow;
            }
            else db.UserPermissions.Add(new UserPermission { UserId = id, PermissionId = permission.Id, IsGranted = o.IsGranted, GrantedByUserId = actorId });
        }

        foreach (var session in await db.Sessions.Where(x => x.UserId == id && x.RevokedAtUtc == null).ToListAsync(ct)) session.RevokedAtUtc = DateTime.UtcNow;
        await userManager.UpdateSecurityStampAsync(user);

        db.AccessAudits.Add(new AccessAudit { UserId = id, EventType = "USER_PERMISSIONS_CHANGED", Detail = $"Overrides: {string.Join(", ", request.Overrides.Select(x => $"{x.Code}={x.IsGranted}"))}" });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<ActionResult<AdminPasswordResetResponse>> ResetPassword(Guid id, CancellationToken ct)
    {
        if (!await VerifyTotp(ct)) return StatusCode(StatusCodes.Status403Forbidden, new { message = TotpError });
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        try
        {
            var password = await auth.AdminResetPasswordAsync(actorId, id, Context(), ct);
            return password is null ? NotFound(new { message = "El usuario no existe." }) : Ok(new AdminPasswordResetResponse(password));
        }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }

    private string? TotpError { get; set; }
    private async Task<bool> VerifyTotp(CancellationToken ct)
    {
        if (totp is null) return true;
        var verification = await totp.VerifyCriticalOperationAsync(HttpContext, ct);
        TotpError = verification.Error;
        return verification.Allowed;
    }

    private AuthContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
}