using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Services;

public sealed class IrrigationAckService(AppDbContext db, IConsumptionCalculator? consumptionCalculator = null)
{
    public async Task<Guid?> ProcessAsync(Guid deviceId, string payload, CancellationToken ct)
    {
        var ack = Parse(payload);
        var command = ack.CommandId is Guid id
            ? await db.IoTCommands.SingleOrDefaultAsync(x => x.Id == id && x.DeviceId == deviceId, ct)
            : await db.IoTCommands.Where(x => x.DeviceId == deviceId && (x.Status == "Pendiente" || x.Status == "Publicado")).OrderByDescending(x => x.RequestedAtUtc).FirstOrDefaultAsync(ct);
        if (command is null) return null;

        var now = DateTime.UtcNow;
        command.Status = "Confirmado"; command.ConfirmedAtUtc = now; command.FailureReason = null;
        var state = await db.ValveRuntimeStates.SingleOrDefaultAsync(x => x.DeviceId == deviceId, ct);
        if (state is null) { state = new ValveRuntimeState { DeviceId = deviceId }; db.ValveRuntimeStates.Add(state); }
        state.IrrigationZoneId = command.IrrigationZoneId; state.LastCommandId = command.Id; state.LastAckAtUtc = now;
        var reportedOpen = ack.IsOpen ?? ack.Status.Contains("abierta", StringComparison.OrdinalIgnoreCase);
        if (command.CommandType == "ABRIR_VALVULA") { state.IsOpen = true; state.State = "Abierta"; }
        else if (command.CommandType == "CERRAR_VALVULA") { state.IsOpen = false; state.State = "Cerrada"; }
        else { state.IsOpen = reportedOpen; state.State = reportedOpen ? "Abierta" : "Cerrada"; state.LastReconciledAtUtc = now; }

        if (command.IrrigationRunId is long runId)
        {
            var run = await db.IrrigationRuns.Include(x => x.IrrigationZone).SingleOrDefaultAsync(x => x.Id == runId, ct);
            if (run is not null && command.CommandType == "ABRIR_VALVULA")
            {
                run.Status = "En curso"; run.StartedAtUtc ??= now;
                db.OperationalEvents.Add(Event(run, "IRRIGATION_OPEN_ACK", $"{run.IrrigationZone.Name}: apertura confirmada por MQTT."));
            }
            else if (run is not null && command.CommandType == "CERRAR_VALVULA")
            {
                await db.SaveChangesAsync(ct);
                var pendingClose = await db.IoTCommands.AnyAsync(x => x.IrrigationRunId == runId && x.CommandType == "CERRAR_VALVULA" && x.Status != "Confirmado" && x.Status != "Fallido", ct);
                if (!pendingClose) await CompleteRun(run, now, ct);
            }
        }
        else if (command.CommandType == "CONSULTAR_ESTADO" && command.IrrigationZoneId is Guid zoneId)
        {
            var active = await db.IrrigationRuns.Where(x => x.IrrigationZoneId == zoneId && (x.Status == "En curso" || x.Status == "Esperando ACK" || x.Status == "Cierre pendiente")).OrderByDescending(x => x.RequestedAtUtc).FirstOrDefaultAsync(ct);
            if (active is not null && !reportedOpen)
            {
                active.Status = "Reconciliado detenido"; active.EndedAtUtc = now;
                db.OperationalEvents.Add(Event(active, "VALVE_STATE_RECONCILED", "El broker informó válvula cerrada; el estado local fue corregido."));
            }
            else if (active is null && reportedOpen)
                db.OperationalEvents.Add(new OperationalEvent { Category = "Seguridad", EventType = "UNEXPECTED_OPEN_VALVE", Severity = "Crítico", IrrigationZoneId = zoneId, Detail = "La reconciliación MQTT detectó una válvula abierta sin riego activo." });
        }
        await db.SaveChangesAsync(ct);
        return command.Id;
    }

    private async Task CompleteRun(IrrigationRun run, DateTime now, CancellationToken ct)
    {
        if (run.EndedAtUtc.HasValue) return;
        var consumption = await (consumptionCalculator ?? new ConsumptionCalculator(db)).BuildAsync(run, now, ct);
        run.Status = "Detenido"; run.EndedAtUtc = now;
        if (!await db.WaterConsumptionRecords.AnyAsync(x => x.IrrigationRunId == run.Id, ct)) db.WaterConsumptionRecords.Add(consumption);
        db.OperationalEvents.Add(Event(run, "IRRIGATION_CLOSE_ACK", $"{run.IrrigationZone.Name}: cierre confirmado; {consumption.Source.ToLowerInvariant()} {consumption.VolumeLiters:0.0} L."));
    }

    private static OperationalEvent Event(IrrigationRun run, string type, string detail) => new() { Category = "Riego", EventType = type, IrrigationZoneId = run.IrrigationZoneId, IrrigationRunId = run.Id, Detail = detail };
    private static AckEnvelope Parse(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload); var root = doc.RootElement;
            Guid? id = root.TryGetProperty("commandId", out var value) && Guid.TryParse(value.ToString(), out var parsed) ? parsed : null;
            if (id is null && root.TryGetProperty("command", out var nested) && nested.ValueKind == JsonValueKind.String)
                try { using var command = JsonDocument.Parse(nested.GetString()!); if (command.RootElement.TryGetProperty("commandId", out value) && Guid.TryParse(value.ToString(), out parsed)) id = parsed; } catch (JsonException) { }
            var status = root.TryGetProperty("status", out value) ? value.GetString() ?? "ACK" : "ACK";
            bool? open = root.TryGetProperty("isOpen", out value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;
            return new(id, status, open);
        }
        catch (JsonException) { return new(null, payload, null); }
    }
    private sealed record AckEnvelope(Guid? CommandId, string Status, bool? IsOpen);
}
