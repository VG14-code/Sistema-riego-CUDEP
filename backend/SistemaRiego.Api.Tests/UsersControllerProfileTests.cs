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

namespace SistemaRiego.Api.Tests;

// El CRUD de usuarios permitia crear, cambiar estado y asignar roles, pero no
// corregir un nombre mal escrito ni un correo equivocado.
public sealed class UsersControllerProfileTests
{
    // La pantalla manda el nombre del estado; con el enum el enlace de modelos
    // respondia 400 y no se podia bloquear a nadie desde el panel.
    [Fact]
    public async Task Status_AcceptsTheStateNameAndClosesTheOpenSessions()
    {
        var (controller, user, databaseName) = await Seed();
        await using (var db = OpenDb(databaseName))
        {
            db.Sessions.Add(new Session { UserId = user.Id, RefreshTokenHash = "hash", ExpiresAtUtc = DateTime.UtcNow.AddDays(1) });
            await db.SaveChangesAsync();
        }

        var resultado = await controller.Status(user.Id, new UpdateUserStatusRequest("Blocked"), default);

        Assert.IsType<NoContentResult>(resultado);
        await using var fresh = OpenDb(databaseName);
        Assert.Equal(UserStatus.Blocked, (await fresh.Users.SingleAsync(x => x.Id == user.Id)).Status);
        Assert.All(await fresh.Sessions.ToListAsync(), x => Assert.NotNull(x.RevokedAtUtc));
        Assert.Contains(await fresh.AccessAudits.ToListAsync(), x => x.EventType == "USER_STATUS_CHANGED" && x.Detail.Contains("Blocked"));
    }

    [Fact]
    public async Task Status_RejectsAnUnknownState()
    {
        var (controller, user, databaseName) = await Seed();

        var resultado = await controller.Status(user.Id, new UpdateUserStatusRequest("Congelado"), default);

        Assert.IsType<BadRequestObjectResult>(resultado);
        await using var fresh = OpenDb(databaseName);
        Assert.Equal(UserStatus.Active, (await fresh.Users.SingleAsync(x => x.Id == user.Id)).Status);
    }

    [Fact]
    public async Task UpdateProfile_ChangesNameAndEmail_AndRecordsThePreviousValue()
    {
        var (controller, user, databaseName) = await Seed();

        var result = await controller.UpdateProfile(user.Id, new UpdateUserProfileRequest("  Ana María Pérez  ", "  Ana.Perez@Correo.GT  "), default);

        Assert.IsType<NoContentResult>(result);
        await using var fresh = OpenDb(databaseName);
        var stored = await fresh.Users.SingleAsync(x => x.Id == user.Id);
        Assert.Equal("Ana María Pérez", stored.FullName);
        Assert.Equal("ana.perez@correo.gt", stored.Email);
        // Identity busca por el valor normalizado: sin actualizarlo, el usuario no
        // podria volver a iniciar sesion con su correo nuevo.
        Assert.Equal("ANA.PEREZ@CORREO.GT", stored.NormalizedEmail);
        Assert.Equal("ana.perez@correo.gt", stored.UserName);
        Assert.Equal("ANA.PEREZ@CORREO.GT", stored.NormalizedUserName);
        var audit = await fresh.AccessAudits.SingleAsync(x => x.EventType == "USER_PROFILE_UPDATED");
        Assert.Contains("op@correo.gt", audit.Detail);
        Assert.Contains("ana.perez@correo.gt", audit.Detail);
    }

    [Fact]
    public async Task UpdateProfile_WhenEmailChanges_RevokesOpenSessions()
    {
        var (controller, user, databaseName) = await Seed();
        await using (var setup = OpenDb(databaseName))
        {
            setup.Sessions.Add(new Session { UserId = user.Id, RefreshTokenHash = "hash-abierta", ExpiresAtUtc = DateTime.UtcNow.AddDays(1) });
            await setup.SaveChangesAsync();
        }

        await controller.UpdateProfile(user.Id, new UpdateUserProfileRequest("Operador", "otro@correo.gt"), default);

        await using var fresh = OpenDb(databaseName);
        Assert.All(await fresh.Sessions.ToListAsync(), x => Assert.NotNull(x.RevokedAtUtc));
    }

    [Fact]
    public async Task UpdateProfile_WhenOnlyTheNameChanges_KeepsSessionsOpen()
    {
        var (controller, user, databaseName) = await Seed();
        await using (var setup = OpenDb(databaseName))
        {
            setup.Sessions.Add(new Session { UserId = user.Id, RefreshTokenHash = "hash-abierta", ExpiresAtUtc = DateTime.UtcNow.AddDays(1) });
            await setup.SaveChangesAsync();
        }

        await controller.UpdateProfile(user.Id, new UpdateUserProfileRequest("Operador Corregido", "op@correo.gt"), default);

        await using var fresh = OpenDb(databaseName);
        Assert.All(await fresh.Sessions.ToListAsync(), x => Assert.Null(x.RevokedAtUtc));
    }

    [Fact]
    public async Task UpdateProfile_RejectsAnEmailAlreadyUsedByAnotherAccount()
    {
        var (controller, user, databaseName) = await Seed();
        await using (var setup = OpenDb(databaseName))
        {
            setup.Users.Add(new User { Email = "ocupado@correo.gt", UserName = "ocupado@correo.gt", NormalizedEmail = "OCUPADO@CORREO.GT", NormalizedUserName = "OCUPADO@CORREO.GT", FullName = "Otra persona" });
            await setup.SaveChangesAsync();
        }

        var result = await controller.UpdateProfile(user.Id, new UpdateUserProfileRequest("Operador", "ocupado@correo.gt"), default);

        Assert.IsType<ConflictObjectResult>(result);
        await using var fresh = OpenDb(databaseName);
        Assert.Equal("op@correo.gt", (await fresh.Users.SingleAsync(x => x.Id == user.Id)).Email);
    }

    [Fact]
    public async Task UpdateProfile_RejectsAnEmptyName_AndUnknownUsers()
    {
        var (controller, user, _) = await Seed();

        Assert.IsType<BadRequestObjectResult>(await controller.UpdateProfile(user.Id, new UpdateUserProfileRequest("  a  ", "op@correo.gt"), default));
        Assert.IsType<NotFoundResult>(await controller.UpdateProfile(Guid.NewGuid(), new UpdateUserProfileRequest("Alguien", "nuevo@correo.gt"), default));
    }


    [Fact]
    public async Task UpdateProfile_AssignsPersonnelCenterAndFarm()
    {
        var (controller, user, databaseName) = await Seed();
        Guid farmId;
        await using (var setup = OpenDb(databaseName))
        {
            var center = new UniversityCenter { Code = "CU", Name = "Centro Universitario" };
            var farm = new Farm { UniversityCenter = center, Code = "FIN", Name = "Finca Experimental" };
            setup.AddRange(center, farm); await setup.SaveChangesAsync(); farmId = farm.Id;
        }

        var result = await controller.UpdateProfile(user.Id, new UpdateUserProfileRequest("Operador", "op@correo.gt", "P-104", null, farmId), default);

        Assert.IsType<NoContentResult>(result);
        await using var fresh = OpenDb(databaseName);
        var stored = await fresh.Users.SingleAsync(x => x.Id == user.Id);
        var farmStored = await fresh.Farms.SingleAsync(x => x.Id == farmId);
        Assert.Equal("P-104", stored.PersonnelCode);
        Assert.Equal(farmId, stored.FarmId);
        Assert.Equal(farmStored.UniversityCenterId, stored.UniversityCenterId);
    }
    private static AppDbContext OpenDb(string name) => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    private static async Task<(UsersController Controller, User User, string DatabaseName)> Seed()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var db = OpenDb(databaseName);
        var user = new User { Email = "op@correo.gt", UserName = "op@correo.gt", NormalizedEmail = "OP@CORREO.GT", NormalizedUserName = "OP@CORREO.GT", FullName = "Operador" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var controllerDb = OpenDb(databaseName);
        var controller = new UsersController(controllerDb, null!, null, BuildUserManager(controllerDb));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "tests")) } };
        return (controller, user, databaseName);
    }

    private static UserManager<User> BuildUserManager(AppDbContext db)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(db);
        services.AddIdentityCore<User>().AddRoles<Role>().AddEntityFrameworkStores<AppDbContext>();
        return services.BuildServiceProvider().GetRequiredService<UserManager<User>>();
    }
}
