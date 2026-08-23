using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Tests;

public sealed class UserPermissionModelTests
{
    [Fact]
    public async Task UserPermission_PersistsOverride_WithCompositeKey()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new User { Email = "u@correo.gt", UserName = "u@correo.gt", FullName = "U" };
        var permission = new Permission { Code = "prueba.codigo", Description = "Prueba" };
        var actor = Guid.NewGuid();
        db.Users.Add(user); db.Permissions.Add(permission);
        await db.SaveChangesAsync();

        db.UserPermissions.Add(new UserPermission { UserId = user.Id, PermissionId = permission.Id, IsGranted = true, GrantedByUserId = actor });
        await db.SaveChangesAsync();

        var stored = await db.UserPermissions.SingleAsync();
        Assert.True(stored.IsGranted);
        Assert.Equal(actor, stored.GrantedByUserId);
    }
}
