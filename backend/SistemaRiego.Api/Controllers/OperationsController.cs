using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Controllers;

[ApiController,Route("api/operations"),Authorize(Policy=PermissionPolicies.OperationsRead)]
public sealed class OperationsController(AppDbContext db):ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult> Summary(int days=30,CancellationToken ct=default)
    { var from=DateTime.UtcNow.Date.AddDays(-Math.Clamp(days,1,365)+1);var records=db.WaterConsumptionRecords.AsNoTracking().Where(x=>x.RecordedAtUtc>=from);var total=await records.SumAsync(x=>(decimal?)x.VolumeLiters,ct)??0;var daily=await records.GroupBy(x=>x.RecordedAtUtc.Date).Select(g=>new{Date=g.Key,VolumeLiters=g.Sum(x=>x.VolumeLiters),Events=g.Count()}).OrderBy(x=>x.Date).ToListAsync(ct);var byZone=await records.GroupBy(x=>x.IrrigationZone.Name).Select(g=>new{Zone=g.Key,VolumeLiters=g.Sum(x=>x.VolumeLiters),Events=g.Count()}).OrderByDescending(x=>x.VolumeLiters).ToListAsync(ct);return Ok(new{From=from,To=DateTime.UtcNow,totalLiters=Math.Round(total,1),eventCount=await records.CountAsync(ct),averageLiters=await records.AnyAsync(ct)?Math.Round(total/await records.CountAsync(ct),1):0,daily,byZone}); }

    [HttpGet("history")]
    public async Task<ActionResult> History(DateTime? from,DateTime? to,string? type,Guid? zoneId,string? search,int take=200,CancellationToken ct=default)
    { var q=db.OperationalEvents.AsNoTracking().Include(x=>x.IrrigationZone).AsQueryable();if(from.HasValue)q=q.Where(x=>x.OccurredAtUtc>=from.Value);if(to.HasValue)q=q.Where(x=>x.OccurredAtUtc<to.Value.Date.AddDays(1));if(!string.IsNullOrWhiteSpace(type))q=q.Where(x=>x.Category==type||x.EventType==type);if(zoneId.HasValue)q=q.Where(x=>x.IrrigationZoneId==zoneId);if(!string.IsNullOrWhiteSpace(search))q=q.Where(x=>x.Detail.Contains(search));return Ok(await q.OrderByDescending(x=>x.OccurredAtUtc).Take(Math.Clamp(take,1,1000)).Select(x=>new{x.Id,x.Category,x.EventType,x.Severity,x.OccurredAtUtc,x.UserEmail,Zone=x.IrrigationZone==null?null:x.IrrigationZone.Name,x.Detail}).ToListAsync(ct)); }

    [HttpGet("export.csv")]
    public async Task<IActionResult> Export(DateTime? from,DateTime? to,CancellationToken ct)
    { var q=db.WaterConsumptionRecords.AsNoTracking().Include(x=>x.IrrigationZone).Include(x=>x.IrrigationRun).AsQueryable();if(from.HasValue)q=q.Where(x=>x.RecordedAtUtc>=from.Value);if(to.HasValue)q=q.Where(x=>x.RecordedAtUtc<to.Value.Date.AddDays(1));var rows=await q.OrderByDescending(x=>x.RecordedAtUtc).Take(5000).ToListAsync(ct);var csv=new StringBuilder("Fecha,Zona,Modo,Caudal L/min,Duración min,Volumen L,Fuente\r\n");foreach(var x in rows)csv.AppendLine(string.Join(',',Q(x.RecordedAtUtc.ToString("O",CultureInfo.InvariantCulture)),Q(x.IrrigationZone.Name),Q(x.IrrigationRun.Mode),x.FlowRateLitersMinute.ToString(CultureInfo.InvariantCulture),x.DurationMinutes.ToString(CultureInfo.InvariantCulture),x.VolumeLiters.ToString(CultureInfo.InvariantCulture),Q(x.Source)));return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),"text/csv","consumo-riego.csv"); }
    private static string Q(string? value)=>$"\"{(value??string.Empty).Replace("\"","\"\"")}\"";
}
