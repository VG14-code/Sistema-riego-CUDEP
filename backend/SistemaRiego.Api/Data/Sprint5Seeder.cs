using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Data;

public static class Sprint5Seeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await Add(db, "ALERT_ESCALATION_MINUTES", "15", "Minutos sin reconocimiento antes de re-notificar una alerta crítica.");
        await Add(db, "MAINTENANCE_INCIDENT_MINUTES", "30", "Minutos de alerta crítica activa antes de generar una incidencia automática.");
        if (!await db.MaintenancePlans.AnyAsync())
        {
            var pump = await db.WaterPumps.OrderBy(x => x.Name).FirstOrDefaultAsync();
            if (pump is not null) db.MaintenancePlans.Add(new MaintenancePlan { Name = "Inspección preventiva de bomba", Frequency = "Recurrente", IntervalDays = 30, EquipmentType = "Bomba", EquipmentId = pump.Id.ToString(), ScheduledAtUtc = DateTime.UtcNow.AddDays(7), NextDueAtUtc = DateTime.UtcNow.AddDays(7), Notes = "Revisar corriente, presión, conexiones y sello mecánico." });
        }
        await db.SaveChangesAsync();
    }
    private static async Task Add(AppDbContext db, string key, string value, string description)
    { if (!await db.GlobalParameters.AnyAsync(x => x.Key == key)) db.GlobalParameters.Add(new GlobalParameter { Key = key, Value = value, DataType = "integer", Category = "Alertas y mantenimiento", Description = description }); }
}
