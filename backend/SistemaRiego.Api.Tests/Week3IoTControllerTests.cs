using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Controllers;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Tests;

public sealed class Week3IoTControllerTests
{
    [Fact]
    public async Task Device_Create_AssignsItToExistingNode()
    {
        var setup = await CreateSetup();
        var request = new IoTDeviceRequest("NODE-02", "Nodo dos", "SER-002", "CUDEP", "Nodo RK520", "Parcela B", new DateOnly(2026, 8, 2), setup.DeviceType.Id, setup.Active.Id, setup.Node.Id, true);

        var result = await setup.Controller.CreateDevice(request, default);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<IoTDeviceResponse>(created.Value);
        Assert.Equal(setup.Node.Name, response.NodeName);
        Assert.Contains(await setup.Db.AccessAudits.ToListAsync(), x => x.EventType == "IOT_DEVICE_CREATED");
    }

    [Fact]
    public async Task Sensor_Create_RejectsInvalidMeasurementRange()
    {
        var setup = await CreateSetup();
        var request = new IoTSensorRequest("HUM-01", "Humedad", "RK-01", "RK520-02", "RS485-1", 100, 10, 0, setup.SensorType.Id, setup.Unit.Id, setup.Active.Id, null, true);

        var result = await setup.Controller.CreateSensor(request, default);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(badRequest.Value);
        Assert.Empty(await setup.Db.IoTSensors.ToListAsync());
    }

    [Fact]
    public async Task Calibration_CalculatesAndAppliesOffset()
    {
        var setup = await CreateSetup();
        var sensor = new IoTSensor { Code = "HUM-01", Name = "Humedad", SerialNumber = "RK-01", MinimumValue = 0, MaximumValue = 100, SensorTypeId = setup.SensorType.Id, MeasurementUnitId = setup.Unit.Id, OperationalStatusId = setup.Active.Id };
        setup.Db.IoTSensors.Add(sensor); await setup.Db.SaveChangesAsync();

        var result = await setup.Controller.Calibrate(new SensorCalibrationRequest(sensor.Id, null, 50, 47.5m, "Prueba controlada"), default);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<SensorCalibrationResponse>(created.Value);
        Assert.Equal(2.5m, response.AppliedOffset);
        Assert.Equal(2.5m, (await setup.Db.IoTSensors.FindAsync(sensor.Id))!.CalibrationOffset);
    }

    [Fact]
    public async Task Sensor_Deactivate_PreservesRecordAndMarksInactive()
    {
        var setup = await CreateSetup();
        var sensor = new IoTSensor { Code = "TEMP-01", Name = "Temperatura", SerialNumber = "RK-T-01", MinimumValue = -40, MaximumValue = 80, SensorTypeId = setup.SensorType.Id, MeasurementUnitId = setup.Unit.Id, OperationalStatusId = setup.Active.Id };
        setup.Db.IoTSensors.Add(sensor); await setup.Db.SaveChangesAsync();

        var result = await setup.Controller.DeactivateSensor(sensor.Id, default);

        Assert.IsType<NoContentResult>(result);
        Assert.False((await setup.Db.IoTSensors.FindAsync(sensor.Id))!.IsActive);
    }

    private static async Task<Setup> CreateSetup()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var active = new MasterCatalogItem { Kind = CatalogKind.OperationalStatus, Code = "ACTIVE", Name = "Activo" };
        var deviceType = new MasterCatalogItem { Kind = CatalogKind.DeviceType, Code = "SENSOR_NODE", Name = "Nodo de sensores" };
        var sensorType = new MasterCatalogItem { Kind = CatalogKind.SensorType, Code = "SOIL_MOISTURE", Name = "Humedad del suelo" };
        var unit = new MasterCatalogItem { Kind = CatalogKind.MeasurementUnit, Code = "PERCENT", Name = "Porcentaje", Symbol = "%" };
        db.MasterCatalogItems.AddRange(active, deviceType, sensorType, unit);
        var node = new IoTNode { Code = "RPI-01", Name = "Raspberry central", OperationalStatusId = active.Id };
        db.IoTNodes.Add(node); await db.SaveChangesAsync();
        var controller = new IoTController(db) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, RoleNames.Administrator)], "tests")) } } };
        return new Setup(db, controller, active, deviceType, sensorType, unit, node);
    }

    private sealed record Setup(AppDbContext Db, IoTController Controller, MasterCatalogItem Active, MasterCatalogItem DeviceType, MasterCatalogItem SensorType, MasterCatalogItem Unit, IoTNode Node);
}
