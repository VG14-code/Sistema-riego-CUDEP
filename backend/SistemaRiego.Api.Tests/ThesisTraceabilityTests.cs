using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Controllers;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Tests;

public sealed class ThesisTraceabilityTests
{
    [Fact]
    public async Task Installation_PreservesWhoWhenAndTerritorialAssociation()
    {
        var setup = await Setup();
        var request = new DeviceInstallationRequest(setup.Device.Id, setup.Zone.Id, "Caseta norte", DateTime.UtcNow.AddDays(-2), "Técnico CUDEP", null, "Montaje documentado");
        var result = await setup.IoT.CreateInstallation(request, default);
        Assert.IsType<OkObjectResult>(result);
        var item = await setup.Db.DeviceInstallations.SingleAsync();
        Assert.Equal(setup.Zone.Id, item.IrrigationZoneId);
        Assert.Equal("Técnico CUDEP", item.InstallerName);
    }

    [Fact]
    public async Task RemoteConfiguration_StopsAfterConfiguredAttempts()
    {
        var setup = await Setup();
        var queued = Assert.IsType<OkObjectResult>(await setup.IoT.QueueRemoteConfiguration(new(setup.Node.Id, "REINICIAR", "{}", 2), default));
        var id = (Guid)queued.Value!.GetType().GetProperty("Id")!.GetValue(queued.Value)!;
        await setup.IoT.ConfirmRemoteConfiguration(id, new(false, "Sin ACK"), default);
        await setup.IoT.ConfirmRemoteConfiguration(id, new(false, "Sin ACK"), default);
        Assert.Equal("Fallida", (await setup.Db.RemoteConfigurationCommands.FindAsync(id))!.Status);
    }

    [Fact]
    public async Task QueueRemoteConfiguration_ResponseSerializesWithoutObjectCycle()
    {
        // Setup deja nodo y dispositivo rastreados, como hace el despachador con Include(Node):
        // la entidad cruda quedaba enlazada Node -> Devices -> Node al serializar.
        var setup = await Setup();
        var queued = Assert.IsType<OkObjectResult>(await setup.IoT.QueueRemoteConfiguration(new(setup.Node.Id, "CAMBIAR_FRECUENCIA", "{\"telemetryIntervalSeconds\":30}", 3), default));
        var json = JsonSerializer.Serialize(queued.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains($"\"node\":\"{setup.Node.Name}\"", json);
        Assert.Contains("\"status\":\"Pendiente\"", json);
        Assert.DoesNotContain("devices", json, StringComparison.OrdinalIgnoreCase);

        var id = (Guid)queued.Value!.GetType().GetProperty("Id")!.GetValue(queued.Value)!;
        var ack = Assert.IsType<OkObjectResult>(await setup.IoT.ConfirmRemoteConfiguration(id, new(true, null), default));
        Assert.Contains("\"status\":\"Confirmada\"", JsonSerializer.Serialize(ack.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public async Task Firmware_RegistryUpdatesNodeOnlyWhenApplicationIsConfirmed()
    {
        var setup = await Setup();
        await setup.IoT.RegisterFirmware(new(setup.Node.Id, "2.0.0", "Pendiente de hardware", null, null), default);
        Assert.Equal("1.0.0", (await setup.Db.IoTNodes.FindAsync(setup.Node.Id))!.FirmwareVersion);
        await setup.IoT.RegisterFirmware(new(setup.Node.Id, "2.0.0", "Aplicada", DateTime.UtcNow, "Confirmada en banco"), default);
        Assert.Equal("2.0.0", (await setup.Db.IoTNodes.FindAsync(setup.Node.Id))!.FirmwareVersion);
    }

    [Fact]
    public async Task Rotation_RejectsOverlapAndRequiresJustificationForSameCrop()
    {
        var setup = await Setup();
        setup.Db.CropCycles.Add(new CropCycle { CropId = setup.Crop.Id, IrrigationZoneId = setup.Zone.Id, Name = "Anterior", SowingDate = new(2026, 1, 1), ExpectedHarvestDate = new(2026, 3, 1), ActualHarvestDate = new(2026, 3, 1), AreaHectares = 1, Status = "Finalizado" });
        await setup.Db.SaveChangesAsync();
        var controller = new CropRotationsController(setup.Db);
        var unjustified = await controller.Create(new(setup.Zone.Id, setup.Crop.Id, null, new(2026, 4, 1), new(2026, 6, 1), "Planificada", null), default);
        Assert.IsType<BadRequestObjectResult>(unjustified);
        var accepted = await controller.Create(new(setup.Zone.Id, setup.Crop.Id, null, new(2026, 4, 1), new(2026, 6, 1), "Planificada", "Rotación con manejo sanitario y abono verde"), default);
        Assert.IsType<OkObjectResult>(accepted);
        var overlap = await controller.Create(new(setup.Zone.Id, setup.OtherCrop.Id, null, new(2026, 5, 1), new(2026, 7, 1), "Planificada", null), default);
        Assert.IsType<BadRequestObjectResult>(overlap);
    }

    [Fact]
    public async Task EnvironmentalEvaluation_BlocksRecommendationOutsideHumidityRange()
    {
        var setup = await Setup();
        var airHumidity = new MasterCatalogItem { Kind = CatalogKind.SensorType, Code = "AIR_HUMIDITY", Name = "Humedad ambiental" };
        var sensor = new IoTSensor { Code = "AIR-H", Name = "Humedad ambiental", SerialNumber = "AIR-H-1", SensorType = airHumidity, MeasurementUnit = setup.Unit, OperationalStatus = setup.Active };
        setup.Db.AddRange(airHumidity, sensor);
        setup.Db.IrrigationZoneSensors.Add(new IrrigationZoneSensor { IrrigationZone = setup.Zone, Sensor = sensor });
        setup.Db.CropCycles.Add(new CropCycle { Crop = setup.Crop, IrrigationZone = setup.Zone, Name = "Activo", SowingDate = new(2026, 1, 1), ExpectedHarvestDate = new(2026, 12, 1), AreaHectares = 1, Status = "Activo" });
        setup.Db.CropWaterRequirements.Add(new CropWaterRequirement { Crop = setup.Crop, MinimumMoisturePercent = 30, TargetMoisturePercent = 50, MaximumMoisturePercent = 70, BaseVolumeLiters = 100, BaseDurationMinutes = 10, FrequencyHours = 12, MinimumAmbientHumidityPercent = 40, MaximumAmbientHumidityPercent = 70 });
        setup.Db.SensorReadings.Add(new SensorReading { Sensor = sensor, IrrigationZone = setup.Zone, CapturedAtUtc = DateTime.UtcNow, Value = 90, MessageId = Guid.NewGuid().ToString() });
        await setup.Db.SaveChangesAsync();
        var result = Assert.IsType<OkObjectResult>(await new AgronomyController(setup.Db).Recommendations(default));
        var recommendations = Assert.IsAssignableFrom<IEnumerable<IrrigationRecommendationResponse>>(result.Value);
        Assert.Contains(recommendations, x => x.Decision == "ESPERAR" && x.Explanation.Contains("humedad ambiental"));
    }

    [Fact]
    public async Task Calibration_PreservesPatternTechnicianAndNextDate()
    {
        var setup = await Setup();
        var sensorType = new MasterCatalogItem { Kind = CatalogKind.SensorType, Code = "CAL", Name = "Calibrable" };
        var sensor = new IoTSensor { Code = "CAL-1", Name = "Sensor calibrable", SerialNumber = "CAL-SER", SensorType = sensorType, MeasurementUnit = setup.Unit, OperationalStatus = setup.Active };
        setup.Db.AddRange(sensorType, sensor); await setup.Db.SaveChangesAsync();
        var next = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6));
        var controller = new IoTController(setup.Db) { ControllerContext = setup.IoT.ControllerContext };
        var result = await controller.Calibrate(new(sensor.Id, null, 50, 48, "Banco de prueba", "Patrón ISO-17025", "Técnico CUDEP", next), default);
        Assert.IsType<CreatedAtActionResult>(result.Result);
        var calibration = await setup.Db.SensorCalibrations.SingleAsync();
        Assert.Equal("Patrón ISO-17025", calibration.CalibrationPattern); Assert.Equal("Técnico CUDEP", calibration.TechnicianName); Assert.Equal(next, calibration.NextCalibrationDate);
    }
    [Fact]
    public async Task UpdateInventory_RejectsAStatusOutsideTheAllowedSet()
    {
        var setup = await Setup();

        // Antes bastaba con que no fuese vacio: cualquier texto se guardaba tal cual
        // y ningun filtro agrupaba despues los dispositivos.
        var result = await setup.IoT.UpdateInventory(setup.Device.Id, new InventoryUpdateRequest("Ana", "averiado", null, null, 100, "GTQ"), default);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("En mantenimiento", bad.Value!.ToString());
        Assert.NotEqual("averiado", (await setup.Db.IoTDevices.SingleAsync(x => x.Id == setup.Device.Id)).InventoryStatus);
    }

    [Fact]
    public async Task UpdateInventory_NormalisesTheCaseOfAnAllowedStatus()
    {
        var setup = await Setup();

        var result = await setup.IoT.UpdateInventory(setup.Device.Id, new InventoryUpdateRequest("Ana", "  en MANTENIMIENTO  ", null, null, 100, "gtq"), default);

        Assert.IsType<NoContentResult>(result);
        var device = await setup.Db.IoTDevices.SingleAsync(x => x.Id == setup.Device.Id);
        Assert.Equal("En mantenimiento", device.InventoryStatus);
        Assert.Equal("GTQ", device.Currency);
    }

    [Fact]
    public void InventoryStatuses_AreExposedForTheInterface()
    {
        var ok = Assert.IsType<OkObjectResult>(new IoTTraceabilityController(null!).Statuses());
        Assert.Equal(["Instalado", "Disponible", "Dañado", "En mantenimiento"], Assert.IsType<string[]>(ok.Value));
    }

    private static async Task<SetupData> Setup()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var active = new MasterCatalogItem { Kind = CatalogKind.OperationalStatus, Code = "ACTIVE", Name = "Activo" };
        var deviceType = new MasterCatalogItem { Kind = CatalogKind.DeviceType, Code = "NODE", Name = "Nodo" };
        var unit = new MasterCatalogItem { Kind = CatalogKind.MeasurementUnit, Code = "PERCENT", Name = "Porcentaje" };
        var center = new UniversityCenter { Code = "C", Name = "Centro" };
        var farm = new Farm { UniversityCenter = center, Code = "F", Name = "Finca" };
        var block = new FarmBlock { Farm = farm, Code = "B", Name = "Bloque" };
        var sector = new IrrigationSector { FarmBlock = block, Code = "S", Name = "Sector" };
        var zone = new IrrigationZone { IrrigationSector = sector, OperationalStatus = active, Code = "Z", Name = "Zona", AreaHectares = 1 };
        var node = new IoTNode { Code = "N1", Name = "Nodo 1", OperationalStatus = active, FirmwareVersion = "1.0.0" };
        var device = new IoTDevice { Code = "D1", Name = "Dispositivo", SerialNumber = "SER1", DeviceType = deviceType, OperationalStatus = active, Node = node };
        var cropType = new CropType { Code = "HORT", Name = "Hortaliza" };
        var crop = new Crop { CropType = cropType, Code = "TOM", Name = "Tomate" };
        var otherCrop = new Crop { CropType = cropType, Code = "MAI", Name = "Maíz" };
        db.AddRange(active, deviceType, unit, center, farm, block, sector, zone, node, device, cropType, crop, otherCrop);
        await db.SaveChangesAsync();
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, RoleNames.Administrator)], "tests"));
        var iot = new IoTTraceabilityController(db) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } } };
        return new(db, iot, active, unit, zone, node, device, crop, otherCrop);
    }

    private sealed record SetupData(AppDbContext Db, IoTTraceabilityController IoT, MasterCatalogItem Active, MasterCatalogItem Unit, IrrigationZone Zone, IoTNode Node, IoTDevice Device, Crop Crop, Crop OtherCrop);
}

