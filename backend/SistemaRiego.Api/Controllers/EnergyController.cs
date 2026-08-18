using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/energy"), Authorize(Policy = Policies.Operator)]
public sealed class EnergyController(AppDbContext db) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult> Status(CancellationToken ct)
    {
        var controller = await db.ChargeControllers.AsNoTracking().Include(x => x.SolarPanelArray).Include(x => x.SolarBattery).FirstOrDefaultAsync(ct);
        var reading = await db.EnergyReadings.AsNoTracking().OrderByDescending(x => x.CapturedAtUtc).FirstOrDefaultAsync(ct);
        return controller is null ? NotFound() : Ok(new { controller.Id, controller.Name, controller.Status, array = controller.SolarPanelArray, battery = controller.SolarBattery, reading });
    }
    [HttpGet("history")]
    public async Task<ActionResult> History(int hours = 24, CancellationToken ct = default)
    {
        var from = DateTime.UtcNow.AddHours(-Math.Clamp(hours, 1, 720));
        return Ok(await db.EnergyReadings.AsNoTracking().Where(x => x.CapturedAtUtc >= from).OrderBy(x => x.CapturedAtUtc).Select(x => new { x.CapturedAtUtc, x.GenerationWatts, x.BatteryPercent, x.ConsumptionWatts, x.BatteryVoltage }).ToListAsync(ct));
    }
}
