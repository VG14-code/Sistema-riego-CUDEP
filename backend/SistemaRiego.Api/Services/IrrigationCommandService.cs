using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Services;

public interface IIrrigationCommandService
{
    Task<IReadOnlyList<IoTCommand>> SendAsync(Guid zoneId, IrrigationRun? run, string commandType, Guid? userId, CancellationToken ct);
    Task ReconcileAsync(CancellationToken ct);
}

public sealed class IrrigationCommandService(AppDbContext db, IMqttCommandPublisher mqtt, ILogger<IrrigationCommandService> logger) : IIrrigationCommandService
{
    public async Task<IReadOnlyList<IoTCommand>> SendAsync(Guid zoneId, IrrigationRun? run, string commandType, Guid? userId, CancellationToken ct)
    {
        var zone = await db.IrrigationZones.AsNoTracking().SingleOrDefaultAsync(x => x.Id == zoneId && x.IsActive, ct)
            ?? throw new InvalidOperationException("La zona no existe o está inactiva.");
        var devices = await db.IrrigationZoneValves.Where(x => x.IrrigationZoneId == zoneId).Select(x => x.DeviceId).ToListAsync(ct);
        if (devices.Count == 0 && zone.ValveDeviceId is Guid legacy) devices.Add(legacy);
        devices = devices.Distinct().ToList();
        if (devices.Count == 0) throw new InvalidOperationException($"La zona {zone.Name} no tiene válvulas asignadas.");
        var timeout = await ParameterInt("MQTT_COMMAND_TIMEOUT_SECONDS", 15, ct);
        var now = DateTime.UtcNow;
        var commands = devices.Select(deviceId => new IoTCommand { DeviceId = deviceId, IrrigationZoneId = zoneId, IrrigationRunId = run?.Id, CommandType = commandType, Status = "Pendiente", RequestedAtUtc = now, ExpiresAtUtc = now.AddSeconds(timeout), RequestedByUserId = userId }).ToList();
        db.IoTCommands.AddRange(commands); await db.SaveChangesAsync(ct);
        foreach (var command in commands)
        {
            command.Payload = System.Text.Json.JsonSerializer.Serialize(new { commandId = command.Id, commandType, irrigationZoneId = zoneId, irrigationRunId = run?.Id });
            try
            {
                await mqtt.PublishCommandAsync(zone.Code, command.DeviceId, new { commandId = command.Id, commandType, irrigationZoneId = zoneId, irrigationRunId = run?.Id }, ct);
                command.Status = "Publicado";
            }
            catch (Exception exception)
            {
                command.Status = "Fallido"; command.FailedAtUtc = DateTime.UtcNow; command.FailureReason = exception.Message;
                logger.LogError(exception, "Falló la publicación del comando {CommandId}", command.Id);
            }
        }
        await db.SaveChangesAsync(ct);
        if (commands.All(x => x.Status == "Fallido")) throw new InvalidOperationException("No fue posible publicar el comando en MQTT.");
        return commands;
    }

    public async Task ReconcileAsync(CancellationToken ct)
    {
        var zones = await db.IrrigationZones
            .Include(x => x.Valves).ThenInclude(x => x.Device).ThenInclude(x => x.OperationalStatus)
            .Include(x => x.ValveDevice).ThenInclude(x => x!.OperationalStatus)
            .Where(x => x.IsActive)
            .ToListAsync(ct);
        foreach (var zone in zones)
        {
            var valves = zone.Valves.Count > 0 ? zone.Valves.Select(x => x.Device).ToList() : zone.ValveDevice is null ? [] : [zone.ValveDevice];
            if (valves.Count == 0 || valves.Any(x => !x.IsActive || x.OperationalStatus.Code != "ACTIVE"))
            {
                logger.LogInformation("Se omite reconciliación automática de {Zone}: existe una válvula sin conexión.", zone.Name);
                continue;
            }
            try { await SendAsync(zone.Id, null, "CONSULTAR_ESTADO", null, ct); }
            catch (Exception exception) { logger.LogWarning(exception, "No se pudo reconciliar la zona {ZoneId}", zone.Id); }
        }
    }

    private async Task<int> ParameterInt(string key, int fallback, CancellationToken ct) => int.TryParse(await db.GlobalParameters.Where(x => x.Key == key).Select(x => x.Value).SingleOrDefaultAsync(ct), out var value) ? Math.Max(1, value) : fallback;
}
