using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Tests;

public sealed class AutomaticOperationsNotificationTests
{
    [Fact]
    public async Task ScheduledIrrigation_SendsMqttCommandAndAdvancesRecurrence()
    {
        await using var db = Db(); var zone = await SeedZone(db); db.WaterTanks.Add(new WaterTank { Name = "Reserva", CapacityLiters = 1000, CurrentLevelLiters = 900, MinimumSafePercent = 10 });
        var schedule = new IrrigationSchedule { Name = "Madrugada", IrrigationZoneId = zone.Id, NextRunAtUtc = DateTime.UtcNow.AddMinutes(-1), Recurrence = "Recurrente", IntervalDays = 1, DurationMinutes = 15, FlowRateLitersMinute = 12 };
        db.Add(schedule); await db.SaveChangesAsync(); var irrigation = new FakeIrrigationCommands();
        using var provider = new ServiceCollection().AddSingleton(db).AddSingleton<IIrrigationCommandService>(irrigation).AddSingleton<IWaterCapacityService>(new FixedCapacity()).BuildServiceProvider();

        await AdvancedOperationsWorker.ProcessSchedules(provider, default);

        Assert.Single(irrigation.Calls); Assert.Equal("ABRIR_VALVULA", irrigation.Calls[0]);
        Assert.Equal("Esperando ACK", (await db.IrrigationRuns.SingleAsync()).Status); Assert.True(schedule.NextRunAtUtc > DateTime.UtcNow); Assert.Contains("solicitado automáticamente", schedule.LastResult);
    }

    // El trabajador solo miraba que el maximo no fuera cero: dos programaciones vencidas
    // abrian dos zonas a la vez aunque el limite configurado fuera una sola valvula.
    [Fact]
    public async Task ScheduledIrrigation_RespectsTheSimultaneousValveLimit()
    {
        await using var db = Db(); var first = await SeedZone(db, "Z1"); var second = await SeedZone(db, "Z2");
        db.WaterTanks.Add(new WaterTank { Name = "Reserva", CapacityLiters = 5000, CurrentLevelLiters = 4500, MinimumSafePercent = 10 });
        foreach (var zone in new[] { first, second })
            db.Add(new IrrigationSchedule { Name = "Programa " + zone.Code, IrrigationZoneId = zone.Id, NextRunAtUtc = DateTime.UtcNow.AddMinutes(-1), Recurrence = "Único", DurationMinutes = 10, FlowRateLitersMinute = 12 });
        await db.SaveChangesAsync(); var irrigation = new FakeIrrigationCommands();
        using var provider = new ServiceCollection().AddSingleton(db).AddSingleton<IIrrigationCommandService>(irrigation).AddSingleton<IWaterCapacityService>(new FixedCapacity(1)).BuildServiceProvider();

        await AdvancedOperationsWorker.ProcessSchedules(provider, default);

        Assert.Single(irrigation.Calls);
        Assert.Single(await db.IrrigationRuns.ToListAsync());
        var postponed = await db.IrrigationSchedules.SingleAsync(x => x.LastRunAtUtc == null);
        Assert.Contains("Capacidad simultánea", postponed.LastResult);
    }

    [Fact]
    public async Task AutomaticFill_SendsOnlyOneStartWhileAckIsPending()
    {
        await using var db = Db(); var tank = new WaterTank { Name = "Reserva", CapacityLiters = 1000, CurrentLevelLiters = 100, MinimumSafePercent = 5 };
        var pump = new WaterPump { WaterTank = tank, Name = "Bomba", Code = "AUTO-01", IoTDeviceId = Guid.NewGuid() }; var source = new WaterSource { Code = "POZO", Name = "Pozo", MaximumFlowLitersMinute = 50 };
        db.AddRange(tank, pump, source); await db.SaveChangesAsync(); db.AutomaticFillConfigurations.Add(new AutomaticFillConfiguration { WaterTankId = tank.Id, WaterPumpId = pump.Id, WaterSourceId = source.Id, StartAtPercent = 20, StopAtPercent = 90 }); await db.SaveChangesAsync();
        var commands = new FakePumpCommands(db); using var provider = new ServiceCollection().AddSingleton(db).AddSingleton<IPumpCommandService>(commands).BuildServiceProvider();

        await AdvancedOperationsWorker.ProcessAutomaticFill(provider, default); await AdvancedOperationsWorker.ProcessAutomaticFill(provider, default);

        Assert.Single(commands.Calls); Assert.Single(await db.WaterSupplyEvents.ToListAsync()); Assert.Contains("ACK", (await db.AutomaticFillConfigurations.SingleAsync()).LastDecision);
    }

    [Fact]
    public async Task NotificationRules_DeliverEmailAndWebhooksWithHistory()
    {
        await using var db = Db(); db.NotificationRules.Add(new NotificationRule { Name = "Críticas", AlertType = "Todos", MinimumSeverity = "Advertencia", Channels = "Correo,n8n,Telegram,Teams", Recipients = "ops@test.local", DailySummary = true, WeeklySummary = true });
        foreach (var (key, value) in new[] { ("N8N_WEBHOOK_URL", "https://n8n.test/hook"), ("TELEGRAM_WEBHOOK_URL", "https://telegram.test/hook"), ("TEAMS_WEBHOOK_URL", "https://teams.test/hook") }) db.GlobalParameters.Add(new GlobalParameter { Key = key, Value = value, DataType = "text", Category = "Integraciones", Description = key });
        var alert = new SystemAlert { Fingerprint = "AUTO:1", Type = "Flujo", Severity = "Crítica", Description = "Consumo anormal" }; db.Add(alert); await db.SaveChangesAsync();
        var http = new FakeHttpFactory(); var mail = new FakeMail(); var dispatcher = new NotificationDispatcher(db, http, mail, Options.Create(new AlertOptions()), NullLogger<NotificationDispatcher>.Instance);

        await dispatcher.DispatchAlertAsync(alert, default);

        Assert.Equal(4, await db.NotificationDeliveries.CountAsync()); Assert.All(await db.NotificationDeliveries.ToListAsync(), x => Assert.Equal("Entregado", x.Status));
        Assert.Equal(3, http.Requests); Assert.Single(mail.Messages); Assert.Equal(3, await db.IntegrationExecutions.CountAsync());
    }

    [Fact]
    public async Task SummaryDispatcher_SendsDailySummaryOnlyToEnabledRules()
    {
        await using var db = Db(); db.NotificationRules.AddRange(new NotificationRule { Name = "Diario", Channels = "Correo", Recipients = "daily@test.local", DailySummary = true }, new NotificationRule { Name = "Sin resumen", Channels = "Correo", Recipients = "skip@test.local" });
        db.SystemAlerts.Add(new SystemAlert { Fingerprint = "SUM:1", Type = "Energía", Severity = "Advertencia", Description = "Batería baja" }); await db.SaveChangesAsync(); var mail = new FakeMail();
        var dispatcher = new NotificationDispatcher(db, new FakeHttpFactory(), mail, Options.Create(new AlertOptions()), NullLogger<NotificationDispatcher>.Instance);

        await dispatcher.DispatchSummaryAsync("daily", DateTime.UtcNow.AddDays(-1), "Resumen diario 2026-09-19", default);

        Assert.Single(mail.Messages); Assert.Contains("1 alertas", mail.Messages[0].Body); Assert.Single(await db.IntegrationExecutions.ToListAsync());
    }

    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static async Task<IrrigationZone> SeedZone(AppDbContext db, string code = "Z")
    {
        var status = new MasterCatalogItem { Kind = CatalogKind.OperationalStatus, Code = "ACTIVE", Name = "Activo" }; var center = new UniversityCenter { Code = "C" + code, Name = "Centro" }; var farm = new Farm { UniversityCenter = center, Code = "F" + code, Name = "Finca" }; var block = new FarmBlock { Farm = farm, Code = "B" + code, Name = "Bloque" }; var sector = new IrrigationSector { FarmBlock = block, Code = "S" + code, Name = "Sector" }; var zone = new IrrigationZone { IrrigationSector = sector, Code = code, Name = "Zona " + code, AreaHectares = 1, OperationalStatus = status };
        var deviceType = new MasterCatalogItem { Kind = CatalogKind.DeviceType, Code = "SOLENOID_VALVE", Name = "Válvula" };
        var valve = new IoTDevice { Code = "VAL-" + code, Name = "Válvula " + code, SerialNumber = "VAL-" + code, DeviceType = deviceType, OperationalStatus = status };
        db.AddRange(status, deviceType, center, farm, block, sector, zone, valve); await db.SaveChangesAsync();
        db.IrrigationZoneValves.Add(new IrrigationZoneValve { IrrigationZoneId = zone.Id, DeviceId = valve.Id }); await db.SaveChangesAsync(); return zone;
    }
    private sealed class FixedCapacity(int maximum = 2) : IWaterCapacityService { public Task<int> GetMaximumValveCountAsync(CancellationToken ct) => Task.FromResult(maximum); }
    private sealed class FakeIrrigationCommands : IIrrigationCommandService { public List<string> Calls { get; } = []; public Task<IReadOnlyList<IoTCommand>> SendAsync(Guid zoneId, IrrigationRun? run, string commandType, Guid? userId, CancellationToken ct) { Calls.Add(commandType); return Task.FromResult<IReadOnlyList<IoTCommand>>([]); } public Task ReconcileAsync(CancellationToken ct) => Task.CompletedTask; }
    private sealed class FakePumpCommands(AppDbContext db) : IPumpCommandService { public List<string> Calls { get; } = []; public async Task<IoTCommand> SendAsync(WaterPump pump, string commandType, Guid? userId, CancellationToken ct) { Calls.Add(commandType); var command = new IoTCommand { DeviceId = pump.IoTDeviceId!.Value, CommandType = commandType, Status = "Publicado", RequestedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(1) }; db.Add(command); await db.SaveChangesAsync(ct); return command; } }
    private sealed class FakeMail : IEmailSender { public List<(string Recipient, string Subject, string Body)> Messages { get; } = []; public Task SendPasswordRecoveryAsync(string recipient, string displayName, string resetLink, DateTime expiresAtUtc, CancellationToken ct) => Task.CompletedTask; public Task SendNotificationAsync(string recipient, string subject, string body, CancellationToken ct) { Messages.Add((recipient, subject, body)); return Task.CompletedTask; } }
    private sealed class FakeHttpFactory : IHttpClientFactory { public int Requests { get; private set; } public HttpClient CreateClient(string name) => new(new Handler(() => Requests++)); private sealed class Handler(Action sent) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { sent(); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)); } } }
}
