using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/reports"), Authorize(Policy = Policies.Operator)]
public sealed class ReportsController(Sprint6ReportService reports) : ControllerBase
{
    [HttpGet("consumption.pdf")]
    public async Task<IActionResult> Consumption(DateTime? from, DateTime? to, Guid? zoneId, string? crop, CancellationToken ct)
    { var filter = Normalize(from, to, zoneId, crop); var data = await reports.LoadAsync(filter, ct); return File(reports.ConsumptionPdf(data, filter), "application/pdf", "consumo-eficiencia.pdf"); }

    [HttpGet("irrigation.pdf")]
    public async Task<IActionResult> Irrigation(DateTime? from, DateTime? to, Guid? zoneId, CancellationToken ct)
    { var filter = Normalize(from, to, zoneId); var data = await reports.LoadAsync(filter, ct); return File(reports.IrrigationPdf(data, filter), "application/pdf", "ejecuciones-riego.pdf"); }

    [HttpGet("maintenance.pdf")]
    public async Task<IActionResult> Maintenance(DateTime? from, DateTime? to, string? equipmentType, string? equipmentId, CancellationToken ct)
    { var filter = Normalize(from, to) with { EquipmentType = equipmentType, EquipmentId = equipmentId }; var data = await reports.LoadAsync(filter, ct); return File(reports.MaintenancePdf(data, filter), "application/pdf", "mantenimiento-incidencias.pdf"); }

    [HttpGet("analytics.xlsx")]
    public async Task<IActionResult> Excel(DateTime? from, DateTime? to, Guid? zoneId, string? crop, string? equipmentType, string? equipmentId, CancellationToken ct)
    { var filter = Normalize(from, to, zoneId, crop) with { EquipmentType = equipmentType, EquipmentId = equipmentId }; var data = await reports.LoadAsync(filter, ct); return File(reports.Excel(data, filter), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "analitica-riego.xlsx"); }

    private static ReportFilter Normalize(DateTime? from, DateTime? to, Guid? zoneId = null, string? crop = null)
    { var end = (to ?? DateTime.UtcNow).Date.AddDays(1); var start = (from ?? end.AddDays(-30)).Date; if (start >= end) start = end.AddDays(-30); return new(start, end, zoneId, crop); }
}
