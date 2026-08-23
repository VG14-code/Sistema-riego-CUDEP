using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Tests;

public sealed class PermissionResolverTests
{
    [Fact]
    public async Task Effective_CombinesRolePermissions_WithGrantOverride()
    {
        var db = NewDb();
        var permission = new Permission { Code = "extra.permiso", Description = "Extra" };
        var role = new Role { Name = "Operador", NormalizedName = "OPERADOR" };
        var user = new User { Email = "u@correo.gt", UserName = "u@correo.gt", FullName = "U" };
        db.Permissions.Add(permission); db.Roles.Add(role); db.Users.Add(user);
        await db.SaveChangesAsync();
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        db.UserPermissions.Add(new UserPermission { UserId = user.Id, PermissionId = permission.Id, IsGranted = true, GrantedByUserId = user.Id });
        await db.SaveChangesAsync();

        var effective = await new PermissionResolver(db).GetEffectivePermissionsAsync(user.Id, default);

        Assert.Contains("extra.permiso", effective);
    }

    [Fact]
    public async Task Effective_RevokeOverride_RemovesRolePermission()
    {
        var db = NewDb();
        var permission = new Permission { Code = "riego.operar", Description = "Operar riego" };
        var role = new Role { Name = "Operador", NormalizedName = "OPERADOR" };
        var user = new User { Email = "u2@correo.gt", UserName = "u2@correo.gt", FullName = "U2" };
        db.Permissions.Add(permission); db.Roles.Add(role); db.Users.Add(user);
        await db.SaveChangesAsync();
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        role.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        db.UserPermissions.Add(new UserPermission { UserId = user.Id, PermissionId = permission.Id, IsGranted = false, GrantedByUserId = user.Id });
        await db.SaveChangesAsync();

        var effective = await new PermissionResolver(db).GetEffectivePermissionsAsync(user.Id, default);

        Assert.DoesNotContain("riego.operar", effective);
    }

    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
