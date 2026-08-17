using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Tests;

public sealed class Sprint3MqttAutomationTests
{
    [Fact]
    public async Task OpenAndClose_AreCompletedOnlyByCorrelatedAck()
    {
        var setup = await SetupAsync(); var publisher = new FakePublisher(); var commands = new IrrigationCommandService(setup.Db, publisher, NullLogger<IrrigationCommandService>.Instance);
        var run = new IrrigationRun { IrrigationZoneId = setup.Zone1.Id, Mode = "Manual", Status = "Esperando ACK", PlannedDurationMinutes = 10, FlowRateLitersMinute = 12, Reason = "Prueba MQTT" };
        setup.Db.IrrigationRuns.Add(run); await setup.Db.SaveChangesAsync();

        var open = Assert.Single(await commands.SendAsync(setup.Zone1.Id, run, "ABRIR_VALVULA", null, default));
        Assert.Equal("Publicado", open.Status); Assert.Equal("Esperando ACK", run.Status);
        await new IrrigationAckService(setup.Db).ProcessAsync(setup.Valve1.Id, $"{{\"commandId\":\"{open.Id}\",\"status\":\"válvula abierta\",\"isOpen\":true}}", default);
        Assert.Equal("En curso", run.Status); Assert.NotNull(run.StartedAtUtc);

        var close = Assert.Single(await commands.SendAsync(setup.Zone1.Id, run, "CERRAR_VALVULA", null, default)); run.Status = "Cierre pendiente"; await setup.Db.SaveChangesAsync();
        await new IrrigationAckService(setup.Db).ProcessAsync(setup.Valve1.Id, $"{{\"commandId\":\"{close.Id}\",\"status\":\"válvula cerrada\",\"isOpen\":false}}", default);
        Assert.Equal("Detenido", run.Status); Assert.Single(setup.Db.WaterConsumptionRecords); Assert.False((await setup.Db.ValveRuntimeStates.FindAsync(setup.Valve1.Id))!.IsOpen);
    }

    [Fact]
    public async Task Automation_ArbitratesPriorityAndHonorsGlobalValveLimit()
    {
        var setup = await SetupAsync(); setup.Db.GlobalParameters.Add(new GlobalParameter { Key = "MAX_SIMULTANEOUS_VALVES", Value = "1", DataType = "integer", Category = "Automatización", Description = "Prueba" });
        setup.Db.IrrigationRules.AddRange(
            new IrrigationRule { IrrigationZoneId = setup.Zone1.Id, Name = "Prioritaria", Priority = 1, MinimumMoisturePercent = 40, TargetMoisturePercent = 60, AllowedFrom = TimeOnly.MinValue, AllowedUntil = TimeOnly.MaxValue },
            new IrrigationRule { IrrigationZoneId = setup.Zone1.Id, Name = "Secundaria", Priority = 2, MinimumMoisturePercent = 45, TargetMoisturePercent = 60, AllowedFrom = TimeOnly.MinValue, AllowedUntil = TimeOnly.MaxValue },
            new IrrigationRule { IrrigationZoneId = setup.Zone2.Id, Name = "Otra zona", Priority = 3, MinimumMoisturePercent = 40, TargetMoisturePercent = 60, AllowedFrom = TimeOnly.MinValue, AllowedUntil = TimeOnly.MaxValue });
        setup.Db.SensorReadings.AddRange(Reading(setup.Zone1.Id, setup.Sensor.Id, "A"), Reading(setup.Zone2.Id, setup.Sensor.Id, "B")); await setup.Db.SaveChangesAsync();
        var publisher = new FakePublisher(); var service = new IrrigationCommandService(setup.Db, publisher, NullLogger<IrrigationCommandService>.Instance);

        var result = await new AutomationEngine(setup.Db, service, NullLogger<AutomationEngine>.Instance).EvaluateAsync(default);

        Assert.Single(setup.Db.IrrigationRuns); Assert.Equal("Esperando ACK", setup.Db.IrrigationRuns.Single().Status);
        Assert.Contains(result.Results, x => x.Name == "Secundaria" && x.LastDecision == "Omitida por prioridad");
        Assert.Contains(result.Results, x => x.Name == "Otra zona" && x.LastDecision == "Límite global");
    }

    private static SensorReading Reading(Guid zoneId, Guid sensorId, string suffix) => new() { IrrigationZoneId = zoneId, SensorId = sensorId, CapturedAtUtc = DateTime.UtcNow, Value = 20, MessageId = "AUTO-" + suffix };
    private static async Task<Setup> SetupAsync()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var status = new MasterCatalogItem { Kind = CatalogKind.OperationalStatus, Code = "ACTIVE", Name = "Activo" };
        var sensor = new IoTSensor { Code = "S", Name = "Sensor", SerialNumber = "S1", SensorTypeId = Guid.NewGuid(), MeasurementUnitId = Guid.NewGuid(), OperationalStatusId = status.Id };
        var sector = new IrrigationSector { FarmBlockId = Guid.NewGuid(), Code = "SEC", Name = "Sector" };
        var valve1 = Valve("V1", status.Id); var valve2 = Valve("V2", status.Id);
        var zone1 = new IrrigationZone { IrrigationSector = sector, Code = "Z1", Name = "Zona 1", OperationalStatus = status, PrimarySensor = sensor, ValveDevice = valve1, IsActive = true };
        var zone2 = new IrrigationZone { IrrigationSector = sector, Code = "Z2", Name = "Zona 2", OperationalStatus = status, PrimarySensor = sensor, ValveDevice = valve2, IsActive = true };
        db.AddRange(status, sensor, sector, valve1, valve2, zone1, zone2); await db.SaveChangesAsync();
        db.IrrigationZoneValves.AddRange(new IrrigationZoneValve { IrrigationZoneId = zone1.Id, DeviceId = valve1.Id }, new IrrigationZoneValve { IrrigationZoneId = zone2.Id, DeviceId = valve2.Id }); await db.SaveChangesAsync();
        return new(db, zone1, zone2, sensor, valve1);
    }
    private static IoTDevice Valve(string code, Guid status) => new() { Code = code, Name = code, SerialNumber = code, DeviceTypeId = Guid.NewGuid(), OperationalStatusId = status };
    private sealed record Setup(AppDbContext Db, IrrigationZone Zone1, IrrigationZone Zone2, IoTSensor Sensor, IoTDevice Valve1);
    private sealed class FakePublisher : IMqttCommandPublisher
    {
        public List<object> Payloads { get; } = [];
        public Task PublishCommandAsync(string zone, Guid deviceId, object payload, CancellationToken cancellationToken) { Payloads.Add(payload); return Task.CompletedTask; }
    }
}
