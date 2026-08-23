using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/automation"), Authorize(Policy=PermissionPolicies.AutomationRead)]
public sealed class AutomationController(AppDbContext db, ITotpService? totp = null, IAutomationEngine? engine = null) : ControllerBase
{
    [HttpGet("rules")]
    public async Task<ActionResult> Rules(CancellationToken ct) => Ok(await db.IrrigationRules.AsNoTracking().Include(x=>x.IrrigationZone).OrderBy(x=>x.Priority).Select(x=>new {
        x.Id,x.Name,x.IrrigationZoneId,Zone=x.IrrigationZone.Name,x.MinimumMoisturePercent,x.TargetMoisturePercent,x.HysteresisPercent,x.Priority,x.MaximumDurationMinutes,x.AllowedFrom,x.AllowedUntil,x.AllowedDays,x.IsEnabled,x.SuspendedUntilUtc,x.LastEvaluatedAtUtc,x.LastDecision,x.LastReason
    }).ToListAsync(ct));

    [HttpPost("rules"), Authorize(Policy=PermissionPolicies.AutomationManage)]
    public async Task<ActionResult> Create(IrrigationRuleRequest request,CancellationToken ct)
    {
        if (totp is not null) { var verification = await totp.VerifyCriticalOperationAsync(HttpContext, ct); if (!verification.Allowed) return StatusCode(StatusCodes.Status403Forbidden, new { message = verification.Error }); }
        if(request.MinimumMoisturePercent<0||request.TargetMoisturePercent>100||request.MinimumMoisturePercent>=request.TargetMoisturePercent||request.MaximumDurationMinutes is <1 or >240) return BadRequest(new{message="Los umbrales o la duración no son válidos."});
        if(!await db.IrrigationZones.AnyAsync(x=>x.Id==request.IrrigationZoneId&&x.IsActive,ct)) return BadRequest(new{message="La zona no existe o está inactiva."});
        var item=new IrrigationRule{IrrigationZoneId=request.IrrigationZoneId,CropWaterRequirementId=request.CropWaterRequirementId,Name=request.Name,MinimumMoisturePercent=request.MinimumMoisturePercent,TargetMoisturePercent=request.TargetMoisturePercent,HysteresisPercent=request.HysteresisPercent,Priority=request.Priority,MaximumDurationMinutes=request.MaximumDurationMinutes,AllowedFrom=request.AllowedFrom,AllowedUntil=request.AllowedUntil,AllowedDays=request.AllowedDays,IsEnabled=request.IsEnabled,RequiresSufficientEnergy=request.RequiresSufficientEnergy};
        db.IrrigationRules.Add(item); await db.SaveChangesAsync(ct); return CreatedAtAction(nameof(Rules),new{id=item.Id},new{item.Id});
    }

    [HttpPut("rules/{id:guid}"), Authorize(Policy=PermissionPolicies.AutomationManage)]
    public async Task<ActionResult> Update(Guid id, IrrigationRuleRequest request, CancellationToken ct)
    {
        if (totp is not null) { var verification = await totp.VerifyCriticalOperationAsync(HttpContext, ct); if (!verification.Allowed) return StatusCode(StatusCodes.Status403Forbidden, new { message = verification.Error }); }
        var item = await db.IrrigationRules.FindAsync([id], ct); if (item is null) return NotFound();
        if (request.MinimumMoisturePercent < 0 || request.TargetMoisturePercent > 100 || request.MinimumMoisturePercent >= request.TargetMoisturePercent || request.MaximumDurationMinutes is < 1 or > 240) return BadRequest(new { message = "Los umbrales o la duración no son válidos." });
        item.IrrigationZoneId = request.IrrigationZoneId; item.CropWaterRequirementId = request.CropWaterRequirementId; item.Name = request.Name.Trim(); item.MinimumMoisturePercent = request.MinimumMoisturePercent; item.TargetMoisturePercent = request.TargetMoisturePercent; item.HysteresisPercent = request.HysteresisPercent; item.Priority = request.Priority; item.MaximumDurationMinutes = request.MaximumDurationMinutes; item.AllowedFrom = request.AllowedFrom; item.AllowedUntil = request.AllowedUntil; item.AllowedDays = request.AllowedDays; item.IsEnabled = request.IsEnabled; item.RequiresSufficientEnergy = request.RequiresSufficientEnergy;
        await Log("AUTOMATION_RULE_UPDATED", $"Regla {item.Name}", ct); await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpPatch("rules/{id:guid}/toggle"), Authorize(Policy=PermissionPolicies.AutomationManage)]
    public async Task<ActionResult> Toggle(Guid id,RuleToggleRequest request,CancellationToken ct)
    { var item=await db.IrrigationRules.FindAsync([id],ct); if(item is null)return NotFound(); item.IsEnabled=request.IsEnabled; if(request.IsEnabled)item.SuspendedUntilUtc=null; item.LastDecision=request.IsEnabled?"Lista":"Desactivada"; item.LastReason=request.IsEnabled?"Reactivación manual autorizada.":item.LastReason; await Log("AUTOMATION_RULE_TOGGLED",$"Regla {item.Name}: {(request.IsEnabled?"activa":"inactiva")}",ct); await db.SaveChangesAsync(ct); return NoContent(); }

    [HttpPost("evaluate")]
    public async Task<ActionResult> Evaluate(CancellationToken ct)
    {
        if (engine is not null) return Ok(await engine.EvaluateAsync(ct));
        var now=DateTime.UtcNow; var time=TimeOnly.FromDateTime(now.ToLocalTime()); var day=((int)now.ToLocalTime().DayOfWeek+6)%7+1;
        var rules=await db.IrrigationRules.Include(x=>x.IrrigationZone).Where(x=>x.IsEnabled).OrderBy(x=>x.Priority).ToListAsync(ct);
        var results=new List<object>();
        foreach(var rule in rules)
        {
            var reading=await db.SensorReadings.Where(x=>x.IrrigationZoneId==rule.IrrigationZoneId&&x.IsValid).OrderByDescending(x=>x.CapturedAtUtc).FirstOrDefaultAsync(ct);
            var blocked=rule.SuspendedUntilUtc>now||!rule.AllowedDays.Split(',').Contains(day.ToString())||time<rule.AllowedFrom||time>rule.AllowedUntil;
            var active=await db.IrrigationRuns.AnyAsync(x=>x.IrrigationZoneId==rule.IrrigationZoneId&&x.Status=="En curso",ct);
            var irrigate=!blocked&&!active&&reading is not null&&reading.Value<rule.MinimumMoisturePercent;
            rule.LastEvaluatedAtUtc=now; rule.LastDecision=irrigate?"Regar":blocked?"Fuera de ventana":active?"Riego activo":"No regar";
            rule.LastReason=reading is null?"No hay una lectura válida.":$"Humedad {reading.Value:0.0}% frente al mínimo {rule.MinimumMoisturePercent:0.0}%.";
            if(irrigate)
            {
                db.IrrigationRuns.Add(new IrrigationRun{IrrigationZoneId=rule.IrrigationZoneId,IrrigationRuleId=rule.Id,Mode="Automático",Status="En curso",PlannedDurationMinutes=rule.MaximumDurationMinutes,FlowRateLitersMinute=12,RequestedAtUtc=now,StartedAtUtc=now,Reason=rule.LastReason});
                db.OperationalEvents.Add(new OperationalEvent{Category="Automatización",EventType="AUTOMATIC_IRRIGATION_STARTED",IrrigationZoneId=rule.IrrigationZoneId,Detail=$"{rule.Name}: {rule.LastReason}"});
            }
            results.Add(new{rule.Id,rule.Name,rule.LastDecision,rule.LastReason,Started=irrigate});
        }
        await db.SaveChangesAsync(ct); return Ok(new{evaluatedAtUtc=now,results});
    }

    [HttpGet("active")]
    public async Task<ActionResult> Active(CancellationToken ct)=>Ok(await db.IrrigationRuns.AsNoTracking().Include(x=>x.IrrigationZone).Where(x=>x.Status=="En curso"||x.Status=="Esperando ACK"||x.Status=="Cierre pendiente").OrderByDescending(x=>x.StartedAtUtc).Select(x=>new{x.Id,x.Mode,x.Status,Zone=x.IrrigationZone.Name,x.PlannedDurationMinutes,x.FlowRateLitersMinute,x.StartedAtUtc,x.Reason}).ToListAsync(ct));

    private async Task Log(string type,string detail,CancellationToken ct){db.AccessAudits.Add(new AccessAudit{UserId=UserId(),EventType=type,Detail=detail,OccurredAtUtc=DateTime.UtcNow});await Task.CompletedTask;}
    private Guid? UserId()=>Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:null;
}
