using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Controllers;

[ApiController,Route("api/manual-irrigation"),Authorize(Policy=Policies.Operator)]
public sealed class ManualIrrigationController(AppDbContext db, ITotpService? totp = null):ControllerBase
{
    [HttpGet("zones")]
    public async Task<ActionResult> Zones(CancellationToken ct)=>Ok(await db.IrrigationZones.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.Name).Select(x=>new{x.Id,x.Name,x.Code,x.AreaHectares,HasValve=x.ValveDeviceId!=null,IsRunning=db.IrrigationRuns.Any(r=>r.IrrigationZoneId==x.Id&&r.Status=="En curso")}).ToListAsync(ct));

    [HttpGet("runs")]
    public async Task<ActionResult> Runs(int take=100,CancellationToken ct=default)=>Ok(await db.IrrigationRuns.AsNoTracking().Include(x=>x.IrrigationZone).OrderByDescending(x=>x.RequestedAtUtc).Take(Math.Clamp(take,1,300)).Select(x=>new{x.Id,x.Mode,x.Status,x.IrrigationZoneId,Zone=x.IrrigationZone.Name,x.PlannedDurationMinutes,x.FlowRateLitersMinute,x.RequestedAtUtc,x.StartedAtUtc,x.EndedAtUtc,x.RequestedByEmail,x.Reason,x.Observations,EstimatedLiters=x.PlannedDurationMinutes*x.FlowRateLitersMinute}).ToListAsync(ct));

    [HttpPost("start")]
    public async Task<ActionResult> Start(ManualIrrigationRequest request,CancellationToken ct)
    {
        if (totp is not null) { var verification = await totp.VerifyCriticalOperationAsync(HttpContext, ct); if (!verification.Allowed) return StatusCode(StatusCodes.Status403Forbidden, new { message = verification.Error }); }
        if(request.DurationMinutes is <1 or >120||request.FlowRateLitersMinute is <=0 or >100)return BadRequest(new{message="La duración o el caudal están fuera del rango permitido."});
        var zone=await db.IrrigationZones.FindAsync([request.IrrigationZoneId],ct);if(zone is null||!zone.IsActive)return BadRequest(new{message="La zona no está disponible."});
        if(await db.IrrigationRuns.AnyAsync(x=>x.IrrigationZoneId==request.IrrigationZoneId&&x.Status=="En curso",ct))return Conflict(new{message="La zona ya tiene un riego en curso."});
        var tank=await db.WaterTanks.OrderBy(x=>x.Name).FirstOrDefaultAsync(ct);var required=request.DurationMinutes*request.FlowRateLitersMinute;if(tank is not null&&tank.CurrentLevelLiters-required<tank.CapacityLiters*tank.MinimumSafePercent/100)return Conflict(new{message="El tanque no tiene nivel seguro para este riego."});
        var now=DateTime.UtcNow;var userId=UserId();var email=User.FindFirstValue(ClaimTypes.Email)??User.Identity?.Name;
        var run=new IrrigationRun{IrrigationZoneId=request.IrrigationZoneId,Mode="Manual",Status="En curso",PlannedDurationMinutes=request.DurationMinutes,FlowRateLitersMinute=request.FlowRateLitersMinute,RequestedAtUtc=now,StartedAtUtc=now,RequestedByUserId=userId,RequestedByEmail=email,Reason=request.Reason,Observations=request.Observations};db.IrrigationRuns.Add(run);
        foreach(var rule in await db.IrrigationRules.Where(x=>x.IrrigationZoneId==request.IrrigationZoneId&&x.IsEnabled).ToListAsync(ct)){rule.SuspendedUntilUtc=now.AddMinutes(request.DurationMinutes+10);rule.LastDecision="Pausa manual";rule.LastReason=$"Intervención autorizada por {email}.";}
        db.OperationalEvents.Add(new OperationalEvent{Category="Riego manual",EventType="MANUAL_IRRIGATION_STARTED",IrrigationZoneId=zone.Id,UserId=userId,UserEmail=email,Detail=$"{zone.Name}: {request.Reason}; {request.DurationMinutes} min a {request.FlowRateLitersMinute:0.0} L/min."});await db.SaveChangesAsync(ct);return Ok(new{run.Id,message="Riego manual iniciado; automatización pausada temporalmente."});
    }

    [HttpPost("{id:long}/stop")]
    public async Task<ActionResult> Stop(long id,StopIrrigationRequest request,CancellationToken ct)
    { var run=await db.IrrigationRuns.Include(x=>x.IrrigationZone).SingleOrDefaultAsync(x=>x.Id==id,ct);if(run is null)return NotFound();if(run.Status!="En curso")return Conflict(new{message="El riego ya no está activo."});var now=DateTime.UtcNow;var actual=Math.Clamp((decimal)(now-(run.StartedAtUtc??now)).TotalMinutes,1,run.PlannedDurationMinutes);var volume=Math.Round(actual*run.FlowRateLitersMinute,2);run.Status="Detenido";run.EndedAtUtc=now;run.Observations=request.Observations??run.Observations;db.WaterConsumptionRecords.Add(new WaterConsumptionRecord{IrrigationRunId=run.Id,IrrigationZoneId=run.IrrigationZoneId,Source="Estimado",FlowRateLitersMinute=run.FlowRateLitersMinute,DurationMinutes=actual,VolumeLiters=volume,RecordedAtUtc=now});var tank=await db.WaterTanks.FirstOrDefaultAsync(ct);if(tank is not null){tank.CurrentLevelLiters=Math.Max(0,tank.CurrentLevelLiters-volume);tank.LastLevelReadingUtc=now;tank.Status=tank.CurrentLevelLiters/tank.CapacityLiters*100<tank.MinimumSafePercent?"Nivel bajo":"Disponible";}db.OperationalEvents.Add(new OperationalEvent{Category="Riego manual",EventType="IRRIGATION_STOPPED",IrrigationZoneId=run.IrrigationZoneId,IrrigationRunId=run.Id,UserId=UserId(),UserEmail=User.FindFirstValue(ClaimTypes.Email),Detail=$"{run.IrrigationZone.Name}: {volume:0.0} L estimados. {request.Observations}"});await db.SaveChangesAsync(ct);return Ok(new{message="Riego detenido y consumo registrado.",volumeLiters=volume}); }
    private Guid? UserId()=>Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:null;
}
