using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Tests;

public sealed class DbSeederPermissionTests
{
    [Fact]
    public async Task Seed_CreatesDeviceCatalogPermissions_AndAssignsThemByRole()
    {
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddLogging();
        services.AddIdentityCore<User>().AddRoles<Role>().AddEntityFrameworkStores<AppDbContext>();
        var provider = services.BuildServiceProvider();
        var configuration = new ConfigurationBuilder().Build();

        await using (var setupScope = provider.CreateAsyncScope())
            await setupScope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        await DbSeeder.SeedAsync(provider, configuration);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Permissions.AnyAsync(x => x.Code == PermissionCodes.DeviceCatalogsRead));
        Assert.True(await db.Permissions.AnyAsync(x => x.Code == PermissionCodes.DeviceCatalogsManage));
        Assert.True(await db.Permissions.AnyAsync(x => x.Code == PermissionCodes.DeviceCatalogsDelete));

        var operatorRole = await db.Roles.Include(x => x.RolePermissions).ThenInclude(x => x.Permission).SingleAsync(x => x.Name == RoleNames.Operator);
        Assert.Contains(operatorRole.RolePermissions, x => x.Permission.Code == PermissionCodes.DeviceCatalogsRead);
        Assert.DoesNotContain(operatorRole.RolePermissions, x => x.Permission.Code == PermissionCodes.DeviceCatalogsDelete);

        var adminRole = await db.Roles.Include(x => x.RolePermissions).ThenInclude(x => x.Permission).SingleAsync(x => x.Name == RoleNames.Administrator);
        Assert.Contains(adminRole.RolePermissions, x => x.Permission.Code == PermissionCodes.DeviceCatalogsDelete);
    }
}
