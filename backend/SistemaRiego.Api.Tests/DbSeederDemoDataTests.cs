using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Tests;

public sealed class DbSeederDemoDataTests
{
    private const string DemoMarker = "DEMO_EMPIRICAL_DATA_V1";

    private static async Task<ServiceProvider> BuildProviderAsync()
    {
        // El nombre se calcula una sola vez: dentro del lambda cada DbContext
        // recibiria una base distinta y no compartirian datos.
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddLogging();
        services.AddIdentityCore<User>().AddRoles<Role>().AddEntityFrameworkStores<AppDbContext>();
        var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        return provider;
    }

    [Fact]
    public async Task Seed_WithoutDemoData_LeavesNoFabricatedHistory()
    {
        var provider = await BuildProviderAsync();

        await DbSeeder.SeedAsync(provider, new ConfigurationBuilder().Build(), includeDemoData: false);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.OperationalEvents.AnyAsync(x => x.EventType == DemoMarker));
        Assert.False(await db.SensorReadings.AnyAsync());
        Assert.False(await db.IrrigationRuns.AnyAsync());
        Assert.False(await db.WaterConsumptionRecords.AnyAsync());
    }

    [Fact]
    public async Task Seed_WithoutDemoData_StillCreatesOperationalStructure()
    {
        var provider = await BuildProviderAsync();

        await DbSeeder.SeedAsync(provider, new ConfigurationBuilder().Build(), includeDemoData: false);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // La estructura territorial e hidraulica describe la instalacion real y debe
        // existir en cualquier entorno, aunque no haya muestra de demostracion.
        foreach (var code in new[] { "ZONA-A1", "ZONA-B1", "ZONA-C1" })
            Assert.True(await db.IrrigationZones.AnyAsync(x => x.Code == code), $"Falta la zona {code}");
        foreach (var code in new[] { "SECTOR-A", "SECTOR-B", "SECTOR-C" })
            Assert.True(await db.IrrigationSectors.AnyAsync(x => x.Code == code), $"Falta el sector {code}");
        Assert.True(await db.MasterCatalogItems.AnyAsync());
        Assert.True(await db.GlobalParameters.AnyAsync(x => x.Key == "AUDIT_RETENTION_DAYS"));
        Assert.True(await db.WaterTanks.AnyAsync());
        Assert.True(await db.IoTSensors.AnyAsync(x => x.Code == "HUM-SUELO-A1"));
    }

    [Fact]
    public async Task Seed_WithDemoData_CreatesSampleMarkedAsNonHardware()
    {
        var provider = await BuildProviderAsync();

        await DbSeeder.SeedAsync(provider, new ConfigurationBuilder().Build(), includeDemoData: true);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.OperationalEvents.AnyAsync(x => x.EventType == DemoMarker));
        Assert.True(await db.IrrigationRuns.AnyAsync());
        // Toda lectura sembrada debe declarar su transporte simulado, para que nunca
        // se confunda con telemetria recibida de un dispositivo.
        var transports = await db.SensorReadings.Select(x => x.Transport).Distinct().ToListAsync();
        Assert.NotEmpty(transports);
        Assert.All(transports, t => Assert.Contains(t, new[] { "SIMULACIÓN", "MUESTRA_CAMPO_PRUEBA" }));
    }
}
