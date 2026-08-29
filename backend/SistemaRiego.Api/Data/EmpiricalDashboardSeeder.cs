using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Data;

public static class EmpiricalDashboardSeeder
{
    private const string Marker = "DEMO_EMPIRICAL_DATA_V1";

    // Estructura territorial permanente: sectores B y C con sus zonas. Describe la
    // instalación, no una muestra, así que se siembra en todos los entornos.
    // Sprint3Seeder les asigna después los polígonos y coordenadas definitivas.
    public static async Task SeedStructureAsync(AppDbContext db)
    {
        var block = await db.FarmBlocks.OrderBy(x => x.Name).FirstOrDefaultAsync();
        var active = await db.MasterCatalogItems.SingleOrDefaultAsync(x => x.Kind == CatalogKind.OperationalStatus && x.Code == "ACTIVE");
        var sensorA = await db.IoTSensors.SingleOrDefaultAsync(x => x.Code == "HUM-SUELO-A1");
        var sensorB = await db.IoTSensors.SingleOrDefaultAsync(x => x.Code == "HUM-SUELO-B1");
        if (block is null || active is null || sensorA is null || sensorB is null) return;

        var sectorSouth = await db.IrrigationSectors.SingleOrDefaultAsync(x => x.Code == "SECTOR-B");
        if (sectorSouth is null)
        {
            sectorSouth = new IrrigationSector { FarmBlockId = block.Id, Code = "SECTOR-B", Name = "Sector de riego sur", AreaHectares = .38m, SlopePercent = 1.8m };
            db.IrrigationSectors.Add(sectorSouth);
        }
        var sectorGreenhouse = await db.IrrigationSectors.SingleOrDefaultAsync(x => x.Code == "SECTOR-C");
        if (sectorGreenhouse is null)
        {
            sectorGreenhouse = new IrrigationSector { FarmBlockId = block.Id, Code = "SECTOR-C", Name = "Sector de invernadero", AreaHectares = .22m, SlopePercent = .7m };
            db.IrrigationSectors.Add(sectorGreenhouse);
        }
        await db.SaveChangesAsync();

        if (!await db.IrrigationZones.AnyAsync(x => x.Code == "ZONA-B1"))
            db.IrrigationZones.Add(new IrrigationZone { IrrigationSectorId = sectorSouth.Id, Code = "ZONA-B1", Name = "Zona chile B1", AreaHectares = .24m, OperationalStatusId = active.Id, PrimarySensorId = sensorB.Id, Latitude = 16.91908m, Longitude = -89.88565m });
        if (!await db.IrrigationZones.AnyAsync(x => x.Code == "ZONA-C1"))
            db.IrrigationZones.Add(new IrrigationZone { IrrigationSectorId = sectorGreenhouse.Id, Code = "ZONA-C1", Name = "Zona semillero C1", AreaHectares = .16m, OperationalStatusId = active.Id, PrimarySensorId = sensorA.Id, Latitude = 16.91898m, Longitude = -89.88550m });
        await db.SaveChangesAsync();
    }

    // Muestra sintética de 84 días para probar tendencias, filtros y exportaciones.
    // NO proviene de hardware conectado: solo debe sembrarse en entornos de demostración.
    public static async Task SeedDemoHistoryAsync(AppDbContext db)
    {
        if (await db.OperationalEvents.AnyAsync(x => x.EventType == Marker)) return;

        var sensorA = await db.IoTSensors.SingleOrDefaultAsync(x => x.Code == "HUM-SUELO-A1");
        var sensorB = await db.IoTSensors.SingleOrDefaultAsync(x => x.Code == "HUM-SUELO-B1");
        var zoneNorth = await db.IrrigationZones.SingleOrDefaultAsync(x => x.Code == "ZONA-A1");
        var zoneSouth = await db.IrrigationZones.SingleOrDefaultAsync(x => x.Code == "ZONA-B1");
        var zoneGreenhouse = await db.IrrigationZones.SingleOrDefaultAsync(x => x.Code == "ZONA-C1");
        if (sensorA is null || sensorB is null || zoneNorth is null || zoneSouth is null || zoneGreenhouse is null) return;

        var user = await db.Users.OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync();
        var zones = new[]
        {
            new SampleZone(zoneNorth, sensorA, 2, 18, 11.8m, 47m),
            new SampleZone(zoneSouth, sensorB, 3, 15, 9.6m, 52m),
            new SampleZone(zoneGreenhouse, sensorA, 4, 11, 7.4m, 58m)
        };
        decimal[] efficiency = [.94m, .98m, 1.01m, .96m, 1.03m, .99m, .95m];
        var today = DateTime.UtcNow.Date;

        for (var offset = 83; offset >= 0; offset--)
        {
            var day = today.AddDays(-offset);
            foreach (var sample in zones)
            {
                var moisture = sample.BaseMoisture + ((offset * 7 + sample.Frequency) % 13 - 6) * .65m;
                var messageId = $"FIELD-DEMO-{sample.Zone.Code}-{day:yyyyMMdd}";
                db.SensorReadings.Add(new SensorReading
                {
                    SensorId = sample.Sensor.Id, IrrigationZoneId = sample.Zone.Id, CapturedAtUtc = day.AddHours(5).AddMinutes(20), ReceivedAtUtc = day.AddHours(5).AddMinutes(21),
                    Value = Math.Round(moisture, 1), BatteryPercent = 91 - offset % 9, SignalStrength = -56 - offset % 8,
                    MessageId = messageId, Transport = "MUESTRA_CAMPO_PRUEBA", IsValid = true, ValidationStatus = "Válida"
                });

                if (offset % sample.Frequency != 0) continue;
                var duration = sample.BaseDuration + (offset % 5 - 2);
                var flow = sample.FlowRate + (offset % 3 - 1) * .2m;
                var volume = Math.Round(duration * flow * efficiency[offset % efficiency.Length], 1);
                var ended = day.AddHours(6).AddMinutes(35 + sample.Frequency * 3);
                var mode = offset % 10 == 0 ? "Manual" : "Automático";
                var run = new IrrigationRun
                {
                    IrrigationZoneId = sample.Zone.Id, Mode = mode, Status = "Completado", PlannedDurationMinutes = duration,
                    FlowRateLitersMinute = flow, RequestedAtUtc = ended.AddMinutes(-duration - 2), StartedAtUtc = ended.AddMinutes(-duration), EndedAtUtc = ended,
                    RequestedByUserId = user?.Id, RequestedByEmail = user?.Email,
                    Reason = moisture < sample.BaseMoisture ? "Humedad por debajo del rango de referencia" : "Programa agronómico de prueba",
                    Observations = "Dato de demostración calibrado con variación diaria; no proviene de hardware conectado."
                };
                db.IrrigationRuns.Add(run);
                await db.SaveChangesAsync();
                db.WaterConsumptionRecords.Add(new WaterConsumptionRecord
                {
                    IrrigationRunId = run.Id, IrrigationZoneId = sample.Zone.Id, Source = "Muestra calibrada de prueba",
                    FlowRateLitersMinute = flow, DurationMinutes = duration, VolumeLiters = volume, RecordedAtUtc = ended
                });
                db.OperationalEvents.Add(new OperationalEvent
                {
                    Category = "Riego", EventType = "FIELD_SAMPLE_IRRIGATION", Severity = "Informativo", IrrigationZoneId = sample.Zone.Id,
                    IrrigationRunId = run.Id, UserId = user?.Id, UserEmail = user?.Email,
                    Detail = $"Muestra de prueba: {volume:0.0} L, {duration} min, {flow:0.0} L/min y humedad inicial de {moisture:0.0}%.", OccurredAtUtc = ended
                });
            }

            if (offset % 14 == 0)
                db.OperationalEvents.Add(new OperationalEvent { Category = "Mantenimiento", EventType = "VALVE_INSPECTION", Severity = "Informativo", IrrigationZoneId = zoneSouth.Id, UserId = user?.Id, UserEmail = user?.Email, Detail = "Inspección preventiva de válvula y verificación de caudal para la muestra de prueba.", OccurredAtUtc = day.AddHours(10) });
            if (offset % 21 == 0)
                db.OperationalEvents.Add(new OperationalEvent { Category = "Alerta", EventType = "LOW_SOIL_MOISTURE", Severity = "Advertencia", IrrigationZoneId = zoneNorth.Id, Detail = "Humedad temporalmente por debajo del rango de referencia; evento incluido para validar alertas.", OccurredAtUtc = day.AddHours(5) });
        }

        db.OperationalEvents.Add(new OperationalEvent
        {
            Category = "Sistema", EventType = Marker, Severity = "Informativo",
            Detail = "Muestra reproducible de 84 días creada para probar tendencias, sectores, filtros y exportaciones. No corresponde a hardware conectado.", OccurredAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private sealed record SampleZone(IrrigationZone Zone, IoTSensor Sensor, int Frequency, int BaseDuration, decimal FlowRate, decimal BaseMoisture);
}
