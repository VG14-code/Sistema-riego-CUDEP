using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Controllers;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Tests;

public sealed class CropPlanningControllerTests
{
    [Fact]
    public async Task Create_RejectsOverlappingCycleInSameZone()
    {
        var (db, crop, zone, stage) = await Setup();
        db.CropCycles.Add(new CropCycle { CropId=crop.Id, IrrigationZoneId=zone.Id, CurrentStageId=stage.Id, Name="Existente", SowingDate=new(2026,8,1), ExpectedHarvestDate=new(2026,10,1), AreaHectares=1, Status="Activo" });
        await db.SaveChangesAsync();
        var result = await new CropPlanningController(db).Create(Request(crop.Id, zone.Id, stage.Id, new(2026,9,1), new(2026,11,1)), default);
        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Single(await db.CropCycles.ToListAsync());
    }

    [Fact]
    public async Task Create_AllowsNonOverlappingCycle()
    {
        var (db, crop, zone, stage) = await Setup();
        var result = await new CropPlanningController(db).Create(Request(crop.Id, zone.Id, stage.Id, new(2026,8,1), new(2026,10,1)), default);
        Assert.IsType<OkObjectResult>(result);
        Assert.Single(await db.CropCycles.ToListAsync());
    }

    [Fact]
    public async Task Create_RejectsStageFromAnotherCrop()
    {
        var (db, crop, zone, _) = await Setup();
        var otherType=new CropType{Code="OTRO",Name="Otro"};var other=new Crop{CropType=otherType,Code="C2",Name="Cultivo 2"};var wrong=new PhenologicalStage{Crop=other,Name="Inicio",Sequence=1,EstimatedDays=10};db.AddRange(otherType,other,wrong);await db.SaveChangesAsync();
        var result=await new CropPlanningController(db).Create(Request(crop.Id,zone.Id,wrong.Id,new(2026,8,1),new(2026,9,1)),default);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static CycleRequest Request(Guid crop,Guid zone,Guid stage,DateOnly start,DateOnly end)=>new(crop,zone,stage,"Ciclo de prueba",start,end,null,1,100,"Planificado",null);
    private static async Task<(AppDbContext Db,Crop Crop,IrrigationZone Zone,PhenologicalStage Stage)> Setup()
    {
        var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var active=new MasterCatalogItem{Kind=CatalogKind.OperationalStatus,Code="ACTIVE",Name="Activo"};var center=new UniversityCenter{Code="C",Name="Centro"};var farm=new Farm{UniversityCenter=center,Code="F",Name="Finca"};var block=new FarmBlock{Farm=farm,Code="B",Name="Bloque"};var sector=new IrrigationSector{FarmBlock=block,Code="S",Name="Sector"};var zone=new IrrigationZone{IrrigationSector=sector,OperationalStatus=active,Code="Z",Name="Zona",AreaHectares=2};
        var type=new CropType{Code="HORT",Name="Hortaliza"};var crop=new Crop{CropType=type,Code="TOM",Name="Tomate"};var stage=new PhenologicalStage{Crop=crop,Name="Inicial",Sequence=1,EstimatedDays=20};db.AddRange(active,center,farm,block,sector,zone,type,crop,stage);await db.SaveChangesAsync();return(db,crop,zone,stage);
    }
}
