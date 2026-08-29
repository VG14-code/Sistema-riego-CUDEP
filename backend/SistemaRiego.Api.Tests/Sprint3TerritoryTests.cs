using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Controllers;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Tests;

public sealed class Sprint3TerritoryTests
{
    private const string SectorPolygon = "{\"type\":\"Polygon\",\"coordinates\":[[[-90,16],[-89,16],[-89,17],[-90,17],[-90,16]]]}";
    private const string ZoneOne = "{\"type\":\"Polygon\",\"coordinates\":[[[-89.9,16.1],[-89.6,16.1],[-89.6,16.4],[-89.9,16.4],[-89.9,16.1]]]}";
    private const string ZoneOverlap = "{\"type\":\"Polygon\",\"coordinates\":[[[-89.7,16.2],[-89.4,16.2],[-89.4,16.5],[-89.7,16.5],[-89.7,16.2]]]}";
    private const string ZoneOutside = "{\"type\":\"Polygon\",\"coordinates\":[[[-88.9,16.1],[-88.7,16.1],[-88.7,16.3],[-88.9,16.3],[-88.9,16.1]]]}";

    [Fact]
    public async Task CreateZone_InsideSector_PersistsMultipleSensorsAndValves()
    {
        var fixture = await CreateFixtureAsync();
        var request = new ZoneRequest(fixture.Sector.Id, "Z-1", "Zona uno", 1, fixture.Status.Id, fixture.Sensors[0].Id, fixture.Valves[0].Id, 16.2m, -89.8m, true, ZoneOne, fixture.Sensors.Select(x => x.Id).ToArray(), fixture.Valves.Select(x => x.Id).ToArray());

        var result = await new TerritoryController(fixture.Db).CreateZone(request, default);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(2, await fixture.Db.IrrigationZoneSensors.CountAsync());
        Assert.Equal(2, await fixture.Db.IrrigationZoneValves.CountAsync());
        Assert.Single(await fixture.Db.IrrigationZoneSensors.Where(x => x.IsPrimary).ToListAsync());
    }

    [Fact]
    public async Task CreateZone_OutsideSector_ReturnsBadRequest()
    {
        var fixture = await CreateFixtureAsync();
        var request = Request(fixture, "Z-OUT", ZoneOutside);
        Assert.IsType<BadRequestObjectResult>(await new TerritoryController(fixture.Db).CreateZone(request, default));
        Assert.Empty(fixture.Db.IrrigationZones);
    }

    [Fact]
    public async Task CreateZone_OverlappingSibling_ReturnsBadRequest()
    {
        var fixture = await CreateFixtureAsync(); var controller = new TerritoryController(fixture.Db);
        Assert.IsType<OkObjectResult>(await controller.CreateZone(Request(fixture, "Z-1", ZoneOne), default));
        Assert.IsType<BadRequestObjectResult>(await controller.CreateZone(Request(fixture, "Z-2", ZoneOverlap), default));
        Assert.Single(fixture.Db.IrrigationZones);
    }

    [Fact]
    public async Task UpdateSector_CannotExcludeExistingZone()
    {
        var fixture = await CreateFixtureAsync();
        await new TerritoryController(fixture.Db).CreateZone(Request(fixture, "Z-1", ZoneOne), default);
        const string reduced = "{\"type\":\"Polygon\",\"coordinates\":[[[-89.5,16.5],[-89.1,16.5],[-89.1,16.9],[-89.5,16.9],[-89.5,16.5]]]}";
        var result = await new TerritoryMaintenanceController(fixture.Db).UpdateSector(fixture.Sector.Id, new(fixture.Block.Id, fixture.Sector.Code, fixture.Sector.Name, 5, 1, true, reduced), default);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateZone_ResponseSerializesWithoutObjectCycle()
    {
        var fixture = await CreateFixtureAsync();
        var request = new ZoneRequest(fixture.Sector.Id, "Z-JSON", "Zona serializable", 1, fixture.Status.Id, fixture.Sensors[0].Id, fixture.Valves[0].Id, 16.2m, -89.8m, true, ZoneOne, fixture.Sensors.Select(x => x.Id).ToArray(), fixture.Valves.Select(x => x.Id).ToArray());

        var result = await new TerritoryController(fixture.Db).CreateZone(request, default);

        // Devolver la entidad con sus navegaciones producia el ciclo
        // Sensors -> IrrigationZone -> Sensors: la zona quedaba guardada pero el
        // endpoint respondia 500 al serializar, y quien probaba reintentaba creando
        // duplicados. Assert.IsType<OkObjectResult> solo no basta: hay que serializar.
        var value = Assert.IsType<OkObjectResult>(result).Value;
        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("Z-JSON", json);
        Assert.DoesNotContain("irrigationZone", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, await fixture.Db.IrrigationZoneSensors.CountAsync());
    }

    private static ZoneRequest Request(Fixture f, string code, string polygon) => new(f.Sector.Id, code, code, 1, f.Status.Id, null, null, null, null, true, polygon);

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var block = new FarmBlock { FarmId = Guid.NewGuid(), Code = "B-" + Guid.NewGuid(), Name = "Bloque", AreaHectares = 10 };
        var sector = new IrrigationSector { FarmBlockId = block.Id, Code = "S-" + Guid.NewGuid(), Name = "Sector", AreaHectares = 5, BoundaryGeoJson = SectorPolygon };
        var status = new MasterCatalogItem { Kind = CatalogKind.OperationalStatus, Code = "ACTIVE-" + Guid.NewGuid(), Name = "Activo" };
        var sensors = Enumerable.Range(1, 2).Select(i => new IoTSensor { Code = $"SEN-{Guid.NewGuid()}", Name = $"Sensor {i}", SerialNumber = $"SNS-{Guid.NewGuid()}", SensorTypeId = Guid.NewGuid(), MeasurementUnitId = Guid.NewGuid(), OperationalStatusId = status.Id }).ToArray();
        var valves = Enumerable.Range(1, 2).Select(i => new IoTDevice { Code = $"VAL-{Guid.NewGuid()}", Name = $"Válvula {i}", SerialNumber = $"VLV-{Guid.NewGuid()}", DeviceTypeId = Guid.NewGuid(), OperationalStatusId = status.Id }).ToArray();
        db.AddRange(block, sector, status); db.AddRange(sensors); db.AddRange(valves); await db.SaveChangesAsync();
        return new Fixture(db, block, sector, status, sensors, valves);
    }

    private sealed record Fixture(AppDbContext Db, FarmBlock Block, IrrigationSector Sector, MasterCatalogItem Status, IoTSensor[] Sensors, IoTDevice[] Valves);
}
