using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Data;

namespace SistemaRiego.Api.Services;

public sealed class PermissionResolver(AppDbContext db) : IPermissionResolver
{
    public async Task<IReadOnlyCollection<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct)
    {
        var fromRoles = await db.UserRoles.Where(x => x.UserId == userId && x.Role.IsActive)
            .SelectMany(x => x.Role.RolePermissions.Select(y => y.Permission.Code))
            .ToListAsync(ct);
        var overrides = await db.UserPermissions.Where(x => x.UserId == userId)
            .Select(x => new { x.Permission.Code, x.IsGranted })
            .ToListAsync(ct);

        var effective = new HashSet<string>(fromRoles, StringComparer.OrdinalIgnoreCase);
        foreach (var o in overrides)
        {
            if (o.IsGranted) effective.Add(o.Code);
            else effective.Remove(o.Code);
        }
        return effective;
    }
}
