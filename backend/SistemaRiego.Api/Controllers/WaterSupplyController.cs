using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Controllers;

[ApiController,Route("api/water-supply"),Authorize(Policy=Policies.Operator)]
public sealed class WaterSupplyController(AppDbContext db, IPumpCommandService? pumpCommands = null, IWaterCapacityService? capacityService = null):ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult> Status(CancellationToken ct)
    { var tanks=await db.WaterTanks.AsNoTracking().Include(x=>x.Pumps).ToListAsync(ct);var capacityValveLimit=capacityService is null?(int?)null:await capacityService.GetMaximumValveCountAsync(ct); return Ok(tanks.Select(x=>new{x.Id,x.Name,x.CapacityLiters,x.CurrentLevelLiters,LevelPercent=x.CapacityLiters==0?0:Math.Round(x.CurrentLevelLiters/x.CapacityLiters*100,1),x.MinimumSafePercent,x.MaximumFillPercent,x.Status,x.LastLevelReadingUtc,CapacityValveLimit=capacityValveLimit,Pumps=x.Pumps.Select(p=>new{p.Id,p.Name,p.Status,p.IsRunning,p.MaximumRunMinutes,p.MinimumRestMinutes,p.StartedAtUtc,p.LastStoppedAtUtc,p.LockedUntilUtc,p.FailureReason,p.HasUnacknowledgedFault,p.LastPressureBar,p.LastMotorCurrentAmps,p.LastTelemetryAtUtc,p.RatedFlowLitersMinute,p.MinimumPressureBar})})); }

    [HttpPost("tanks/{id:guid}/level"),Authorize(Policy=Policies.Technician)]
    public async Task<ActionResult> Level(Guid id,TankLevelRequest request,CancellationToken ct)
    { var tank=await db.WaterTanks.FindAsync([id],ct); if(tank is null)return NotFound(); if(request.LevelLiters<0||request.LevelLiters>tank.CapacityLiters)return BadRequest(new{message="El nivel debe estar dentro de la capacidad del tanque."}); tank.CurrentLevelLiters=request.LevelLiters;tank.LastLevelReadingUtc=DateTime.UtcNow;tank.Status=Percent(tank)<tank.MinimumSafePercent?"Nivel bajo":"Disponible";db.OperationalEvents.Add(Event("Tanque","TANK_LEVEL_UPDATED",request.Detail??$"Nivel actualizado a {request.LevelLiters:0} L"));await db.SaveChangesAsync(ct);return NoContent(); }

    [HttpPost("pumps/{id:guid}/start")]
    public async Task<ActionResult> Start(Guid id,PumpCommandRequest request,CancellationToken ct)
    { var pump=await db.WaterPumps.Include(x=>x.WaterTank).SingleOrDefaultAsync(x=>x.Id==id,ct);if(pump is null)return NotFound();var now=DateTime.UtcNow;var safety=await db.SystemSafetyStates.FindAsync([1],ct);if(safety?.EmergencyStopActive==true)return Conflict(new{message="El paro global de emergencia está activo."});if(Percent(pump.WaterTank)<=pump.WaterTank.MinimumSafePercent)return Conflict(new{message="Arranque bloqueado: nivel bajo y riesgo de trabajo en seco."});if(pump.HasUnacknowledgedFault)return Conflict(new{message="La bomba tiene una falla pendiente de reconocimiento."});if(pump.IsRunning)return Conflict(new{message="La bomba ya está encendida."});if(pump.LockedUntilUtc>now||pump.Status=="Falla")return Conflict(new{message="La bomba está bloqueada por seguridad."});if(Percent(pump.WaterTank)>=pump.WaterTank.MaximumFillPercent)return Conflict(new{message="El tanque ya alcanzó el nivel máximo seguro."});if(pump.LastStoppedAtUtc.HasValue&&pump.LastStoppedAtUtc.Value.AddMinutes(pump.MinimumRestMinutes)>now)return Conflict(new{message="La bomba aún está en su intervalo mínimo de descanso."});if(pumpCommands is not null){pump.Status="Esperando ACK";db.WaterSupplyEvents.Add(new WaterSupplyEvent{WaterPumpId=id,Status="En curso",InitialLevelLiters=pump.WaterTank.CurrentLevelLiters,RequestedByUserId=UserId(),Detail=request.Reason});await db.SaveChangesAsync(ct);try{await pumpCommands.SendAsync(pump,"ENCENDER_BOMBA",UserId(),ct);}catch(Exception ex){pump.Status="Falla";pump.FailureReason=ex.Message;await db.SaveChangesAsync(ct);return StatusCode(503,new{message=ex.Message});}return Ok(new{message="Orden de encendido publicada; esperando ACK MQTT."});}pump.IsRunning=true;pump.Status="Encendida";pump.StartedAtUtc=now;db.WaterSupplyEvents.Add(new WaterSupplyEvent{WaterPumpId=id,Status="En curso",InitialLevelLiters=pump.WaterTank.CurrentLevelLiters,RequestedByUserId=UserId(),Detail=request.Reason});db.OperationalEvents.Add(Event("Abastecimiento","PUMP_STARTED",request.Reason));await db.SaveChangesAsync(ct);return Ok(new{message="Bomba encendida con protecciones activas."}); }

    [HttpPost("pumps/{id:guid}/stop")]
    public async Task<ActionResult> Stop(Guid id,PumpCommandRequest request,CancellationToken ct)
    { var pump=await db.WaterPumps.Include(x=>x.WaterTank).SingleOrDefaultAsync(x=>x.Id==id,ct);if(pump is null)return NotFound();if(!pump.IsRunning)return Conflict(new{message="La bomba ya está detenida."});var now=DateTime.UtcNow;if(pumpCommands is not null){pump.Status="Esperando ACK de parada";await db.SaveChangesAsync(ct);try{await pumpCommands.SendAsync(pump,"APAGAR_BOMBA",UserId(),ct);}catch(Exception ex){pump.Status="Falla";pump.FailureReason=ex.Message;await db.SaveChangesAsync(ct);return StatusCode(503,new{message=ex.Message});}return Ok(new{message="Orden de parada publicada; esperando ACK MQTT.",suppliedLiters=(decimal?)null});}var evt=await db.WaterSupplyEvents.Where(x=>x.WaterPumpId==id&&x.Status=="En curso").OrderByDescending(x=>x.StartedAtUtc).FirstOrDefaultAsync(ct);var minutes=Math.Max(1,(now-(pump.StartedAtUtc??now)).TotalMinutes);var supplied=Math.Min(pump.WaterTank.CapacityLiters-pump.WaterTank.CurrentLevelLiters,(decimal)minutes*25);pump.WaterTank.CurrentLevelLiters+=supplied;pump.WaterTank.LastLevelReadingUtc=now;pump.IsRunning=false;pump.Status="Detenida";pump.LastStoppedAtUtc=now;pump.StartedAtUtc=null;if(evt is not null){evt.Status="Completado";evt.EndedAtUtc=now;evt.FinalLevelLiters=pump.WaterTank.CurrentLevelLiters;evt.SuppliedLiters=supplied;evt.Detail=request.Reason;}db.OperationalEvents.Add(Event("Abastecimiento","PUMP_STOPPED",$"{request.Reason}. Se estimaron {supplied:0.0} L."));await db.SaveChangesAsync(ct);return Ok(new{message="Bomba detenida.",suppliedLiters=Math.Round(supplied,1)}); }

    [HttpPost("pumps/{id:guid}/acknowledge-fault"),Authorize(Policy=Policies.Technician)]
    public async Task<ActionResult> AcknowledgeFault(Guid id,CancellationToken ct){var pump=await db.WaterPumps.FindAsync([id],ct);if(pump is null)return NotFound();pump.HasUnacknowledgedFault=false;pump.FailureReason=null;pump.Status="Detenida";await db.SaveChangesAsync(ct);return NoContent();}

    [HttpPost("emergency/clear"),Authorize(Policy=Policies.Technician)]
    public async Task<ActionResult> ClearEmergency(CancellationToken ct){var safety=await db.SystemSafetyStates.FindAsync([1],ct)??new SystemSafetyState();safety.EmergencyStopActive=false;safety.ClearedAtUtc=DateTime.UtcNow;safety.Detail="Paro global restablecido por técnico.";db.Update(safety);await db.SaveChangesAsync(ct);return NoContent();}

    [HttpGet("history")]
    public async Task<ActionResult> History(int take=50,CancellationToken ct=default)=>Ok(await db.WaterSupplyEvents.AsNoTracking().Include(x=>x.WaterPump).OrderByDescending(x=>x.StartedAtUtc).Take(Math.Clamp(take,1,200)).Select(x=>new{x.Id,Pump=x.WaterPump.Name,x.EventType,x.Status,x.StartedAtUtc,x.EndedAtUtc,x.InitialLevelLiters,x.FinalLevelLiters,x.SuppliedLiters,x.Detail}).ToListAsync(ct));
    private static decimal Percent(WaterTank x)=>x.CapacityLiters==0?0:x.CurrentLevelLiters/x.CapacityLiters*100;
    private OperationalEvent Event(string category,string type,string detail)=>new(){Category=category,EventType=type,UserId=UserId(),UserEmail=User.FindFirstValue(ClaimTypes.Email),Detail=detail};
    private Guid? UserId()=>Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:null;
}
