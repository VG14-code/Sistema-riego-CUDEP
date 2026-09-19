using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Controllers;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Tests;

public sealed class MinorFixesModules2To6Tests
{
    [Theory]
    [InlineData("60 s", 60)]
    [InlineData("5 min", 300)]
    [InlineData("1 h", 3600)]
    [InlineData("30 segundos", 30)]
    [InlineData("Cada minuto", 60)]
    [InlineData("Humedades", null)]
    [InlineData("2 h", null)]
    [InlineData(null, null)]
    public void ReadingFrequencyInterval_ParsesLegacyText(string? text, int? expected) => Assert.Equal(expected, ReadingFrequencyInterval.Parse(text));

    [Fact]
    public async Task ReadingFrequencyCatalog_RequiresIntervalBetween1And3600Seconds()
    {
        await using var db = Db(); var controller = WithUser(new MasterDataController(db));

        Assert.IsType<BadRequestObjectResult>((await controller.Create("ReadingFrequency", new CatalogItemRequest("NONE", "Sin intervalo", null, null), default)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Create("ReadingFrequency", new CatalogItemRequest("SLOW", "Muy lenta", null, null, IntervalSeconds: 7200), default)).Result);
        var created = Assert.IsType<CreatedAtActionResult>((await controller.Create("ReadingFrequency", new CatalogItemRequest("MIN", "Cada minuto", null, "60 s", IntervalSeconds: 60), default)).Result);

        Assert.Equal(60, Assert.IsType<CatalogItemResponse>(created.Value).IntervalSeconds);
    }

    [Fact]
    public async Task SensorFrequency_IsQueuedToItsNodeOnlyWhenItChanges()
    {
        await using var db = Db();
        var active = new MasterCatalogItem { Kind = CatalogKind.OperationalStatus, Code = "ACTIVE", Name = "Activo" };
        var deviceType = new MasterCatalogItem { Kind = CatalogKind.DeviceType, Code = "NODE", Name = "Nodo" };
        var sensorType = new MasterCatalogItem { Kind = CatalogKind.SensorType, Code = "SOIL_MOISTURE", Name = "Humedad" };
        var unit = new MasterCatalogItem { Kind = CatalogKind.MeasurementUnit, Code = "PERCENT", Name = "Porcentaje", Symbol = "%" };
        var fast = new MasterCatalogItem { Kind = CatalogKind.ReadingFrequency, Code = "FAST", Name = "Rápida", IntervalSeconds = 30 };
        var slow = new MasterCatalogItem { Kind = CatalogKind.ReadingFrequency, Code = "SLOW", Name = "Lenta", IntervalSeconds = 300 };
        var node = new IoTNode { Code = "RPI-01", Name = "Nodo central", OperationalStatusId = active.Id };
        var device = new IoTDevice { Code = "ESP-01", Name = "ESP32", SerialNumber = "ESP-01", DeviceTypeId = deviceType.Id, OperationalStatusId = active.Id, NodeId = node.Id };
        db.AddRange(active, deviceType, sensorType, unit, fast, slow, node, device); await db.SaveChangesAsync();
        var controller = WithUser(new IoTController(db));
        IoTSensorRequest Request(Guid frequencyId) => new("HUM-01", "Humedad", "SER-01", null, null, 0, 100, 0, sensorType.Id, unit.Id, active.Id, device.Id, true, frequencyId);

        var created = Assert.IsType<IoTSensorResponse>(Assert.IsType<CreatedAtActionResult>((await controller.CreateSensor(Request(fast.Id), default)).Result).Value);
        await controller.UpdateSensor(created.Id, Request(fast.Id), default);
        await controller.UpdateSensor(created.Id, Request(slow.Id), default);

        var commands = await db.RemoteConfigurationCommands.OrderBy(x => x.RequestedAtUtc).ToListAsync();
        Assert.Equal(2, commands.Count);
        Assert.All(commands, x => Assert.Equal(("CAMBIAR_FRECUENCIA", node.Id), (x.CommandType, x.NodeId)));
        Assert.Equal([30, 300], commands.Select(x => JsonDocument.Parse(x.Payload).RootElement.GetProperty("intervalSeconds").GetInt32()));
    }

    [Fact]
    public async Task ApproveRecommendation_WithoutValve_IsRejectedBeforeCreatingTheRun()
    {
        await using var db = Db();
        var active = new MasterCatalogItem { Kind = CatalogKind.OperationalStatus, Code = "ACTIVE", Name = "Activo" };
        var sensorType = new MasterCatalogItem { Kind = CatalogKind.SensorType, Code = "SOIL_MOISTURE", Name = "Humedad" };
        var unit = new MasterCatalogItem { Kind = CatalogKind.MeasurementUnit, Code = "PERCENT", Name = "Porcentaje", Symbol = "%" };
        var sensor = new IoTSensor { Code = "HUM-01", Name = "Humedad", SerialNumber = "SER-01", MinimumValue = 0, MaximumValue = 100, SensorType = sensorType, MeasurementUnit = unit, OperationalStatus = active };
        var center = new UniversityCenter { Code = "C", Name = "Centro" };
        var farm = new Farm { UniversityCenter = center, Code = "F", Name = "Finca" };
        var block = new FarmBlock { Farm = farm, Code = "B", Name = "Bloque" };
        var sector = new IrrigationSector { FarmBlock = block, Code = "S", Name = "Sector" };
        var zone = new IrrigationZone { IrrigationSector = sector, OperationalStatus = active, Code = "Z", Name = "Zona sin válvula", AreaHectares = 1, PrimarySensor = sensor };
        var crop = new Crop { CropType = new CropType { Code = "H", Name = "Hortaliza" }, Code = "T", Name = "Tomate" };
        var cycle = new CropCycle { Crop = crop, IrrigationZone = zone, Name = "Ciclo", Status = "Activo" };
        db.AddRange(active, sensorType, unit, sensor, center, farm, block, sector, zone, crop, cycle,
            new CropWaterRequirement { Crop = crop, MinimumMoisturePercent = 30, TargetMoisturePercent = 50, MaximumMoisturePercent = 70, BaseVolumeLiters = 100, FrequencyHours = 24, BaseDurationMinutes = 20, IsActive = true },
            new SensorReading { Sensor = sensor, IrrigationZone = zone, CapturedAtUtc = DateTime.UtcNow, Value = 10, IsValid = true, MessageId = "LOW-1" });
        await db.SaveChangesAsync();

        var result = await WithUser(new AgronomyController(db)).ApproveRecommendation(cycle.Id, new ApproveIrrigationRecommendationRequest(null), default);

        Assert.Contains("no tiene válvulas", Assert.IsType<ConflictObjectResult>(result).Value!.ToString());
        Assert.Empty(db.IrrigationRuns);
        Assert.Empty(db.OperationalEvents);
    }

    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static T WithUser<T>(T controller) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, RoleNames.Administrator)], "tests")) } };
        return controller;
    }
}
