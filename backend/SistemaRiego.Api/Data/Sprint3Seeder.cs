using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Data;

public static class Sprint3Seeder
{
    private const string SectorABoundary = """{"type":"Polygon","coordinates":[[[-89.9069,16.8815],[-89.9026,16.8815],[-89.9026,16.8860],[-89.9069,16.8860],[-89.9069,16.8815]]]}""";
    private const string ZoneABoundary = """{"type":"Polygon","coordinates":[[[-89.9066,16.8820],[-89.9029,16.8820],[-89.9029,16.8858],[-89.9066,16.8858],[-89.9066,16.8820]]]}""";
    private const string SectorBBoundary = """{"type":"Polygon","coordinates":[[[-89.9025,16.8797],[-89.8997,16.8797],[-89.8997,16.8855],[-89.9025,16.8855],[-89.9025,16.8797]]]}""";
    private const string ZoneBBoundary = """{"type":"Polygon","coordinates":[[[-89.9024,16.8800],[-89.8998,16.8800],[-89.8998,16.8852],[-89.9024,16.8852],[-89.9024,16.8800]]]}""";
    private const string SectorCBoundary = """{"type":"Polygon","coordinates":[[[-89.8996,16.8807],[-89.8966,16.8807],[-89.8966,16.8860],[-89.8996,16.8860],[-89.8996,16.8807]]]}""";
    private const string ZoneCBoundary = """{"type":"Polygon","coordinates":[[[-89.8995,16.8810],[-89.8967,16.8810],[-89.8967,16.8857],[-89.8995,16.8857],[-89.8995,16.8810]]]}""";

    public static async Task SeedAsync(AppDbContext db)
    {
        var center = await db.UniversityCenters.SingleOrDefaultAsync(x => x.Code == "CUDEP");
        if (center is not null) { center.Name = "Centro Universitario de Petén"; center.Location = "Santa Elena, Flores, Petén, Guatemala"; }
        var farm = await db.Farms.SingleOrDefaultAsync(x => x.Code == "GRANJA-CUDEP");
        if (farm is not null) { farm.Name = "Granja Experimental CUDEP"; farm.Location = "Centro Universitario de Petén, Santa Elena, Flores, Petén, Guatemala"; farm.Latitude = 16.88375m; farm.Longitude = -89.90087m; }
        await UpdateTerritory(db, "SECTOR-A", SectorABoundary, "ZONA-A1", ZoneABoundary, 16.88430m, -89.90450m);
        await UpdateTerritory(db, "SECTOR-B", SectorBBoundary, "ZONA-B1", ZoneBBoundary, 16.88320m, -89.90100m);
        await UpdateTerritory(db, "SECTOR-C", SectorCBoundary, "ZONA-C1", ZoneCBoundary, 16.88380m, -89.89780m);
        var zone = await db.IrrigationZones.SingleOrDefaultAsync(x => x.Code == "ZONA-A1");
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

    private static async Task UpdateTerritory(AppDbContext db, string sectorCode, string sectorBoundary, string zoneCode, string zoneBoundary, decimal latitude, decimal longitude)
    {
        var sector=await db.IrrigationSectors.SingleOrDefaultAsync(x=>x.Code==sectorCode);
        if(sector is not null) sector.BoundaryGeoJson=sectorBoundary;
        var zone=await db.IrrigationZones.SingleOrDefaultAsync(x=>x.Code==zoneCode);
        if(zone is not null){zone.BoundaryGeoJson=zoneBoundary;zone.Latitude=latitude;zone.Longitude=longitude;}
    }
    private static async Task UpsertParameter(AppDbContext db, string key, string value, string type, string category, string description)
    {
        if (!await db.GlobalParameters.AnyAsync(x => x.Key == key)) db.GlobalParameters.Add(new GlobalParameter { Key = key, Value = value, DataType = type, Category = category, Description = description });
    }
}
