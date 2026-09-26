using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaRiego.Api.Data;

namespace SistemaRiego.Api.Services;

public sealed record ReportFilter(DateTime FromUtc, DateTime ToUtc, Guid? ZoneId = null, string? Crop = null, string? EquipmentType = null, string? EquipmentId = null);
public sealed record ConsumptionReportRow(DateTime Date, string Zone, string Sector, string Crop, decimal VolumeLiters, decimal RecommendedLiters, decimal DeviationPercent, decimal Cost, string Source);
public sealed record IrrigationReportRow(long RunId, DateTime RequestedAt, string Zone, string Mode, string Status, int Minutes, string Command, string Ack, string User, string Reason);
public sealed record MaintenanceReportRow(DateTime Date, string Kind, string EquipmentType, string EquipmentId, string Title, string Status, string AssignedTo, string Notes);
public sealed record ReportData(IReadOnlyList<ConsumptionReportRow> Consumption, IReadOnlyList<IrrigationReportRow> Irrigation, IReadOnlyList<MaintenanceReportRow> Maintenance);

public sealed class Sprint6ReportService(AppDbContext db)
{
    private const string Green = "1F6B4F";

    public async Task<ReportData> LoadAsync(ReportFilter filter, CancellationToken ct)
    {
        var consumptionQuery = db.WaterConsumptionRecords.AsNoTracking().Where(x => x.RecordedAtUtc >= filter.FromUtc && x.RecordedAtUtc < filter.ToUtc);
        if (filter.ZoneId.HasValue) consumptionQuery = consumptionQuery.Where(x => x.IrrigationZoneId == filter.ZoneId);
        var consumption = await consumptionQuery.OrderBy(x => x.RecordedAtUtc).Select(x => new ConsumptionReportRow(
            x.RecordedAtUtc, x.IrrigationZone.Name, x.IrrigationZone.IrrigationSector.Name,
            db.CropCycles.Where(c => c.IrrigationZoneId == x.IrrigationZoneId && c.Status == "Activo").Select(c => c.Crop.Name).FirstOrDefault() ?? "Sin cultivo",
            x.VolumeLiters, x.RecommendedVolumeLiters ?? 0, x.DeviationPercent ?? 0, x.EstimatedCost, x.Source)).ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(filter.Crop)) consumption = consumption.Where(x => x.Crop.Equals(filter.Crop, StringComparison.OrdinalIgnoreCase)).ToList();

        var irrigationQuery = db.IrrigationRuns.AsNoTracking().Where(x => x.RequestedAtUtc >= filter.FromUtc && x.RequestedAtUtc < filter.ToUtc);
        if (filter.ZoneId.HasValue) irrigationQuery = irrigationQuery.Where(x => x.IrrigationZoneId == filter.ZoneId);
        var irrigation = await irrigationQuery.OrderBy(x => x.RequestedAtUtc).Select(x => new IrrigationReportRow(
            x.Id, x.RequestedAtUtc, x.IrrigationZone.Name, x.Mode, x.Status, x.PlannedDurationMinutes,
            db.IoTCommands.Where(c => c.IrrigationRunId == x.Id).OrderByDescending(c => c.RequestedAtUtc).Select(c => c.CommandType).FirstOrDefault() ?? "Sin comando",
            db.IoTCommands.Where(c => c.IrrigationRunId == x.Id).OrderByDescending(c => c.RequestedAtUtc).Select(c => c.Status).FirstOrDefault() ?? "Sin ACK",
            x.RequestedByEmail ?? "Proceso automático", x.Reason)).ToListAsync(ct);

        var plans = db.MaintenancePlans.AsNoTracking().Where(x => x.ScheduledAtUtc >= filter.FromUtc && x.ScheduledAtUtc < filter.ToUtc);
        var activities = db.MaintenanceActivities.AsNoTracking().Where(x => x.ScheduledAtUtc >= filter.FromUtc && x.ScheduledAtUtc < filter.ToUtc);
        var incidents = db.MaintenanceIncidents.AsNoTracking().Where(x => x.CreatedAtUtc >= filter.FromUtc && x.CreatedAtUtc < filter.ToUtc);
        if (!string.IsNullOrWhiteSpace(filter.EquipmentType)) { plans = plans.Where(x => x.EquipmentType == filter.EquipmentType); activities = activities.Where(x => x.EquipmentType == filter.EquipmentType); incidents = incidents.Where(x => x.EquipmentType == filter.EquipmentType); }
        if (!string.IsNullOrWhiteSpace(filter.EquipmentId)) { plans = plans.Where(x => x.EquipmentId == filter.EquipmentId); activities = activities.Where(x => x.EquipmentId == filter.EquipmentId); incidents = incidents.Where(x => x.EquipmentId == filter.EquipmentId); }
        var maintenance = (await plans.Select(x => new MaintenanceReportRow(x.ScheduledAtUtc, "Plan", x.EquipmentType, x.EquipmentId, x.Name, x.Status, x.AssignedToEmail ?? "Sin asignar", x.Notes ?? "")).ToListAsync(ct))
            .Concat(await activities.Select(x => new MaintenanceReportRow(x.ScheduledAtUtc, "Actividad", x.EquipmentType, x.EquipmentId, x.Title, x.Status, x.AssignedToEmail ?? "Sin asignar", x.Notes ?? "")).ToListAsync(ct))
            .Concat(await incidents.Select(x => new MaintenanceReportRow(x.CreatedAtUtc, "Incidente", x.EquipmentType, x.EquipmentId, x.Title, x.Status, x.AssignedToEmail ?? "Sin asignar", x.Notes ?? x.Description)).ToListAsync(ct))
            .OrderBy(x => x.Date).ToList();
        return new(consumption, irrigation, maintenance);
    }

    public byte[] ConsumptionPdf(ReportData data, ReportFilter filter) => BuildPdf("Consumo y eficiencia hídrica", filter, new[] { "Fecha", "Zona / cultivo", "Consumo", "Recomendado", "Desviación" }, data.Consumption.Select(x => new[] { x.Date.ToString("dd/MM/yyyy"), $"{x.Zone} · {x.Crop}", $"{x.VolumeLiters:N1} L", $"{x.RecommendedLiters:N1} L", $"{x.DeviationPercent:N1}%" }), $"Consumo total: {data.Consumption.Sum(x => x.VolumeLiters):N1} L · Costo estimado: Q {data.Consumption.Sum(x => x.Cost):N2}");
    public byte[] IrrigationPdf(ReportData data, ReportFilter filter) => BuildPdf("Ejecuciones de riego y confirmación MQTT", filter, new[] { "Fecha", "Zona / modo", "Estado", "Comando", "ACK" }, data.Irrigation.Select(x => new[] { x.RequestedAt.ToString("dd/MM HH:mm"), $"{x.Zone} · {x.Mode}", x.Status, x.Command, x.Ack }), $"Ejecuciones: {data.Irrigation.Count} · Manuales: {data.Irrigation.Count(x => x.Mode == "Manual")} · Automáticas: {data.Irrigation.Count(x => x.Mode != "Manual")}");
    public byte[] MaintenancePdf(ReportData data, ReportFilter filter) => BuildPdf("Mantenimiento e incidencias", filter, new[] { "Fecha", "Tipo", "Equipo", "Trabajo / incidencia", "Estado" }, data.Maintenance.Select(x => new[] { x.Date.ToString("dd/MM/yyyy"), x.Kind, $"{x.EquipmentType} · {x.EquipmentId}", x.Title, x.Status }), $"Registros: {data.Maintenance.Count} · Pendientes: {data.Maintenance.Count(x => !x.Status.Contains("Resuelt", StringComparison.OrdinalIgnoreCase) && !x.Status.Contains("Complet", StringComparison.OrdinalIgnoreCase))}");

    public byte[] Excel(ReportData data, ReportFilter filter)
    {
        using var workbook = new XLWorkbook();
        Sheet(workbook, "Consumo", new[] { "Fecha", "Zona", "Sector", "Cultivo", "Litros", "Recomendado", "Desviación %", "Costo Q", "Fuente" }, data.Consumption.Select(x => new object?[] { x.Date, x.Zone, x.Sector, x.Crop, x.VolumeLiters, x.RecommendedLiters, x.DeviationPercent, x.Cost, x.Source }));
        Sheet(workbook, "Riegos", new[] { "Id", "Fecha", "Zona", "Modo", "Estado", "Minutos", "Comando", "ACK", "Usuario", "Motivo" }, data.Irrigation.Select(x => new object?[] { x.RunId, x.RequestedAt, x.Zone, x.Mode, x.Status, x.Minutes, x.Command, x.Ack, x.User, x.Reason }));
        Sheet(workbook, "Mantenimiento", new[] { "Fecha", "Tipo", "Equipo", "Id equipo", "Título", "Estado", "Asignado", "Notas" }, data.Maintenance.Select(x => new object?[] { x.Date, x.Kind, x.EquipmentType, x.EquipmentId, x.Title, x.Status, x.AssignedTo, x.Notes }));
        var totals = workbook.Worksheets.Add("Totales");
        totals.Cell("A1").Value = "Resumen ejecutivo"; totals.Range("A1:B1").Merge().Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml("#" + Green));
        var values = new (string Label, object Value)[] { ("Período", $"{filter.FromUtc:dd/MM/yyyy} – {filter.ToUtc.AddTicks(-1):dd/MM/yyyy}"), ("Consumo total (L)", data.Consumption.Sum(x => x.VolumeLiters)), ("Consumo recomendado (L)", data.Consumption.Sum(x => x.RecommendedLiters)), ("Costo estimado (Q)", data.Consumption.Sum(x => x.Cost)), ("Ejecuciones de riego", data.Irrigation.Count), ("Registros de mantenimiento", data.Maintenance.Count) };
        for (var i = 0; i < values.Length; i++) { totals.Cell(i + 3, 1).Value = values[i].Label; totals.Cell(i + 3, 2).Value = XLCellValue.FromObject(values[i].Value); }
        totals.Columns().AdjustToContents(); totals.SheetView.FreezeRows(1);
        using var stream = new MemoryStream(); workbook.SaveAs(stream); return stream.ToArray();
    }

    public static byte[] SimplePdf(string title, DateTime from, DateTime to, string[] headers, IEnumerable<string[]> rows, string summary) => BuildPdf(title, new ReportFilter(from, to), headers, rows, summary);

    private static byte[] BuildPdf(string title, ReportFilter filter, string[] headers, IEnumerable<string[]> rows, string summary)
    {
        var materialized = rows.ToList();
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape()); page.Margin(28); page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Arial));
            page.Header().Column(column => { column.Item().Text("SISTEMA DE RIEGO · CUDEP").FontColor(Green).SemiBold(); column.Item().Text(title).FontSize(20).Bold(); column.Item().Text($"Período: {filter.FromUtc:dd/MM/yyyy} – {filter.ToUtc.AddTicks(-1):dd/MM/yyyy} · Generado: {DateTime.UtcNow:u}").FontColor(Colors.Grey.Darken1); });
            page.Content().PaddingVertical(15).Column(column =>
            {
                column.Spacing(10); column.Item().Background(Colors.Green.Lighten5).Padding(10).Text(summary).SemiBold().FontColor(Green);
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols => { foreach (var _ in headers) cols.RelativeColumn(); });
                    table.Header(header => { foreach (var item in headers) header.Cell().Background(Green).Padding(6).Text(item).FontColor(Colors.White).SemiBold(); });
                    foreach (var row in materialized) foreach (var value in row) table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(value ?? "—");
                });
                if (materialized.Count == 0) column.Item().AlignCenter().Padding(20).Text("No hay datos para los filtros seleccionados.").Italic();
            });
            page.Footer().AlignCenter().Text(x => { x.Span("Documento operativo · Página "); x.CurrentPageNumber(); x.Span(" de "); x.TotalPages(); });
        })).GeneratePdf();
    }

    private static void Sheet(XLWorkbook workbook, string name, string[] headers, IEnumerable<object?[]> rows)
    {
        var sheet = workbook.Worksheets.Add(name); for (var c = 0; c < headers.Length; c++) sheet.Cell(1, c + 1).Value = headers[c];
        var rowNumber = 2; foreach (var row in rows) { for (var c = 0; c < row.Length; c++) sheet.Cell(rowNumber, c + 1).Value = XLCellValue.FromObject(row[c]); rowNumber++; }
        var header = sheet.Range(1, 1, 1, headers.Length); header.Style.Font.Bold = true; header.Style.Font.FontColor = XLColor.White; header.Style.Fill.BackgroundColor = XLColor.FromHtml("#" + Green);
        sheet.SheetView.FreezeRows(1); sheet.RangeUsed()?.SetAutoFilter(); sheet.Columns().AdjustToContents(8, 42); sheet.RangeUsed()?.Style.Border.BottomBorder = XLBorderStyleValues.Hair;
    }
}
