using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Controllers;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Tests;

public sealed class RolesControllerCrudTests
{
    [Fact]
    public async Task Create_PersistsRoleAndAudit()
    {
        await using var db = Db();
        var result = await new RolesController(db).Create(new SaveRoleRequest("Supervisor de campo", "Supervisa zonas", true), default);
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<RoleResponse>(created.Value);
        Assert.Equal("Supervisor de campo", response.Name);
        Assert.True(response.IsActive);
        Assert.Equal("ROLE_CREATED", (await db.AccessAudits.SingleAsync()).EventType);
    }

    [Fact]
    public async Task Create_RejectsDuplicatedNormalizedName()
    {
        await using var db = Db();
        db.Roles.Add(new Role { Name = "Supervisor", NormalizedName = "SUPERVISOR", Description = "" });
        await db.SaveChangesAsync();
        var result = await new RolesController(db).Create(new SaveRoleRequest("supervisor", "Duplicado", true), default);
        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Status_ProtectsAdministrator()
    {
        await using var db = Db();
        var role = new Role { Name = RoleNames.Administrator, NormalizedName = RoleNames.Administrator.ToUpperInvariant(), Description = "" };
        db.Roles.Add(role); await db.SaveChangesAsync();
        var result = await new RolesController(db).Status(role.Id, new UpdateRoleStatusRequest(false), default);
        Assert.IsType<BadRequestObjectResult>(result);
        Assert.True((await db.Roles.SingleAsync()).IsActive);
    }

    [Fact]
    public async Task Delete_RejectsRoleAssignedToUsers()
    {
        await using var db = Db();
        var role = new Role { Name = "Operador temporal", NormalizedName = "OPERADOR TEMPORAL", Description = "" };
        var user = new User { UserName = "test@example.com", NormalizedUserName = "TEST@EXAMPLE.COM", Email = "test@example.com", NormalizedEmail = "TEST@EXAMPLE.COM", FullName = "Usuario prueba" };
        db.AddRange(role, user); await db.SaveChangesAsync();
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id }); await db.SaveChangesAsync();
        var result = await new RolesController(db).Delete(role.Id, default);
        Assert.IsType<ConflictObjectResult>(result);
        Assert.True(await db.Roles.AnyAsync(x => x.Id == role.Id));
    }

    [Fact]
    public async Task Delete_RemovesUnassignedCustomRole()
    {
        await using var db = Db();
        var role = new Role { Name = "Temporal", NormalizedName = "TEMPORAL", Description = "" };
        db.Roles.Add(role); await db.SaveChangesAsync();
        var result = await new RolesController(db).Delete(role.Id, default);
        Assert.IsType<NoContentResult>(result);
        Assert.False(await db.Roles.AnyAsync(x => x.Id == role.Id));
    }

    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}