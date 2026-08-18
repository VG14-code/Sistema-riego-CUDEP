using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Data;

public static class Sprint6Seeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.GlobalParameters.AnyAsync(x => x.Key == "AUDIT_RETENTION_DAYS")) return;
        db.GlobalParameters.Add(new GlobalParameter
        {
            Key = "AUDIT_RETENTION_DAYS",
            Value = "730",
            DataType = "integer",
            Category = "Auditoría",
            Description = "Días de conservación de la auditoría sensible (mínimo 30, máximo 3650).",
            IsEditable = true
        });
        await db.SaveChangesAsync();
    }
}
