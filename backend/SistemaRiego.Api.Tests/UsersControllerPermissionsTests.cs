using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Controllers;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Tests;

public sealed class UsersControllerPermissionsTests
{
    [Fact]
    public async Task GetPermissions_MarksRoleAndOverrideSources()
    {
        var (controller, db, user, _, _) = await Seed();
        var result = await controller.Permissions(user.Id, default);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyCollection<EffectivePermissionResponse>>(ok.Value);

        var fromRole = items.Single(x => x.Code == PermissionCodes.IrrigationOperate);
        Assert.True(fromRole.GrantedByRole);
        Assert.Null(fromRole.OverrideIsGranted);
        Assert.True(fromRole.EffectiveGranted);
    }

    [Fact]
    public async Task UpdatePermissions_GrantOverride_AddsPermissionOutsideRole_AndRevokesSessions()
    {
        var (controller, db, user, actorId, databaseName) = await Seed();
        var activeSession = new Session { UserId = user.Id, RefreshTokenHash = "hash", ExpiresAtUtc = DateTime.UtcNow.AddDays(1) };
        db.Sessions.Add(activeSession);
        await db.SaveChangesAsync();

        var request = new UpdateUserPermissionsRequest([new(PermissionCodes.DeviceCatalogsDelete, true)]);
        var result = await controller.UpdatePermissions(user.Id, request, default);

        Assert.IsType<NoContentResult>(result);
        // Verificación con un contexto EF nuevo: leer con `db` (que ya trackea estas
        // entidades desde el seed) devolvería los valores originales sin refrescar,
        // ocultando que el controller sí las guardó en el almacenamiento compartido.
        await using var verifyDb = OpenDb(databaseName);
        Assert.True(await verifyDb.UserPermissions.AnyAsync(x => x.UserId == user.Id && x.IsGranted));
        Assert.True((await verifyDb.Sessions.SingleAsync(x => x.Id == activeSession.Id)).RevokedAtUtc != null);
        Assert.Contains(await verifyDb.AccessAudits.ToListAsync(), x => x.EventType == "USER_PERMISSIONS_CHANGED");
    }

    [Fact]
    public async Task UpdatePermissions_UnknownCode_ReturnsBadRequest()
    {
        var (controller, _, user, _, _) = await Seed();
        var request = new UpdateUserPermissionsRequest([new("codigo.inexistente", true)]);
        var result = await controller.UpdatePermissions(user.Id, request, default);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static AppDbContext OpenDb(string databaseName) => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName).Options);

    private static async Task<(UsersController Controller, AppDbContext Db, User User, Guid ActorId, string DatabaseName)> Seed()
    {
        var databaseName = Guid.NewGuid().ToString();
        var db = OpenDb(databaseName);
        var seedUserManager = BuildUserManager(db);

        var role = new Role { Name = RoleNames.Operator, NormalizedName = "OPERADOR" };
        var irrigatePermission = new Permission { Code = PermissionCodes.IrrigationOperate, Description = "Operar riego" };
        var deletePermission = new Permission { Code = PermissionCodes.DeviceCatalogsDelete, Description = "Eliminar catalogos" };
        db.Roles.Add(role); db.Permissions.AddRange(irrigatePermission, deletePermission);
        await db.SaveChangesAsync();
        role.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = irrigatePermission.Id });
        var user = new User { Email = "op@correo.gt", UserName = "op@correo.gt", FullName = "Operador" };
        await seedUserManager.CreateAsync(user, "Segura123!");
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        await db.SaveChangesAsync();

        // Contexto EF nuevo (misma base InMemory, con su propio UserManager) para que
        // el controller lea desde almacenamiento real, igual que un request de
        // produccion nuevo, en vez de reusar entidades ya trackeadas en `db` — eso
        // oculta un Include/ThenInclude faltante.
        var controllerDb = OpenDb(databaseName);
        var controllerUserManager = BuildUserManager(controllerDb);
        var controller = new UsersController(controllerDb, null!, null, controllerUserManager);
        var actorId = Guid.NewGuid();
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actorId.ToString())], "tests")) } };
        return (controller, db, user, actorId, databaseName);
    }

    private static UserManager<User> BuildUserManager(AppDbContext db)
    {
        var services = new ServiceCollection();
        services.AddLogging(); services.AddDataProtection(); services.AddSingleton(db);
        services.AddIdentityCore<User>().AddRoles<Role>().AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
        return services.BuildServiceProvider().GetRequiredService<UserManager<User>>();
    }
}
