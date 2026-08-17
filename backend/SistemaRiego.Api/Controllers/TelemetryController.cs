using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/telemetry"), Authorize(Policy = Policies.Operator)]
public sealed class TelemetryController(AppDbContext db, ITelemetryIngestionService ingestion, IMqttCommandPublisher mqtt) : ControllerBase
{
    [HttpPost("readings"), Authorize(Policy = Policies.Technician)]
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

    [HttpPost("commands"), Authorize(Policy = Policies.Technician)]
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

    [HttpPatch("commands/{id:guid}/confirm"), Authorize(Policy = Policies.Technician)]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken cancellationToken)
    {
        var command = await db.IoTCommands.FindAsync([id], cancellationToken);
        if (command is null) return NotFound();
        command.Status = "Confirmado";
        command.ConfirmedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static IQueryable<ReadingResponse> Project(IQueryable<SensorReading> query) => query.Select(x => new ReadingResponse(
        x.Id, x.SensorId, x.Sensor.Name, x.IrrigationZone != null ? x.IrrigationZone.Name : null,
        x.CapturedAtUtc, x.Value, x.Sensor.MeasurementUnit.Symbol, x.BatteryPercent, x.SignalStrength,
        x.IsValid, x.ValidationStatus, x.Transport));

    private Guid? CurrentUserId() => Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
}
