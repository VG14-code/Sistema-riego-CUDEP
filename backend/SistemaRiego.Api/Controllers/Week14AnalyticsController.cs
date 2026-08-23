using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/week14"), Authorize(Policy = PermissionPolicies.AnalyticsRead)]
public sealed class Week14AnalyticsController(AppDbContext db) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult> Dashboard(int days = 30, CancellationToken ct = default)
    {
        var from = DateTime.UtcNow.Date.AddDays(-Math.Clamp(days, 1, 365) + 1);
        var rows = await db.WaterConsumptionRecords.AsNoTracking().Where(x => x.RecordedAtUtc >= from)
            .Select(x => new { x.RecordedAtUtc, x.VolumeLiters, x.Source, x.IsMeasured, x.RecommendedVolumeLiters, x.DeviationPercent, x.EstimatedCost, Zone = x.IrrigationZone.Name, Sector = x.IrrigationZone.IrrigationSector.Name, Crop = db.CropCycles.Where(c => c.IrrigationZoneId == x.IrrigationZoneId && c.Status == "Activo").Select(c => c.Crop.Name).FirstOrDefault() ?? "Sin cultivo" }).ToListAsync(ct);
        var total = rows.Sum(x => x.VolumeLiters);
        return Ok(new
        {
            from, to = DateTime.UtcNow, totalLiters = Math.Round(total, 1), eventCount = rows.Count,
            averageLiters = rows.Count == 0 ? 0 : Math.Round(total / rows.Count, 1),
            daily = rows.GroupBy(x => x.RecordedAtUtc.Date).Select(g => new { date = g.Key, volumeLiters = g.Sum(x => x.VolumeLiters), events = g.Count() }).OrderBy(x => x.date),
            weekly = rows.GroupBy(x => Monday(x.RecordedAtUtc.Date)).Select(g => new { weekStart = g.Key, volumeLiters = g.Sum(x => x.VolumeLiters), events = g.Count() }).OrderBy(x => x.weekStart),
            bySector = rows.GroupBy(x => x.Sector).Select(g => new { sector = g.Key, volumeLiters = g.Sum(x => x.VolumeLiters), events = g.Count() }).OrderByDescending(x => x.volumeLiters),
            byZone = rows.GroupBy(x => x.Zone).Select(g => new { zone = g.Key, volumeLiters = g.Sum(x => x.VolumeLiters), events = g.Count(), measuredEvents = g.Count(x => x.IsMeasured), estimatedEvents = g.Count(x => !x.IsMeasured), cost = g.Sum(x => x.EstimatedCost) }).OrderByDescending(x => x.volumeLiters),
            byCrop = rows.GroupBy(x => x.Crop).Select(g => new { crop = g.Key, volumeLiters = g.Sum(x => x.VolumeLiters), recommendedLiters = g.Sum(x => x.RecommendedVolumeLiters ?? 0), averageDeviationPercent = g.Where(x => x.DeviationPercent.HasValue).Select(x => x.DeviationPercent).Average(), cost = g.Sum(x => x.EstimatedCost) }).OrderByDescending(x => x.volumeLiters),
            bySource = rows.GroupBy(x => x.Source).Select(g => new { source = g.Key, volumeLiters = g.Sum(x => x.VolumeLiters), events = g.Count() }),
            totalEstimatedCost = Math.Round(rows.Sum(x => x.EstimatedCost), 2)
        });
    }

    [HttpGet("filters")]
    public async Task<ActionResult> Filters(CancellationToken ct)
    {
        var categories = await db.OperationalEvents.AsNoTracking().Select(x => x.Category).Distinct().OrderBy(x => x).ToListAsync(ct);
        var users = await db.OperationalEvents.AsNoTracking().Where(x => x.UserEmail != null).Select(x => x.UserEmail!).Distinct().OrderBy(x => x).ToListAsync(ct);
        var zones = await db.IrrigationZones.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, x.Name }).ToListAsync(ct);
        return Ok(new { categories, users, zones });
    }

    [HttpGet("history")]
    public async Task<ActionResult> History(DateTime? from, DateTime? to, string? type, Guid? zoneId, string? user, string? search, int take = 300, CancellationToken ct = default)
        => Ok(await Project(Filter(from, to, type, zoneId, user, search)).Take(Math.Clamp(take, 1, 1000)).ToListAsync(ct));

    [HttpGet("history/{id:long}")]
    public async Task<ActionResult> Detail(long id, CancellationToken ct)
    {
        var item = await Project(db.OperationalEvents.AsNoTracking().Include(x => x.IrrigationZone).Where(x => x.Id == id)).SingleOrDefaultAsync(ct);
        return item is null ? NotFound(new { message = "El evento solicitado no existe." }) : Ok(item);
    }

    [HttpGet("history.csv")]
    public async Task<IActionResult> Csv(DateTime? from, DateTime? to, string? type, Guid? zoneId, string? user, string? search, CancellationToken ct)
    {
        var rows = await Project(Filter(from, to, type, zoneId, user, search)).Take(5000).ToListAsync(ct);
        var csv = new StringBuilder("Fecha,Categoría,Tipo,Severidad,Zona,Usuario,Detalle\r\n");
        foreach (var x in rows) csv.AppendLine(string.Join(',', Quote(x.OccurredAtUtc.ToString("O", CultureInfo.InvariantCulture)), Quote(x.Category), Quote(x.EventType), Quote(x.Severity), Quote(x.Zone), Quote(x.UserEmail), Quote(x.Detail)));
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv", "historial-operativo-semana14.csv");
    }

    [HttpGet("history.json")]
    public async Task<ActionResult> Json(DateTime? from, DateTime? to, string? type, Guid? zoneId, string? user, string? search, CancellationToken ct)
        => Ok(await Project(Filter(from, to, type, zoneId, user, search)).Take(5000).ToListAsync(ct));

    [HttpGet("powerbi/model"), Authorize(Policy = PermissionPolicies.AnalyticsPowerBi)]
    public ActionResult PowerBiModel() => Ok(new
    {
        database = "SistemaRiego", server = @"(localdb)\MSSQLLocalDB",
        views = new[] { "vw_PowerBI_Consumption", "vw_PowerBI_Telemetry", "vw_PowerBI_OperationalEvents" },
        procedure = "sp_PowerBI_ConsumptionSummary",
        relationships = new[] { "Zona -> Sector -> Lote -> Finca", "Riego -> Consumo", "Sensor -> Telemetría -> Zona" },
        measures = new[] { "Litros Totales = SUM(vw_PowerBI_Consumption[VolumeLiters])", "Promedio por Riego = AVERAGE(vw_PowerBI_Consumption[VolumeLiters])", "Eventos = COUNTROWS(vw_PowerBI_Consumption)" }
    });

    private IQueryable<OperationalEvent> Filter(DateTime? from, DateTime? to, string? type, Guid? zoneId, string? user, string? search)
    {
        var q = db.OperationalEvents.AsNoTracking().Include(x => x.IrrigationZone).AsQueryable();
        if (from.HasValue) q = q.Where(x => x.OccurredAtUtc >= from.Value);
        if (to.HasValue) q = q.Where(x => x.OccurredAtUtc < to.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(type)) q = q.Where(x => x.Category == type || x.EventType == type);
        if (zoneId.HasValue) q = q.Where(x => x.IrrigationZoneId == zoneId);
        if (!string.IsNullOrWhiteSpace(user)) q = q.Where(x => x.UserEmail == user);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(x => x.Detail.Contains(search));
        return q;
    }

    private static IQueryable<EventRow> Project(IQueryable<OperationalEvent> q) => q.OrderByDescending(x => x.OccurredAtUtc)
        .Select(x => new EventRow(x.Id, x.Category, x.EventType, x.Severity, x.OccurredAtUtc, x.UserEmail, x.IrrigationZone == null ? null : x.IrrigationZone.Name, x.Detail));
    private static DateTime Monday(DateTime date) { var offset = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7; return date.AddDays(-offset); }
    private static string Quote(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
    private sealed record EventRow(long Id, string Category, string EventType, string Severity, DateTime OccurredAtUtc, string? UserEmail, string? Zone, string Detail);
}
