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

    [Fact]
    public async Task Seed_ReplicatesPreExistingRoleHierarchy_AcrossAllMigratedControllers()
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
        var operatorCodes = await CodesOf(db, RoleNames.Operator);
        var technicianCodes = await CodesOf(db, RoleNames.Technician);
        var adminCodes = await CodesOf(db, RoleNames.Administrator);

        // Antes de la migracion, Policies.Operator = RequireRole(Admin, Tecnico, Operador):
        // el tecnico satisfacia cualquier endpoint de solo-lectura igual que el operador.
        Assert.Contains(PermissionCodes.IrrigationOperate, technicianCodes);
        Assert.Contains(PermissionCodes.TerritoryRead, technicianCodes);
        Assert.Contains(PermissionCodes.AgronomyRead, operatorCodes);

        // Policies.Technician = RequireRole(Admin, Tecnico): el operador NO satisfacia
        // los endpoints de gestion.
        Assert.DoesNotContain(PermissionCodes.AgronomyManage, operatorCodes);
        Assert.DoesNotContain(PermissionCodes.WaterSupplyManage, operatorCodes);

        // MaintenanceController/TerritoryMaintenanceController exigian Technician sin
        // nivel Operator equivalente: el operador no debe tener acceso.
        Assert.DoesNotContain(PermissionCodes.MaintenanceManage, operatorCodes);
        Assert.Contains(PermissionCodes.MaintenanceManage, technicianCodes);

        // Acciones exclusivas de Administrator (eliminar/roles/auditoria) nunca se
        // asignan explicitamente a tecnico u operador; solo Administrador las trae
        // via la lista completa de permisos.
        Assert.DoesNotContain(PermissionCodes.WaterSupplyDelete, technicianCodes);
        Assert.DoesNotContain(PermissionCodes.RolesManage, technicianCodes);
        Assert.Contains(PermissionCodes.WaterSupplyDelete, adminCodes);
        Assert.Contains(PermissionCodes.RolesManage, adminCodes);
        Assert.Contains(PermissionCodes.AuditRead, adminCodes);
    }

    private static async Task<HashSet<string>> CodesOf(AppDbContext db, string roleName)
    {
        var role = await db.Roles.Include(x => x.RolePermissions).ThenInclude(x => x.Permission).SingleAsync(x => x.Name == roleName);
        return role.RolePermissions.Select(x => x.Permission.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
