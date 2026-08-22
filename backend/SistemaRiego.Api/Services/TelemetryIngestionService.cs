using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Hubs;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Services;

public enum TelemetryIngestionStatus { Accepted, Duplicate, UnknownSensor }
public sealed record TelemetryIngestionResult(TelemetryIngestionStatus Status, ReadingResponse? Reading = null);

public interface ITelemetryIngestionService
{
    Task<TelemetryIngestionResult> IngestAsync(TelemetryRequest request, string transport, CancellationToken cancellationToken);
}

public sealed class TelemetryIngestionService(AppDbContext db, IHubContext<TelemetryHub> hub) : ITelemetryIngestionService
{
    public async Task<TelemetryIngestionResult> IngestAsync(TelemetryRequest request, string transport, CancellationToken cancellationToken)
    {
        var messageId = request.MessageId.Trim();
        if (await db.SensorReadings.AnyAsync(x => x.MessageId == messageId, cancellationToken))
            return new(TelemetryIngestionStatus.Duplicate);
        var sensor = await db.IoTSensors.Include(x => x.MeasurementUnit).Include(x => x.Device).ThenInclude(x => x!.Node).SingleOrDefaultAsync(x => x.Id == request.SensorId && x.IsActive, cancellationToken);
        if (sensor is null) return new(TelemetryIngestionStatus.UnknownSensor);
        var capturedAt = request.CapturedAtUtc ?? DateTime.UtcNow;
        var isLate = sensor.LastReadingUtc.HasValue && capturedAt < sensor.LastReadingUtc.Value;
        var valid = true;
        var status = "Válida";
        if (request.Value < sensor.MinimumValue || request.Value > sensor.MaximumValue)
        {
            valid = false;
            status = "Fuera de rango";
        }
        var previous = await db.SensorReadings.Where(x => x.SensorId == sensor.Id && x.IsValid && x.CapturedAtUtc <= capturedAt).OrderByDescending(x => x.CapturedAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (valid && previous is not null && Math.Abs(request.Value - previous.Value) > (sensor.MaximumValue - sensor.MinimumValue) * .4m)
        {
            valid = false;
            status = "Salto brusco";
        }
        if (request.BatteryPercent is < 0 or > 100)
        {
            valid = false;
            status = "Batería inválida";
        }
        if (isLate) status += " · Atrasada";
        var reading = new SensorReading
        {
            SensorId = sensor.Id, IrrigationZoneId = request.IrrigationZoneId,
            CapturedAtUtc = capturedAt, Value = request.Value,
            BatteryPercent = request.BatteryPercent, SignalStrength = request.SignalStrength,
            MessageId = messageId, IsValid = valid, ValidationStatus = status,
            Transport = transport.Trim().ToUpperInvariant()
        };
        db.SensorReadings.Add(reading);
        if (!sensor.LastReadingUtc.HasValue || capturedAt > sensor.LastReadingUtc.Value)
            sensor.LastReadingUtc = capturedAt;
        var resolvedAlerts = new List<SystemAlert>();
        var onlineId = await db.MasterCatalogItems.Where(x => x.Kind == CatalogKind.OperationalStatus && x.Code == "ACTIVE").Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);
        if (onlineId.HasValue)
        {
            sensor.OperationalStatusId = onlineId.Value;
            if (sensor.Device is not null)
            {
                sensor.Device.LastCommunicationUtc = DateTime.UtcNow;
                sensor.Device.OperationalStatusId = onlineId.Value;
                if (sensor.Device.Node is not null)
                {
                    sensor.Device.Node.LastCommunicationUtc = DateTime.UtcNow;
                    sensor.Device.Node.OperationalStatusId = onlineId.Value;
                }
            }
            var recoveredFingerprints = new List<string> { $"IOT:SENSOR:{sensor.Id}:OFFLINE" };
            if (sensor.Device is not null)
            {
                recoveredFingerprints.Add($"IOT:DEVICE:{sensor.Device.Id}:OFFLINE");
                if (sensor.Device.Node is not null) recoveredFingerprints.Add($"IOT:NODE:{sensor.Device.Node.Id}:OFFLINE");
            }
            foreach (var alert in await db.SystemAlerts.Where(x => recoveredFingerprints.Contains(x.Fingerprint) && (x.Status == "Activa" || x.Status == "Reconocida")).ToListAsync(cancellationToken))
            {
                alert.Status = "Resuelta";
                alert.ResolvedAtUtc = DateTime.UtcNow;
                resolvedAlerts.Add(alert);
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        foreach (var alert in resolvedAlerts)
            await hub.Clients.All.SendAsync(TelemetryHub.AlertResolved, new { alert.Id, alert.Status, alert.ResolvedAtUtc }, cancellationToken);
        var zoneName = reading.IrrigationZoneId.HasValue
            ? await db.IrrigationZones.Where(x => x.Id == reading.IrrigationZoneId).Select(x => x.Name).SingleOrDefaultAsync(cancellationToken)
            : null;
        var response = new ReadingResponse(reading.Id, sensor.Id, sensor.Name, zoneName, reading.CapturedAtUtc,
            reading.Value, sensor.MeasurementUnit.Symbol, reading.BatteryPercent, reading.SignalStrength,
            reading.IsValid, reading.ValidationStatus, reading.Transport);
        await hub.Clients.All.SendAsync(TelemetryHub.ReadingReceived, response, cancellationToken);
        return new(TelemetryIngestionStatus.Accepted, response);
    }
}
