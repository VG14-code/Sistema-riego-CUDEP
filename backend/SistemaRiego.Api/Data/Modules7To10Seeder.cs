using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Data;

public static class Modules7To10Seeder
{
    public static async Task SeedAsync(AppDbContext db, bool includeDemoData)
    {
        var zone=await db.IrrigationZones.OrderBy(x=>x.Name).FirstOrDefaultAsync();
        if(zone is null)return;
        if(!await db.IrrigationRules.AnyAsync())
        {
            var requirement=await db.CropWaterRequirements.OrderByDescending(x=>x.PhenologicalStageId!=null).FirstOrDefaultAsync();
            db.IrrigationRules.Add(new IrrigationRule{IrrigationZoneId=zone.Id,CropWaterRequirementId=requirement?.Id,Name="Riego automático tomate A1",MinimumMoisturePercent=requirement?.MinimumMoisturePercent??42,TargetMoisturePercent=requirement?.TargetMoisturePercent??58,HysteresisPercent=2,Priority=1,MaximumDurationMinutes=requirement?.BaseDurationMinutes??20,AllowedFrom=new TimeOnly(5,0),AllowedUntil=new TimeOnly(20,0),AllowedDays="1,2,3,4,5,6,7",IsEnabled=true,LastDecision="Lista",LastReason="Esperando evaluación de humedad."});
        }
        if(!await db.WaterTanks.AnyAsync())
        {
            var tank=new WaterTank{Name="Tanque principal CUDEP",CapacityLiters=10000,CurrentLevelLiters=7200,MinimumSafePercent=15,MaximumFillPercent=95,Status="Disponible",LastLevelReadingUtc=DateTime.UtcNow};
            var pump=new WaterPump{WaterTank=tank,Name="Bomba de abastecimiento 1",Status="Detenida",MaximumRunMinutes=45,MinimumRestMinutes=10,LastStoppedAtUtc=DateTime.UtcNow.AddHours(-2)};
            db.AddRange(tank,pump);
        }
        // Riegos historicos de muestra: solo en entornos de demostracion.
        if(includeDemoData && !await db.IrrigationRuns.AnyAsync())
        {
            var user=await db.Users.OrderBy(x=>x.CreatedAtUtc).FirstOrDefaultAsync();
            for(var i=6;i>=1;i--)
            {
                var ended=DateTime.UtcNow.Date.AddDays(-i).AddHours(6);var duration=14+i;var flow=12m;var run=new IrrigationRun{IrrigationZoneId=zone.Id,Mode=i%3==0?"Manual":"Automático",Status="Completado",PlannedDurationMinutes=duration,FlowRateLitersMinute=flow,RequestedAtUtc=ended.AddMinutes(-duration-1),StartedAtUtc=ended.AddMinutes(-duration),EndedAtUtc=ended,RequestedByUserId=user?.Id,RequestedByEmail=user?.Email,Reason=i%3==0?"Riego preventivo autorizado":"Humedad por debajo del umbral",Observations="Registro demostrativo persistido."};
                db.IrrigationRuns.Add(run);await db.SaveChangesAsync();
                db.WaterConsumptionRecords.Add(new WaterConsumptionRecord{IrrigationRunId=run.Id,IrrigationZoneId=zone.Id,Source="Estimado",FlowRateLitersMinute=flow,DurationMinutes=duration,VolumeLiters=duration*flow,RecordedAtUtc=ended});
                db.OperationalEvents.Add(new OperationalEvent{Category="Riego",EventType="IRRIGATION_COMPLETED",IrrigationZoneId=zone.Id,IrrigationRunId=run.Id,UserId=user?.Id,UserEmail=user?.Email,Detail=$"{run.Mode}: {duration*flow:0} L consumidos en {duration} minutos.",OccurredAtUtc=ended});
            }
            db.OperationalEvents.Add(new OperationalEvent{Category="Sistema",EventType="MODULES_7_TO_10_READY",Detail="Automatización, abastecimiento, riego manual y consumo inicializados.",OccurredAtUtc=DateTime.UtcNow});
        }
        await db.SaveChangesAsync();
    }
}
