using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Controllers;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Tests;

public sealed class AdvancedModulesTests
{
    [Fact]
    public async Task RuleSimulation_PersistsDecisionWithoutStartingEquipment()
    {
        var setup = await Setup();
        setup.Db.SensorReadings.Add(new SensorReading { SensorId = setup.Sensor.Id, IrrigationZoneId = setup.Zone.Id, CapturedAtUtc = DateTime.UtcNow, Value = 20, IsValid = true, MessageId = "SIMULATION-01" });
        var rule = new IrrigationRule { IrrigationZoneId = setup.Zone.Id, Name = "Regla simulada", MinimumMoisturePercent = 30, TargetMoisturePercent = 45, AllowedFrom = TimeOnly.MinValue, AllowedUntil = TimeOnly.MaxValue };
        setup.Db.Add(rule); await setup.Db.SaveChangesAsync();

        Assert.IsType<OkObjectResult>(await setup.Controller.Simulate(rule.Id, default));
        var evaluation = await setup.Db.IrrigationRuleEvaluations.SingleAsync();
        Assert.True(evaluation.IsSimulation); Assert.Equal("Regaría", evaluation.Decision); Assert.Empty(setup.Db.IrrigationRuns);
    }

    [Fact]
    public async Task ScheduleCrud_StoresFutureRecurrenceAndValidatesRange()
    {
        var setup = await Setup();
        var invalid = await setup.Controller.CreateSchedule(new("Inválida", setup.Zone.Id, DateTime.UtcNow.AddHours(1), "Único", null, 0, 12, true), default);
        Assert.IsType<BadRequestObjectResult>(invalid);
        var valid = await setup.Controller.CreateSchedule(new("Cada dos días", setup.Zone.Id, DateTime.UtcNow.AddHours(1), "Recurrente", 2, 20, 12, true), default);
        Assert.IsType<OkObjectResult>(valid);
        var saved = await setup.Db.IrrigationSchedules.SingleAsync(); Assert.Equal(2, saved.IntervalDays); Assert.Equal("admin@test.local", saved.CreatedByEmail);
    }

    [Fact]
    public async Task AutomaticFill_EvaluatesConfiguredTankThresholds()
    {
        var setup = await Setup();
        var tank = new WaterTank { Name = "Reserva", CapacityLiters = 1000, CurrentLevelLiters = 150 };
        var pump = new WaterPump { WaterTank = tank, Name = "Bomba", Code = "P-AUTO" };
        var source = new WaterSource { Code = "POZO", Name = "Pozo", MaximumFlowLitersMinute = 50 };
        setup.Db.AddRange(tank, pump, source); await setup.Db.SaveChangesAsync();

        Assert.IsType<OkObjectResult>(await setup.Controller.SaveAutoFill(new(tank.Id, pump.Id, source.Id, 25, 90, true), default));
        Assert.Equal("Solicitar llenado", (await setup.Db.AutomaticFillConfigurations.SingleAsync()).LastDecision);
    }

    [Fact]
    public async Task EfficiencyAnalysis_ComputesSavingAndDetectsAnomaly()
    {
        var setup = await Setup();
        setup.Db.WaterEfficiencyBaselines.Add(new WaterEfficiencyBaseline { IrrigationZoneId = setup.Zone.Id, Name = "Base", LitersPerEvent = 100, AnomalyThresholdPercent = 20 });
        var run = new IrrigationRun { IrrigationZoneId = setup.Zone.Id, Reason = "Prueba", PlannedDurationMinutes = 10, FlowRateLitersMinute = 13 };
        setup.Db.Add(run); await setup.Db.SaveChangesAsync();
        setup.Db.WaterConsumptionRecords.Add(new WaterConsumptionRecord { IrrigationRunId = run.Id, IrrigationZoneId = setup.Zone.Id, VolumeLiters = 130, RecordedAtUtc = DateTime.UtcNow }); await setup.Db.SaveChangesAsync();

        var result = Assert.IsType<OkObjectResult>(await setup.Controller.Efficiency(30, default));
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("\"totalSavingLiters\":-30", json); Assert.Contains("\"isAnomaly\":true", json);
    }

    [Fact]
    public async Task WorkOrderAndNotificationSummary_AreAvailableFromUiContracts()
    {
        var setup = await Setup();
        Assert.IsType<OkObjectResult>(await setup.Controller.WorkOrder(new WorkOrderRequest("Correctivo", "Revisar válvula", "Válvula", Guid.NewGuid().ToString(), "Ana", "ana@test.local", DateTime.UtcNow.AddDays(2), "Pendiente", null), default));
        setup.Db.SystemAlerts.Add(new SystemAlert { Fingerprint = "TEST:1", Type = "Flujo", Severity = "Crítica", Description = "Desviación", RaisedAtUtc = DateTime.UtcNow }); await setup.Db.SaveChangesAsync();
        Assert.Single(await setup.Db.MaintenanceWorkOrders.ToListAsync());
        var summary = Assert.IsType<OkObjectResult>(await setup.Controller.NotificationSummary("daily", default));
        Assert.Contains("\"critical\":1", System.Text.Json.JsonSerializer.Serialize(summary.Value));
    }

    private static async Task<Fixture> Setup()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var status = new MasterCatalogItem { Kind = CatalogKind.OperationalStatus, Code = "ACTIVE", Name = "Activo" };
        var sensorType = new MasterCatalogItem { Kind = CatalogKind.SensorType, Code = "SOIL", Name = "Humedad" };
        var unit = new MasterCatalogItem { Kind = CatalogKind.MeasurementUnit, Code = "PCT", Name = "Porcentaje" };
        var center = new UniversityCenter { Code = "C", Name = "Centro" }; var farm = new Farm { UniversityCenter = center, Code = "F", Name = "Finca" }; var block = new FarmBlock { Farm = farm, Code = "B", Name = "Bloque" }; var sector = new IrrigationSector { FarmBlock = block, Code = "S", Name = "Sector" };
        var sensor = new IoTSensor { Code = "H", Name = "Humedad", SerialNumber = "S1", SensorType = sensorType, MeasurementUnit = unit, OperationalStatus = status };
        var zone = new IrrigationZone { IrrigationSector = sector, Code = "Z", Name = "Zona", AreaHectares = 1, OperationalStatus = status, PrimarySensor = sensor };
        db.AddRange(status, sensorType, unit, center, farm, block, sector, sensor, zone); await db.SaveChangesAsync();
        var controller = new AdvancedModulesController(db, new FakeHttpFactory()) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, "admin@test.local")], "test")) } } };
        return new(db, controller, zone, sensor);
    }

    private sealed record Fixture(AppDbContext Db, AdvancedModulesController Controller, IrrigationZone Zone, IoTSensor Sensor);
    private sealed class FakeHttpFactory : IHttpClientFactory { public HttpClient CreateClient(string name) => new(new Handler()); private sealed class Handler : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)); } }
}
