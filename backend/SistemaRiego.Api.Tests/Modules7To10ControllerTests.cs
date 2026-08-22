using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Controllers;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Tests;

public sealed class Modules7To10ControllerTests
{
    [Fact]
    public async Task ManualIrrigation_StartAndStop_RegistersConsumptionAndPausesRule()
    {
        var setup=await CreateSetup();
        var started=await setup.Manual.Start(new ManualIrrigationRequest(setup.Zone.Id,20,10,"Prueba autorizada","Ensayo"),default);
        var ok=Assert.IsType<OkObjectResult>(started);
        var run=await setup.Db.IrrigationRuns.SingleAsync();
        Assert.Equal("En curso",run.Status);
        Assert.True((await setup.Db.IrrigationRules.SingleAsync()).SuspendedUntilUtc>DateTime.UtcNow);

        var stopped=await setup.Manual.Stop(run.Id,new StopIrrigationRequest("Finalizada"),default);
        Assert.IsType<OkObjectResult>(stopped);
        Assert.Equal("Detenido",(await setup.Db.IrrigationRuns.FindAsync(run.Id))!.Status);
        Assert.Equal(10m,(await setup.Db.WaterConsumptionRecords.SingleAsync()).VolumeLiters);
    }

    [Fact]
    public async Task ManualIrrigation_UsesAggregateSafeVolumeAcrossActiveTanks()
    {
        var setup=await CreateSetup();
        setup.Tank.CurrentLevelLiters=105;
        var second=new WaterTank{Name="Tanque B",CapacityLiters=1000,CurrentLevelLiters=200,MinimumSafePercent=10,MaximumFillPercent=95};
        setup.Db.WaterTanks.Add(second);await setup.Db.SaveChangesAsync();
        var started=await setup.Manual.Start(new ManualIrrigationRequest(setup.Zone.Id,1,10,"Prueba multi-tanque",null),default);
        Assert.IsType<OkObjectResult>(started);
        var run=await setup.Db.IrrigationRuns.SingleAsync();
        Assert.IsType<OkObjectResult>(await setup.Manual.Stop(run.Id,new StopIrrigationRequest("Fin"),default));
        Assert.Equal(105,setup.Tank.CurrentLevelLiters);
        Assert.Equal(190,second.CurrentLevelLiters);
    }

    [Fact]
    public async Task Pump_Start_RejectsTankAtMaximumSafeLevel()
    {
        var setup=await CreateSetup();
        setup.Tank.CurrentLevelLiters=960;await setup.Db.SaveChangesAsync();
        var result=await setup.Supply.Start(setup.Pump.Id,new PumpCommandRequest("Prueba"),default);
        Assert.IsType<ConflictObjectResult>(result);
        Assert.False((await setup.Db.WaterPumps.FindAsync(setup.Pump.Id))!.IsRunning);
    }

    [Fact]
    public async Task Tanks_Crud_SupportsMultipleIndependentReservoirs()
    {
        var setup=await CreateSetup();
        var created=await setup.Supply.CreateTank(new WaterTankRequest("Tanque secundario",2500,1200,20,90,[setup.Pump.Id]),default);
        Assert.IsType<CreatedAtActionResult>(created);
        var second=await setup.Db.WaterTanks.SingleAsync(x=>x.Name=="Tanque secundario");
        await setup.Supply.UpdateTank(second.Id,new WaterTankRequest("Tanque experimental",3000,1500,25,92),default);
        Assert.Equal(3000,(await setup.Db.WaterTanks.FindAsync(second.Id))!.CapacityLiters);
        Assert.Equal(second.Id,(await setup.Db.WaterPumps.FindAsync(setup.Pump.Id))!.WaterTankId);
        Assert.IsType<NoContentResult>(await setup.Supply.DeactivateTank(second.Id,default));
        Assert.Equal("Inactivo",(await setup.Db.WaterTanks.FindAsync(second.Id))!.Status);
        Assert.Equal(2,await setup.Db.WaterTanks.CountAsync());
    }
    [Fact]
    public async Task Pump_Start_UsesMinimumThresholdEditedThroughTankCrud()
    {
        var setup=await CreateSetup();
        var updated=await setup.Supply.UpdateTank(setup.Tank.Id,new WaterTankRequest("Tanque",1000,800,85,95,[setup.Pump.Id],true),default);
        Assert.IsType<NoContentResult>(updated);
        var result=await setup.Supply.Start(setup.Pump.Id,new PumpCommandRequest("Validar umbral configurable"),default);
        Assert.IsType<ConflictObjectResult>(result);
        Assert.False((await setup.Db.WaterPumps.FindAsync(setup.Pump.Id))!.IsRunning);
    }

    [Fact]
    public async Task Tank_CannotBeDeactivatedWhileAssociatedPumpIsRunning()
    {
        var setup=await CreateSetup();
        setup.Pump.IsRunning=true;
        await setup.Db.SaveChangesAsync();
        var result=await setup.Supply.UpdateTank(setup.Tank.Id,new WaterTankRequest("Tanque",1000,800,10,95,[setup.Pump.Id],false),default);
        Assert.IsType<ConflictObjectResult>(result);
        Assert.NotEqual("Inactivo",(await setup.Db.WaterTanks.FindAsync(setup.Tank.Id))!.Status);
    }
    [Fact]
    public async Task Automation_Evaluate_StartsRunWhenMoistureIsBelowThreshold()
    {
        var setup=await CreateSetup();
        setup.Rule.AllowedFrom=TimeOnly.MinValue;setup.Rule.AllowedUntil=TimeOnly.MaxValue;
        setup.Db.SensorReadings.Add(new SensorReading{SensorId=setup.Sensor.Id,IrrigationZoneId=setup.Zone.Id,CapturedAtUtc=DateTime.UtcNow,Value=30,MessageId="TEST-AUTO-01"});await setup.Db.SaveChangesAsync();
        var result=await setup.Automation.Evaluate(default);
        Assert.IsType<OkObjectResult>(result);
        Assert.Contains(await setup.Db.IrrigationRuns.ToListAsync(),x=>x.Mode=="Automático"&&x.Status=="En curso");
    }

    private static async Task<Setup> CreateSetup()
    {
        var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var active=new MasterCatalogItem{Kind=CatalogKind.OperationalStatus,Code="ACTIVE",Name="Activo"};
        var type=new MasterCatalogItem{Kind=CatalogKind.SensorType,Code="SOIL_MOISTURE",Name="Humedad"};
        var unit=new MasterCatalogItem{Kind=CatalogKind.MeasurementUnit,Code="PERCENT",Name="Porcentaje"};db.AddRange(active,type,unit);
        var center=new UniversityCenter{Code="C",Name="Centro"};var farm=new Farm{UniversityCenter=center,Code="F",Name="Finca"};var block=new FarmBlock{Farm=farm,Code="B",Name="Bloque"};var sector=new IrrigationSector{FarmBlock=block,Code="S",Name="Sector"};
        var sensor=new IoTSensor{Code="H",Name="Humedad",SerialNumber="1",SensorType=type,MeasurementUnit=unit,OperationalStatus=active};var zone=new IrrigationZone{IrrigationSector=sector,Code="Z",Name="Zona",AreaHectares=1,OperationalStatus=active,PrimarySensor=sensor};db.AddRange(center,farm,block,sector,sensor,zone);await db.SaveChangesAsync();
        var rule=new IrrigationRule{IrrigationZoneId=zone.Id,Name="Regla",MinimumMoisturePercent=40,TargetMoisturePercent=55,MaximumDurationMinutes=15,AllowedFrom=TimeOnly.MinValue,AllowedUntil=TimeOnly.MaxValue};
        var tank=new WaterTank{Name="Tanque",CapacityLiters=1000,CurrentLevelLiters=800,MinimumSafePercent=10,MaximumFillPercent=95};var pump=new WaterPump{WaterTank=tank,Name="Bomba",LastStoppedAtUtc=DateTime.UtcNow.AddHours(-1)};db.AddRange(rule,tank,pump);await db.SaveChangesAsync();
        var user=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,Guid.NewGuid().ToString()),new Claim(ClaimTypes.Email,"admin@test.local"),new Claim(ClaimTypes.Role,RoleNames.Administrator)],"tests"));
        T With<T>(T controller) where T:ControllerBase{controller.ControllerContext=new ControllerContext{HttpContext=new DefaultHttpContext{User=user}};return controller;}
        return new Setup(db,With(new ManualIrrigationController(db)),With(new WaterSupplyController(db)),With(new AutomationController(db)),zone,sensor,rule,tank,pump);
    }
    private sealed record Setup(AppDbContext Db,ManualIrrigationController Manual,WaterSupplyController Supply,AutomationController Automation,IrrigationZone Zone,IoTSensor Sensor,IrrigationRule Rule,WaterTank Tank,WaterPump Pump);
}
