using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Controllers;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Hubs;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Tests;

public sealed class Sprint5AlertMaintenanceTests
{
    [Fact]
    public async Task AlertService_DeduplicatesActiveConditionAndDeliversWebhook()
    {
        await using var db = Db(); var hub = new FakeHubContext(); var service = new AlertService(db, hub, new FakeHttpFactory(), Options.Create(new AlertOptions { WebhookUrl = "https://webhook.test/alerts" }), NullLogger<AlertService>.Instance);
        var signal = new AlertSignal("PUMP:1:DRY", "Tanque", "Crítica", "Nivel bajo", "Bomba", "1");
        var first = await service.RaiseAsync(signal, default); var second = await service.RaiseAsync(signal, default);
        Assert.Equal(first.Id, second.Id); Assert.Single(db.SystemAlerts); Assert.Single(db.NotificationDeliveries); Assert.Single(hub.Proxy.Messages);
    }

    [Fact]
    public async Task AlertService_KeepsDistinctCommandAttemptsAsSeparateOccurrences()
    {
        await using var db = Db();
        var hub = new FakeHubContext();
        var service = new AlertService(db, hub, new FakeHttpFactory(), Options.Create(new AlertOptions()), NullLogger<AlertService>.Instance);
        const string description = "La válvula no respondió al comando ABRIR_VALVULA dentro del tiempo esperado.";

        foreach (var commandId in new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() })
            await service.RaiseAsync(new AlertSignal($"MQTT:{commandId}:TIMEOUT", "Comando sin ACK", "Crítica", description, "Dispositivo IoT", "valve-a1"), default);

        Assert.Equal(3, db.SystemAlerts.Count());
        Assert.Equal(3, db.SystemAlerts.Select(alert => alert.Fingerprint).Distinct().Count());
        Assert.Equal(3, hub.Proxy.Messages.Count(message => message == "alertRaised"));
        Assert.All(db.SystemAlerts, alert => Assert.Equal(description, alert.Description));
    }

    [Fact]
    public async Task TelemetryRecovery_ResolvesOfflineAlertsAndEmitsSignalR()
    {
        await using var db = Db();
        var active = new MasterCatalogItem { Kind = CatalogKind.OperationalStatus, Code = "ACTIVE", Name = "Activo" };
        var offline = new MasterCatalogItem { Kind = CatalogKind.OperationalStatus, Code = "OFFLINE", Name = "Sin conexión" };
        var unit = new MasterCatalogItem { Kind = CatalogKind.MeasurementUnit, Code = "PERCENT", Name = "Porcentaje", Symbol = "%" };
        var sensorType = new MasterCatalogItem { Kind = CatalogKind.SensorType, Code = "MOISTURE", Name = "Humedad" };
        var deviceType = new MasterCatalogItem { Kind = CatalogKind.DeviceType, Code = "NODE", Name = "Nodo" };
        var node = new IoTNode { Code = "NODE-B", Name = "Nodo B", OperationalStatusId = offline.Id, OperationalStatus = offline };
        var device = new IoTDevice { Code = "DEVICE-B", Name = "Dispositivo B", SerialNumber = "DEVICE-B-001", DeviceTypeId = deviceType.Id, DeviceType = deviceType, OperationalStatusId = offline.Id, OperationalStatus = offline, NodeId = node.Id, Node = node };
        var sensor = new IoTSensor { Code = "SENSOR-B", Name = "Sensor B", SerialNumber = "SENSOR-B-001", MinimumValue = 0, MaximumValue = 100, SensorTypeId = sensorType.Id, SensorType = sensorType, MeasurementUnitId = unit.Id, MeasurementUnit = unit, OperationalStatusId = offline.Id, OperationalStatus = offline, DeviceId = device.Id, Device = device };
        db.AddRange(active, offline, unit, sensorType, deviceType, node, device, sensor);
        db.SystemAlerts.AddRange(
            new SystemAlert { Fingerprint = $"IOT:NODE:{node.Id}:OFFLINE", Type = "Conexión", Severity = "Advertencia", Description = "Nodo sin heartbeat." },
            new SystemAlert { Fingerprint = $"IOT:DEVICE:{device.Id}:OFFLINE", Type = "Conexión", Severity = "Advertencia", Description = "Dispositivo sin heartbeat." },
            new SystemAlert { Fingerprint = $"IOT:SENSOR:{sensor.Id}:OFFLINE", Type = "Conexión", Severity = "Advertencia", Description = "Sensor sin lecturas." });
        await db.SaveChangesAsync();
        var hub = new FakeHubContext();

        var result = await new TelemetryIngestionService(db, hub).IngestAsync(new TelemetryRequest(sensor.Id, null, DateTime.UtcNow, 55, 90, -50, "RECOVERY-B-1", "MQTT_SIMULATED"), "MQTT_SIMULATED", default);

        Assert.Equal(TelemetryIngestionStatus.Accepted, result.Status);
        Assert.All(db.SystemAlerts, alert => Assert.Equal("Resuelta", alert.Status));
        Assert.Equal(3, hub.Proxy.Messages.Count(message => message == TelemetryHub.AlertResolved));
        Assert.Contains(TelemetryHub.ReadingReceived, hub.Proxy.Messages);
    }
    [Fact]
    public async Task Acknowledge_RecordsUserAndTimestamp()
    {
        await using var db = Db(); var alert = new SystemAlert { Fingerprint = "A", Type = "Conexión", Description = "Offline" }; db.Add(alert); await db.SaveChangesAsync();
        var controller = new AlertsController(db, new FakeAlerts()) { ControllerContext = Context("tecnico@test.local") };
        Assert.IsType<NoContentResult>(await controller.Acknowledge(alert.Id, default));
        Assert.Equal("Reconocida", alert.Status); Assert.Equal("tecnico@test.local", alert.AcknowledgedByEmail); Assert.NotNull(alert.AcknowledgedAtUtc);
    }

    [Fact]
    public async Task Escalation_CreatesSingleAutomaticMaintenanceIncident()
    {
        await using var db = Db(); var alert = new SystemAlert { Fingerprint = "CRIT", Type = "Falla de dispositivo", Severity = "Crítica", Description = "ACK agotado", RelatedEntityType = "Válvula", RelatedEntityId = "V1", RaisedAtUtc = DateTime.UtcNow.AddMinutes(-60) }; db.Add(alert); await db.SaveChangesAsync();
        var processor = new AlertEscalationProcessor(db, new FakeAlerts(), Options.Create(new AlertOptions { EscalationMinutes = 5, IncidentMinutes = 10 }));
        await processor.EvaluateAsync(DateTime.UtcNow, default); await processor.EvaluateAsync(DateTime.UtcNow, default);
        var incident = Assert.Single(db.MaintenanceIncidents); Assert.Equal("Automática desde alerta", incident.Origin); Assert.Equal(alert.Id, incident.SystemAlertId); Assert.Equal(1, alert.EscalationLevel);
    }
    [Fact]
    public async Task Alert_RemainsActiveWhenOnlyTimePasses()
    {
        await using var db = Db();
        var alert = new SystemAlert { Fingerprint = "DEVICE:OFFLINE", Type = "Conexión", Severity = "Crítica", Description = "El dispositivo sigue sin conexión.", RaisedAtUtc = DateTime.UtcNow.AddMinutes(-10) };
        db.Add(alert); await db.SaveChangesAsync();
        var processor = new AlertEscalationProcessor(db, new FakeAlerts(), Options.Create(new AlertOptions { EscalationMinutes = 1, IncidentMinutes = 2 }));

        await processor.EvaluateAsync(DateTime.UtcNow.AddHours(1), default);

        Assert.Equal("Activa", alert.Status);
        Assert.Null(alert.ResolvedAtUtc);
    }

    [Fact]
    public async Task MaintenanceController_CreatesPlanAndResolvesIncident()
    {
        await using var db = Db(); var controller = new MaintenanceController(db);
        var planResult = await controller.CreatePlan(new("Inspección", "Recurrente", 30, "Bomba", "P1", DateTime.UtcNow.AddDays(1), null, "tec@test.local", "Revisar", "Pendiente"), default);
        Assert.IsType<OkObjectResult>(planResult); Assert.Single(db.MaintenancePlans);
        var incidentResult = await controller.CreateIncident(new("Falla", "Corriente alta", "Bomba", "P1", "Crítica", null, null, null), default);
        var incident = Assert.IsType<MaintenanceIncident>(Assert.IsType<OkObjectResult>(incidentResult).Value);
        await controller.UpdateIncident(incident.Id, new("Resuelta", null, "tec@test.local", "Motor revisado"), default);
        Assert.NotNull(incident.ResolvedAtUtc);
    }

    [Fact]
    public async Task PumpProtection_RaisesCriticalDetectedAlert()
    {
        await using var db = Db(); var tank = new WaterTank { Name = "Tanque", CapacityLiters = 1000, CurrentLevelLiters = 800, MinimumSafePercent = 15 }; var pump = new WaterPump { WaterTank = tank, Name = "Bomba", Code = "P", MaximumCurrentAmps = 12, IsRunning = true }; db.AddRange(tank, pump); await db.SaveChangesAsync();
        var alerts = new FakeAlerts(); await new Sprint4TelemetryService(db, new FakePumpCommands(), alerts).IngestStationAsync(new("P", DateTime.UtcNow, 800, 2, 18, true, "OVER-1"), default);
        var raised = Assert.Single(alerts.Raised); Assert.Equal("Crítica", raised.Severity); Assert.Equal("Falla de dispositivo", raised.Type);
    }

    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static ControllerContext Context(string email) => new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Email, email)], "test")) } };
    private sealed class FakeAlerts : IAlertService
    {
        public List<AlertSignal> Raised { get; } = [];
        public Task<SystemAlert> RaiseAsync(AlertSignal signal, CancellationToken ct) { Raised.Add(signal); return Task.FromResult(new SystemAlert { Fingerprint = signal.Fingerprint, Type = signal.Type, Severity = signal.Severity, Description = signal.Description }); }
        public Task NotifyAsync(SystemAlert alert, CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class FakePumpCommands : IPumpCommandService { public Task<IoTCommand> SendAsync(WaterPump pump, string commandType, Guid? userId, CancellationToken ct) => Task.FromResult(new IoTCommand { DeviceId = Guid.NewGuid(), CommandType = commandType }); }
    private sealed class FakeHttpFactory : IHttpClientFactory { public HttpClient CreateClient(string name) => new(new Handler()) { BaseAddress = new Uri("https://webhook.test") }; private sealed class Handler : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)); } }
    private sealed class FakeHubContext : IHubContext<TelemetryHub>
    {
        public FakeProxy Proxy { get; } = new(); public IHubClients Clients => new FakeClients(Proxy); public IGroupManager Groups { get; } = new FakeGroups();
    }
    private sealed class FakeProxy : IClientProxy { public List<string> Messages { get; } = []; public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) { Messages.Add(method); return Task.CompletedTask; } }
    private sealed class FakeClients(FakeProxy proxy) : IHubClients
    {
        public IClientProxy All => proxy; public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => proxy; public IClientProxy Client(string connectionId) => proxy; public IClientProxy Clients(IReadOnlyList<string> connectionIds) => proxy; public IClientProxy Group(string groupName) => proxy; public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => proxy; public IClientProxy Groups(IReadOnlyList<string> groupNames) => proxy; public IClientProxy User(string userId) => proxy; public IClientProxy Users(IReadOnlyList<string> userIds) => proxy;
    }
    private sealed class FakeGroups : IGroupManager { public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask; public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask; }
}
