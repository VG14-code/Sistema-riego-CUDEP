using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Data;

public static class Sprint3Seeder
{
    private const string LegacySectorBoundary = "{\"type\":\"Polygon\",\"coordinates\":[[[-89.8920,16.9258],[-89.8907,16.9258],[-89.8907,16.9271],[-89.8920,16.9271],[-89.8920,16.9258]]]}";
    private const string LegacyZoneBoundary = "{\"type\":\"Polygon\",\"coordinates\":[[[-89.8918,16.9260],[-89.8911,16.9260],[-89.8911,16.9268],[-89.8918,16.9268],[-89.8918,16.9260]]]}";
    private const string SectorBoundary = "{\"type\":\"Polygon\",\"coordinates\":[[[-89.88610,16.91882],[-89.88538,16.91882],[-89.88538,16.91946],[-89.88610,16.91946],[-89.88610,16.91882]]]}";
    private const string ZoneBoundary = "{\"type\":\"Polygon\",\"coordinates\":[[[-89.88603,16.91902],[-89.88575,16.91902],[-89.88575,16.91932],[-89.88603,16.91932],[-89.88603,16.91902]]]}";

    public static async Task SeedAsync(AppDbContext db)
    {
        var sector = await db.IrrigationSectors.SingleOrDefaultAsync(x => x.Code == "SECTOR-A");
        var zone = await db.IrrigationZones.SingleOrDefaultAsync(x => x.Code == "ZONA-A1");
        if (sector is not null && (sector.BoundaryGeoJson is null || sector.BoundaryGeoJson == LegacySectorBoundary)) sector.BoundaryGeoJson = SectorBoundary;
        if (zone is not null && (zone.BoundaryGeoJson is null || zone.BoundaryGeoJson == LegacyZoneBoundary)) zone.BoundaryGeoJson = ZoneBoundary;
        var farm = await db.Farms.SingleOrDefaultAsync(x => x.Code == "GRANJA-CUDEP");
        if (farm is not null && farm.Latitude == 16.9264m && farm.Longitude == -89.8914m) { farm.Latitude = 16.91916m; farm.Longitude = -89.88578m; }
        if (zone is not null && zone.Latitude == 16.9265m && zone.Longitude == -89.8912m) { zone.Latitude = 16.91915m; zone.Longitude = -89.88590m; }
        var zoneB = await db.IrrigationZones.SingleOrDefaultAsync(x => x.Code == "ZONA-B1");
        if (zoneB is not null && zoneB.Latitude == 16.9261m && zoneB.Longitude == -89.8910m) { zoneB.Latitude = 16.91908m; zoneB.Longitude = -89.88565m; }
        var zoneC = await db.IrrigationZones.SingleOrDefaultAsync(x => x.Code == "ZONA-C1");
        if (zoneC is not null && zoneC.Latitude == 16.9259m && zoneC.Longitude == -89.8908m) { zoneC.Latitude = 16.91898m; zoneC.Longitude = -89.88550m; }

        IoTDevice? valve = await db.IoTDevices.SingleOrDefaultAsync(x => x.Code == "VALVULA-A1");
        if (valve is null)
        {
            var valveType = await db.MasterCatalogItems.SingleAsync(x => x.Kind == CatalogKind.DeviceType && x.Code == "SOLENOID_VALVE");
            var active = await db.MasterCatalogItems.SingleAsync(x => x.Kind == CatalogKind.OperationalStatus && x.Code == "ACTIVE");
            valve = new IoTDevice { Code = "VALVULA-A1", Name = "Válvula solenoide A1", SerialNumber = "VLV-CUDEP-A1", Manufacturer = "Simulada", Model = "MQTT-24V", InstallationLocation = "Zona tomate A1", DeviceTypeId = valveType.Id, OperationalStatusId = active.Id, NodeId = await db.IoTNodes.OrderBy(x => x.Code).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(), LastCommunicationUtc = DateTime.UtcNow };
            db.IoTDevices.Add(valve);
        }

        await UpsertParameter(db, "MAX_SIMULTANEOUS_VALVES", "2", "integer", "Automatización", "Límite global de válvulas abiertas simultáneamente.");
        await UpsertParameter(db, "MQTT_COMMAND_TIMEOUT_SECONDS", "15", "integer", "Automatización", "Tiempo máximo para recibir ACK de una orden MQTT.");
        await UpsertParameter(db, "AUTOMATION_MAX_COMMAND_ATTEMPTS", "3", "integer", "Automatización", "Intentos automáticos máximos desde el último ACK antes de suspender una regla.");
        await UpsertParameter(db, "AUTOMATION_INTERVAL_SECONDS", "10", "integer", "Automatización", "Intervalo del evaluador automático de reglas.");
        await db.SaveChangesAsync();

        if (zone is not null)
        {
            zone.ValveDeviceId ??= valve.Id;
            if (!await db.IrrigationZoneValves.AnyAsync(x => x.IrrigationZoneId == zone.Id && x.DeviceId == valve.Id)) db.IrrigationZoneValves.Add(new IrrigationZoneValve { IrrigationZoneId = zone.Id, DeviceId = valve.Id });
            foreach (var sensorId in await db.IoTSensors.Where(x => x.Code == "HUM-SUELO-A1" || x.Code == "TEMP-SUELO-A1").Select(x => x.Id).ToListAsync())
                if (!await db.IrrigationZoneSensors.AnyAsync(x => x.IrrigationZoneId == zone.Id && x.SensorId == sensorId)) db.IrrigationZoneSensors.Add(new IrrigationZoneSensor { IrrigationZoneId = zone.Id, SensorId = sensorId, IsPrimary = sensorId == zone.PrimarySensorId });
        }
        await db.SaveChangesAsync();
    }

    private static async Task UpsertParameter(AppDbContext db, string key, string value, string type, string category, string description)
    {
        if (!await db.GlobalParameters.AnyAsync(x => x.Key == key)) db.GlobalParameters.Add(new GlobalParameter { Key = key, Value = value, DataType = type, Category = category, Description = description });
    }
}
