using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Services;

public sealed class TerritoryIntegrityService(AppDbContext db, SpatialGeometryValidator? geometry = null)
{
    private readonly SpatialGeometryValidator geometry = geometry ?? new SpatialGeometryValidator();

    public async Task ValidateSectorAsync(SectorRequest request, Guid? currentId, CancellationToken ct)
    {
        if (!await db.FarmBlocks.AnyAsync(x => x.Id == request.FarmBlockId, ct)) throw new TerritoryIntegrityException("El bloque seleccionado no existe.");
        var sectorPolygon = geometry.ParsePolygon(request.BoundaryGeoJson, "sector");
        if (currentId is not Guid sectorId || sectorPolygon.Count == 0) return;
        var zones = await db.IrrigationZones.AsNoTracking().Where(x => x.IrrigationSectorId == sectorId && x.BoundaryGeoJson != null).Select(x => new { x.Name, x.BoundaryGeoJson }).ToListAsync(ct);
        foreach (var zone in zones)
            try { geometry.EnsureZoneInsideSector(geometry.ParsePolygon(zone.BoundaryGeoJson, $"zona {zone.Name}"), sectorPolygon); }
            catch (SpatialValidationException) { throw new TerritoryIntegrityException($"El nuevo límite dejaría fuera a la zona {zone.Name}."); }
    }

    public async Task ValidateZoneAsync(ZoneRequest request, Guid? currentId, CancellationToken ct)
    {
        var sector = await db.IrrigationSectors.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.IrrigationSectorId, ct)
            ?? throw new TerritoryIntegrityException("El sector seleccionado no existe.");
        if (!await db.MasterCatalogItems.AnyAsync(x => x.Id == request.OperationalStatusId && x.Kind == CatalogKind.OperationalStatus, ct)) throw new TerritoryIntegrityException("El estado operativo no existe.");

        var sensorIds = SensorIds(request);
        var valveIds = ValveIds(request);
        if (sensorIds.Count != await db.IoTSensors.CountAsync(x => sensorIds.Contains(x.Id) && x.IsActive, ct)) throw new TerritoryIntegrityException("Uno o más sensores seleccionados no existen o están inactivos.");
        if (valveIds.Count != await db.IoTDevices.CountAsync(x => valveIds.Contains(x.Id) && x.IsActive, ct)) throw new TerritoryIntegrityException("Uno o más dispositivos de válvula no existen o están inactivos.");

        var zonePolygon = geometry.ParsePolygon(request.BoundaryGeoJson, "zona");
        geometry.EnsureZoneInsideSector(zonePolygon, geometry.ParsePolygon(sector.BoundaryGeoJson, "sector"));
        if (zonePolygon.Count == 0) return;
        var siblings = await db.IrrigationZones.AsNoTracking().Where(x => x.IrrigationSectorId == request.IrrigationSectorId && x.Id != currentId && x.IsActive && x.BoundaryGeoJson != null).Select(x => new { x.Name, x.BoundaryGeoJson }).ToListAsync(ct);
        foreach (var sibling in siblings)
            if (geometry.Overlaps(zonePolygon, geometry.ParsePolygon(sibling.BoundaryGeoJson, $"zona {sibling.Name}")))
                throw new TerritoryIntegrityException($"El polígono se superpone con la zona {sibling.Name} del mismo sector.");
    }

    public async Task SyncAssignmentsAsync(IrrigationZone zone, ZoneRequest request, CancellationToken ct)
    {
        var sensors = SensorIds(request); var valves = ValveIds(request);
        var oldSensors = await db.IrrigationZoneSensors.Where(x => x.IrrigationZoneId == zone.Id).ToListAsync(ct);
        var oldValves = await db.IrrigationZoneValves.Where(x => x.IrrigationZoneId == zone.Id).ToListAsync(ct);
        db.IrrigationZoneSensors.RemoveRange(oldSensors); db.IrrigationZoneValves.RemoveRange(oldValves);
        db.IrrigationZoneSensors.AddRange(sensors.Select(id => new IrrigationZoneSensor { IrrigationZoneId = zone.Id, SensorId = id, IsPrimary = id == request.PrimarySensorId }));
        db.IrrigationZoneValves.AddRange(valves.Select(id => new IrrigationZoneValve { IrrigationZoneId = zone.Id, DeviceId = id }));
    }

    public static HashSet<Guid> SensorIds(ZoneRequest request) => (request.SensorIds ?? []).AppendIf(request.PrimarySensorId).ToHashSet();
    public static HashSet<Guid> ValveIds(ZoneRequest request) => (request.ValveDeviceIds ?? []).AppendIf(request.ValveDeviceId).ToHashSet();
}

internal static class TerritoryEnumerableExtensions
{
    public static IEnumerable<Guid> AppendIf(this IEnumerable<Guid> source, Guid? value) => value is Guid id ? source.Append(id) : source;
}

public sealed class TerritoryIntegrityException(string message) : Exception(message);
