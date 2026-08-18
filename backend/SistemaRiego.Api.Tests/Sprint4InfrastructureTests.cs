using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Tests;

public sealed class Sprint4InfrastructureTests
{
    [Fact]
    public async Task StationTelemetry_LowLevel_TripsDryRunProtectionAndRequestsStop()
    {
        await using var db = Db(); var tank = new WaterTank { Name = "Tanque", CapacityLiters = 1000, CurrentLevelLiters = 500, MinimumSafePercent = 15 };
        var pump = new WaterPump { WaterTank = tank, Name = "Bomba", Code = "P-01", IsRunning = true, MaximumCurrentAmps = 12 };
        db.AddRange(tank, pump); await db.SaveChangesAsync(); var commands = new FakePumpCommands();

        await new Sprint4TelemetryService(db, commands).IngestStationAsync(new("P-01", DateTime.UtcNow, 100, 2.4m, 7, true, "ST-1"), default);

        Assert.True(pump.HasUnacknowledgedFault); Assert.Contains("seco", pump.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("APAGAR_BOMBA", Assert.Single(commands.Commands)); Assert.Single(db.PumpStationReadings);
    }

    [Fact]
    public async Task EnergyTelemetry_PersistsLowBatteryAlarm()
    {
        await using var db = Db(); var array = new SolarPanelArray { Name = "Paneles", PanelCount = 4, RatedPowerWatts = 1800 };
        var battery = new SolarBattery { Name = "Batería", CapacityWattHours = 5000, NominalVoltage = 48, MinimumSafeChargePercent = 20 };
        db.ChargeControllers.Add(new ChargeController { Name = "MPPT", SolarPanelArray = array, SolarBattery = battery, RatedCurrentAmps = 60 }); await db.SaveChangesAsync();

        await new Sprint4TelemetryService(db, new FakePumpCommands()).IngestEnergyAsync(new(DateTime.UtcNow, 100, 15, 250, 48, "EN-1"), default);

        Assert.Equal("Batería baja", battery.Status); Assert.Single(db.EnergyReadings);
        Assert.Contains(db.OperationalEvents, x => x.EventType == "LOW_BATTERY");
    }

    [Fact]
    public async Task HydraulicCapacity_UsesFlowPressureAndEmergencyInterlock()
    {
        await using var db = Db(); var tank = new WaterTank { Name = "Tanque", CapacityLiters = 1000, CurrentLevelLiters = 800, MinimumSafePercent = 15 };
        db.WaterPumps.Add(new WaterPump { WaterTank = tank, Name = "Bomba", Code = "P-01", RatedFlowLitersMinute = 36, NominalValveFlowLitersMinute = 12, LastPressureBar = 2.5m, MinimumPressureBar = 1.2m });
        db.GlobalParameters.Add(new GlobalParameter { Key = "MAX_SIMULTANEOUS_VALVES", Value = "5", DataType = "integer", Category = "Test", Description = "Test" });
        db.SystemSafetyStates.Add(new SystemSafetyState()); await db.SaveChangesAsync(); var service = new WaterCapacityService(db);

        Assert.Equal(2, await service.GetMaximumValveCountAsync(default));
        (await db.SystemSafetyStates.FindAsync(1))!.EmergencyStopActive = true; await db.SaveChangesAsync();
        Assert.Equal(0, await service.GetMaximumValveCountAsync(default));
    }

    [Fact]
    public async Task Consumption_UsesMeasuredFlowAndCalculatesCost()
    {
        await using var db = Db(); var zone = new IrrigationZone { IrrigationSectorId = Guid.NewGuid(), OperationalStatusId = Guid.NewGuid(), Code = "Z1", Name = "Zona 1" };
        var started = DateTime.UtcNow.AddMinutes(-10); var run = new IrrigationRun { IrrigationZone = zone, Mode = "Manual", Status = "En curso", PlannedDurationMinutes = 10, FlowRateLitersMinute = 12, StartedAtUtc = started, Reason = "Test" };
        db.AddRange(zone, run); await db.SaveChangesAsync();
        db.FlowReadings.AddRange(new FlowReading { IrrigationZoneId = zone.Id, CapturedAtUtc = started.AddMinutes(2), FlowLitersMinute = 10, MessageId = "F1" }, new FlowReading { IrrigationZoneId = zone.Id, CapturedAtUtc = started.AddMinutes(8), FlowLitersMinute = 14, MessageId = "F2" });
        db.GlobalParameters.Add(new GlobalParameter { Key = "WATER_TARIFF_PER_M3", Value = "5", DataType = "decimal", Category = "Test", Description = "Test" }); await db.SaveChangesAsync();

        var result = await new ConsumptionCalculator(db).BuildAsync(run, started.AddMinutes(10), default);

        Assert.True(result.IsMeasured); Assert.Equal("Caudal real simulado", result.Source); Assert.Equal(120, result.VolumeLiters); Assert.Equal(.6m, result.EstimatedCost);
    }

    [Fact]
    public async Task StationTelemetry_Overcurrent_TripsProtectionAndRequestsStop()
    {
        await using var db = Db(); var tank = new WaterTank { Name = "Tanque", CapacityLiters = 1000, CurrentLevelLiters = 800, MinimumSafePercent = 15 };
        var pump = new WaterPump { WaterTank = tank, Name = "Bomba", Code = "P-02", IsRunning = true, MaximumCurrentAmps = 12 };
        db.AddRange(tank, pump); await db.SaveChangesAsync(); var commands = new FakePumpCommands();
        await new Sprint4TelemetryService(db, commands).IngestStationAsync(new("P-02", DateTime.UtcNow, 800, 2.4m, 18, true, "ST-OVER"), default);
        Assert.True(pump.HasUnacknowledgedFault); Assert.Contains("Sobrecorriente", pump.FailureReason); Assert.Equal("APAGAR_BOMBA", Assert.Single(commands.Commands));
    }

    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private sealed class FakePumpCommands : IPumpCommandService
    {
        public List<string> Commands { get; } = [];
        public Task<IoTCommand> SendAsync(WaterPump pump, string commandType, Guid? userId, CancellationToken ct) { Commands.Add(commandType); return Task.FromResult(new IoTCommand { DeviceId = Guid.NewGuid(), CommandType = commandType }); }
    }
}
