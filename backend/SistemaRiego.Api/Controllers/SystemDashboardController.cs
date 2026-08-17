using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/system/dashboard"), Authorize(Policy = Policies.Operator)]
public sealed class SystemDashboardController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SystemDashboardResponse>> Get(CancellationToken ct)
    {
        var moistureType = await db.MasterCatalogItems.Where(x => x.Kind == CatalogKind.SensorType && x.Code == "SOIL_MOISTURE").Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        var latest = await db.SensorReadings.AsNoTracking().OrderByDescending(x => x.CapturedAtUtc).Take(12).Select(x => new ReadingResponse(x.Id, x.SensorId, x.Sensor.Name, x.IrrigationZone != null ? x.IrrigationZone.Name : null, x.CapturedAtUtc, x.Value, x.Sensor.MeasurementUnit.Symbol, x.BatteryPercent, x.SignalStrength, x.IsValid, x.ValidationStatus, x.Transport)).ToListAsync(ct);
        var since = DateTime.UtcNow.AddHours(-24); var moisture = moistureType.HasValue ? await db.SensorReadings.Where(x => x.Sensor.SensorTypeId == moistureType && x.IsValid && x.CapturedAtUtc >= since).Select(x => (decimal?)x.Value).AverageAsync(ct) : null;
        var audits = await db.AccessAudits.AsNoTracking().OrderByDescending(x => x.OccurredAtUtc).Take(8).Select(x => new ActivityItemResponse("AUDITORIA:" + x.EventType, x.Detail ?? "Operación registrada", x.OccurredAtUtc)).ToListAsync(ct);
        var telemetryActivity = await db.SensorReadings.AsNoTracking().OrderByDescending(x => x.ReceivedAtUtc).Take(8).Select(x => new ActivityItemResponse("TELEMETRIA", x.Sensor.Name + " · " + x.Value + " " + x.Sensor.MeasurementUnit.Symbol, x.ReceivedAtUtc)).ToListAsync(ct);
        var irrigationActivity = await db.OperationalEvents.AsNoTracking().Where(x => x.Category == "Riego" || x.EventType.Contains("IRRIGATION")).OrderByDescending(x => x.OccurredAtUtc).Take(8).Select(x => new ActivityItemResponse("RIEGO:" + x.EventType, x.Detail, x.OccurredAtUtc)).ToListAsync(ct);
        var commandActivity = await db.IoTCommands.AsNoTracking().Where(x => x.ConfirmedAtUtc.HasValue).OrderByDescending(x => x.ConfirmedAtUtc).Take(8).Select(x => new ActivityItemResponse("COMANDO:" + x.CommandType, x.Device.Name + " · confirmado", x.ConfirmedAtUtc!.Value)).ToListAsync(ct);
        var activity = audits.Concat(telemetryActivity).Concat(irrigationActivity).Concat(commandActivity).OrderByDescending(x => x.OccurredAtUtc).Take(12).ToList();
        return Ok(new SystemDashboardResponse(await db.IoTSensors.CountAsync(x => x.IsActive, ct), await db.IrrigationZones.CountAsync(x => x.IsActive, ct), await db.IoTDevices.CountAsync(x => x.IsActive, ct), await db.SensorReadings.CountAsync(x => !x.IsValid && x.CapturedAtUtc >= since, ct), await db.CropCycles.CountAsync(x => x.Status == "Activo" || x.Status == "Planificado", ct), Math.Round(moisture ?? 0, 1), DateTime.UtcNow, latest, activity));
    }
}
