using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/reports"), Authorize(Policy = PermissionPolicies.ReportsRead)]
public sealed class ReportsController(Sprint6ReportService reports, AppDbContext db) : ControllerBase
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

    [HttpGet("readings.pdf")]
    public async Task<IActionResult> Readings(DateTime? from, DateTime? to, CancellationToken ct)
    { var f=Normalize(from,to); var rows=await db.SensorReadings.AsNoTracking().Where(x=>x.CapturedAtUtc>=f.FromUtc&&x.CapturedAtUtc<f.ToUtc).OrderByDescending(x=>x.CapturedAtUtc).Take(1000).Select(x=>new[]{x.CapturedAtUtc.ToString("dd/MM HH:mm"),x.Sensor.Name,x.IrrigationZone!=null?x.IrrigationZone.Name:"Sin zona",x.Value.ToString("N2"),x.IsValid?"Válida":"Inválida"}).ToListAsync(ct); return File(Sprint6ReportService.SimplePdf("Lecturas de sensores",f.FromUtc,f.ToUtc,new[]{"Fecha","Sensor","Zona","Valor","Calidad"},rows,$"Lecturas incluidas: {rows.Count}"),"application/pdf","lecturas-sensores.pdf"); }

    [HttpGet("alerts.pdf")]
    public async Task<IActionResult> Alerts(DateTime? from, DateTime? to, CancellationToken ct)
    { var f=Normalize(from,to); var rows=await db.SystemAlerts.AsNoTracking().Where(x=>x.RaisedAtUtc>=f.FromUtc&&x.RaisedAtUtc<f.ToUtc).OrderByDescending(x=>x.RaisedAtUtc).Select(x=>new[]{x.RaisedAtUtc.ToString("dd/MM HH:mm"),x.Type,x.Severity,x.Status,x.Description}).ToListAsync(ct); return File(Sprint6ReportService.SimplePdf("Alertas del sistema",f.FromUtc,f.ToUtc,new[]{"Fecha","Tipo","Severidad","Estado","Descripción"},rows,$"Alertas: {rows.Count}"),"application/pdf","alertas.pdf"); }

    [HttpGet("iot.pdf")]
    public async Task<IActionResult> IoT(DateTime? from, DateTime? to, CancellationToken ct)
    { var f=Normalize(from,to); var rows=await db.IoTDevices.AsNoTracking().OrderBy(x=>x.Name).Select(x=>new[]{x.Code,x.Name,x.DeviceType.Name,x.OperationalStatus.Name,x.InventoryStatus}).ToListAsync(ct); return File(Sprint6ReportService.SimplePdf("Inventario y estado IoT",f.FromUtc,f.ToUtc,new[]{"Código","Dispositivo","Tipo","Estado operativo","Inventario"},rows,$"Dispositivos: {rows.Count}"),"application/pdf","inventario-iot.pdf"); }

    [HttpGet("energy.pdf")]
    public async Task<IActionResult> Energy(DateTime? from, DateTime? to, CancellationToken ct)
    { var f=Normalize(from,to); var rows=await db.EnergyReadings.AsNoTracking().Where(x=>x.CapturedAtUtc>=f.FromUtc&&x.CapturedAtUtc<f.ToUtc).OrderByDescending(x=>x.CapturedAtUtc).Take(1000).Select(x=>new[]{x.CapturedAtUtc.ToString("dd/MM HH:mm"),x.GenerationWatts.ToString("N0")+" W",x.ConsumptionWatts.ToString("N0")+" W",x.BatteryPercent.ToString("N1")+"%",x.BatteryVoltage.ToString("N1")+" V"}).ToListAsync(ct); return File(Sprint6ReportService.SimplePdf("Generación y autonomía energética",f.FromUtc,f.ToUtc,new[]{"Fecha","Generación","Consumo","Batería","Voltaje"},rows,$"Lecturas energéticas: {rows.Count}"),"application/pdf","energia.pdf"); }
    [HttpGet("analytics.xlsx")]
    public async Task<IActionResult> Excel(DateTime? from, DateTime? to, Guid? zoneId, string? crop, string? equipmentType, string? equipmentId, CancellationToken ct)
    { var filter = Normalize(from, to, zoneId, crop) with { EquipmentType = equipmentType, EquipmentId = equipmentId }; var data = await reports.LoadAsync(filter, ct); return File(reports.Excel(data, filter), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "analitica-riego.xlsx"); }

    private static ReportFilter Normalize(DateTime? from, DateTime? to, Guid? zoneId = null, string? crop = null)
    { var end = (to ?? DateTime.UtcNow).Date.AddDays(1); var start = (from ?? end.AddDays(-30)).Date; if (start >= end) start = end.AddDays(-30); return new(start, end, zoneId, crop); }
}
