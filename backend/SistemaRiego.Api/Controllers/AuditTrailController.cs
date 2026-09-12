using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/audit-trail"), Authorize(Policy = PermissionPolicies.AuditRead)]
public sealed class AuditTrailController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> Get(DateTime? from, DateTime? to, string? user, string? action, string? entity, int take = 200, CancellationToken ct = default)
        => Ok(await Filter(from, to, user, action, entity).OrderByDescending(x => x.OccurredAtUtc).Take(Math.Clamp(take, 1, 1000)).ToListAsync(ct));

    /// <summary>Pagina de la bitacora con el total filtrado; la lista simple solo traia las 200 mas recientes.</summary>
    [HttpGet("paged")]
    public async Task<ActionResult> Paged(DateTime? from, DateTime? to, string? user, string? action, string? entity, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = Filter(from, to, user, action, entity);
        var total = await query.CountAsync(ct);
        var pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, pageCount);
        var items = await query.OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Ok(new AuditPage(items, total, page, pageSize, pageCount));
    }

    [HttpGet("filters")]
    public async Task<ActionResult> Filters(CancellationToken ct)
    {
        var users = await db.AuditEntries.AsNoTracking().Where(x => x.UserEmail != null).Select(x => x.UserEmail!).Distinct().OrderBy(x => x).ToListAsync(ct);
        var actions = await db.AuditEntries.AsNoTracking().Select(x => x.ActionType).Distinct().OrderBy(x => x).ToListAsync(ct);
        var entities = await db.AuditEntries.AsNoTracking().Select(x => x.EntityType).Distinct().OrderBy(x => x).ToListAsync(ct);
        return Ok(new { users, actions, entities });
    }

    [HttpGet("export.csv")]
    public async Task<IActionResult> Csv(DateTime? from, DateTime? to, string? user, string? action, string? entity, CancellationToken ct)
    {
        var rows = await Rows(from, to, user, action, entity, ct);
        var csv = new StringBuilder("Fecha,Usuario,Acción,Entidad,Id,Antes,Después,IP,CorrelationId,Origen\r\n");
        foreach (var x in rows) csv.AppendLine(string.Join(',', Quote(x.OccurredAtUtc.ToString("O", CultureInfo.InvariantCulture)), Quote(x.UserEmail), Quote(x.ActionType), Quote(x.EntityType), Quote(x.EntityId), Quote(x.BeforeJson), Quote(x.AfterJson), Quote(x.IpAddress), Quote(x.CorrelationId), Quote(x.Origin)));
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv", "auditoria-filtrada.csv");
    }

    [HttpGet("export.xlsx")]
    public async Task<IActionResult> Excel(DateTime? from, DateTime? to, string? user, string? action, string? entity, CancellationToken ct)
    {
        var rows = await Rows(from, to, user, action, entity, ct);
        using var workbook = new XLWorkbook(); var sheet = workbook.Worksheets.Add("Auditoría");
        var headers = new[] { "Fecha UTC", "Usuario", "Acción", "Entidad", "Id", "Antes", "Después", "IP", "CorrelationId", "Origen" };
        for (var c = 0; c < headers.Length; c++) sheet.Cell(1, c + 1).Value = headers[c];
        for (var r = 0; r < rows.Count; r++)
        {
            var x = rows[r]; object?[] values = [x.OccurredAtUtc, x.UserEmail, x.ActionType, x.EntityType, x.EntityId, x.BeforeJson, x.AfterJson, x.IpAddress, x.CorrelationId, x.Origin];
            for (var c = 0; c < values.Length; c++) sheet.Cell(r + 2, c + 1).Value = XLCellValue.FromObject(values[c]);
        }
        var header = sheet.Range(1, 1, 1, headers.Length); header.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F6B4F"); header.Style.Font.FontColor = XLColor.White; header.Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1); sheet.RangeUsed()?.SetAutoFilter(); sheet.Columns().AdjustToContents(8, 45);
        using var stream = new MemoryStream(); workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "auditoria-filtrada.xlsx");
    }

    private Task<List<AuditEntry>> Rows(DateTime? from, DateTime? to, string? user, string? action, string? entity, CancellationToken ct)
        => Filter(from, to, user, action, entity).OrderByDescending(x => x.OccurredAtUtc).Take(10000).ToListAsync(ct);

    private IQueryable<AuditEntry> Filter(DateTime? from, DateTime? to, string? user, string? action, string? entity)
    {
        var query = db.AuditEntries.AsNoTracking().AsQueryable();
        if (from.HasValue) query = query.Where(x => x.OccurredAtUtc >= from.Value.Date);
        if (to.HasValue) query = query.Where(x => x.OccurredAtUtc < to.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(user)) query = query.Where(x => x.UserEmail == user);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(x => x.ActionType == action);
        if (!string.IsNullOrWhiteSpace(entity)) query = query.Where(x => x.EntityType == entity);
        return query;
    }

    private static string Quote(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
}

public sealed record AuditPage(List<AuditEntry> Items, int Total, int Page, int PageSize, int PageCount);
