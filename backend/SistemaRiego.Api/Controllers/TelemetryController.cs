using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/telemetry"), Authorize(Policy = PermissionPolicies.TelemetryRead)]
public sealed class TelemetryController(AppDbContext db, ITelemetryIngestionService ingestion, IMqttCommandPublisher mqtt) : ControllerBase
{
    [HttpPost("readings"), Authorize(Policy = PermissionPolicies.TelemetryManage)]
    public async Task<ActionResult<ReadingResponse>> Receive(TelemetryRequest request, CancellationToken cancellationToken)
    {
        var transport = string.IsNullOrWhiteSpace(request.Transport) ? "HTTP" : request.Transport;
        var result = await ingestion.IngestAsync(request, transport, cancellationToken);
        return result.Status switch
        {
            TelemetryIngestionStatus.Duplicate => Conflict(new { message = "La trama ya fue recibida." }),
            TelemetryIngestionStatus.UnknownSensor => BadRequest(new { message = "Sensor no autorizado o inactivo." }),
            _ => Ok(result.Reading)
        };
    }

    [HttpGet("latest")]
    public async Task<IActionResult> Latest(CancellationToken cancellationToken)
    {
        var query = db.SensorReadings.AsNoTracking()
            .OrderByDescending(x => x.CapturedAtUtc)
            .Take(30);
        return Ok(await Project(query).ToListAsync(cancellationToken));
    }

    [HttpGet("history")]
    public async Task<IActionResult> History(Guid? sensorId, DateTime? from, DateTime? to, int take = 250, CancellationToken cancellationToken = default)
    {
        var query = db.SensorReadings.AsNoTracking().AsQueryable();
        if (sensorId.HasValue) query = query.Where(x => x.SensorId == sensorId);
        if (from.HasValue) query = query.Where(x => x.CapturedAtUtc >= from);
        if (to.HasValue) query = query.Where(x => x.CapturedAtUtc <= to);
        query = query.OrderByDescending(x => x.CapturedAtUtc).Take(Math.Clamp(take, 1, 1000));
        return Ok(await Project(query).ToListAsync(cancellationToken));
    }

    [HttpGet("history/paged")]
    public async Task<ActionResult<PagedReadingResponse>> PagedHistory(Guid? sensorId, DateTime? from, DateTime? to, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 10, 200);
        var query = FilterHistory(sensorId, from, to);
        var total = await query.CountAsync(cancellationToken);
        var items = await Project(query.OrderByDescending(x => x.CapturedAtUtc).Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(cancellationToken);
        return Ok(new PagedReadingResponse(items, page, pageSize, total, Math.Max(1, (int)Math.Ceiling(total / (decimal)pageSize))));
    }

    [HttpGet("aggregates")]
    public async Task<ActionResult<IReadOnlyCollection<TelemetryAggregateResponse>>> Aggregates(Guid? sensorId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var query = FilterHistory(sensorId, from ?? DateTime.UtcNow.AddDays(-7), to);
        var rows = await query.GroupBy(x => new { x.SensorId, x.Sensor.Name, x.Sensor.MeasurementUnit.Symbol })
            .Select(group => new { group.Key.SensorId, group.Key.Name, group.Key.Symbol, Count = group.Count(), Minimum = group.Min(x => x.Value), Maximum = group.Max(x => x.Value), Average = group.Average(x => x.Value), FromUtc = group.Min(x => x.CapturedAtUtc), ToUtc = group.Max(x => x.CapturedAtUtc) })
            .OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return Ok(rows.Select(row => new TelemetryAggregateResponse(row.SensorId, row.Name, row.Symbol, row.Count, row.Minimum, row.Maximum, Math.Round(row.Average, 2), row.FromUtc, row.ToUtc)).ToList());
    }
    [HttpGet("quality")]
    public async Task<IActionResult> Quality(CancellationToken cancellationToken)
    {
        var total = await db.SensorReadings.CountAsync(cancellationToken);
        var valid = await db.SensorReadings.CountAsync(x => x.IsValid, cancellationToken);
        var sensors = await db.IoTSensors.Where(x => x.IsActive).CountAsync(cancellationToken);
        var reporting = await db.SensorReadings.Where(x => x.ReceivedAtUtc > DateTime.UtcNow.AddHours(-24)).Select(x => x.SensorId).Distinct().CountAsync(cancellationToken);
        return Ok(new
        {
            total,
            valid,
            invalid = total - valid,
            validPercent = total == 0 ? 0 : Math.Round(valid * 100m / total, 1),
            activeSensors = sensors,
            reportingSensors = reporting,
            availabilityPercent = sensors == 0 ? 0 : Math.Round(reporting * 100m / sensors, 1)
        });
    }

    [HttpGet("commands")]
    public async Task<IActionResult> Commands(CancellationToken cancellationToken) => Ok(await db.IoTCommands.AsNoTracking()
        .OrderByDescending(x => x.RequestedAtUtc)
        .Take(100)
        .Select(x => new { x.Id, x.DeviceId, Device = x.Device.Name, x.CommandType, x.Payload, x.Status, x.RequestedAtUtc, x.ConfirmedAtUtc })
        .ToListAsync(cancellationToken));

    [HttpPost("commands"), Authorize(Policy = PermissionPolicies.TelemetryManage)]
    public async Task<IActionResult> Command(CommandRequest request, CancellationToken cancellationToken)
    {
        var device = await db.IoTDevices.AsNoTracking().Where(x => x.Id == request.DeviceId && x.IsActive)
            .Select(x => new { x.Id, x.Code }).SingleOrDefaultAsync(cancellationToken);
        if (device is null) return BadRequest(new { message = "Dispositivo no disponible." });
        var zoneCode = await db.IrrigationZones.AsNoTracking().Where(x => (x.ValveDeviceId == request.DeviceId || x.Valves.Any(v => v.DeviceId == request.DeviceId)) && x.IsActive)
            .Select(x => x.Code).SingleOrDefaultAsync(cancellationToken) ?? device.Code;
        var command = new IoTCommand
        {
            DeviceId = request.DeviceId,
            CommandType = request.CommandType.Trim().ToUpperInvariant(),
            Payload = request.Payload,
            RequestedByUserId = CurrentUserId()
        };
        db.Add(command);
        db.AccessAudits.Add(new AccessAudit { UserId = CurrentUserId(), EventType = "IOT_COMMAND_REQUESTED", Detail = $"{command.CommandType}:{command.DeviceId}" });
        await db.SaveChangesAsync(cancellationToken);
        await mqtt.PublishCommandAsync(zoneCode, command.DeviceId, new { commandId = command.Id, commandType = command.CommandType, payload = command.Payload }, cancellationToken);
        command.Status = "Publicado"; await db.SaveChangesAsync(cancellationToken);
        return Ok(command);
    }

    [HttpPatch("commands/{id:guid}/confirm"), Authorize(Policy = PermissionPolicies.TelemetryManage)]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken cancellationToken)
    {
        var command = await db.IoTCommands.FindAsync([id], cancellationToken);
        if (command is null) return NotFound();
        command.Status = "Confirmado";
        command.ConfirmedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private IQueryable<SensorReading> FilterHistory(Guid? sensorId, DateTime? from, DateTime? to)
    {
        var query = db.SensorReadings.AsNoTracking().AsQueryable();
        if (sensorId.HasValue) query = query.Where(x => x.SensorId == sensorId);
        if (from.HasValue) query = query.Where(x => x.CapturedAtUtc >= from);
        if (to.HasValue) query = query.Where(x => x.CapturedAtUtc <= to);
        return query;
    }
    private static IQueryable<ReadingResponse> Project(IQueryable<SensorReading> query) => query.Select(x => new ReadingResponse(
        x.Id, x.SensorId, x.Sensor.Name, x.IrrigationZone != null ? x.IrrigationZone.Name : null,
        x.CapturedAtUtc, x.Value, x.Sensor.MeasurementUnit.Symbol, x.BatteryPercent, x.SignalStrength,
        x.IsValid, x.ValidationStatus, x.Transport));

    private Guid? CurrentUserId() => Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
}
