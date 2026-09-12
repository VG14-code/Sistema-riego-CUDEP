using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Controllers;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Tests;

public sealed class Week14AnalyticsTests
{
    [Fact]
    public async Task HistoryPaged_ReturnsNewestFirstWithFilteredTotals()
    {
        await using var db = Db();
        for (var i = 0; i < 35; i++)
            db.OperationalEvents.Add(new OperationalEvent { Category = i % 5 == 0 ? "Abastecimiento" : "Riego manual", EventType = $"EVT_{i}", Detail = $"Evento {i}", OccurredAtUtc = new DateTime(2026, 8, 1).AddHours(i) });
        await db.SaveChangesAsync();
        var controller = new Week14AnalyticsController(db);

        // La lista simple cortaba en 300 eventos y la pantalla no podia llegar a los anteriores.
        var second = Page(await controller.HistoryPaged(null, null, null, null, null, null, 2, 15));
        Assert.Equal((35, 3, 2, 15), (second.Total, second.PageCount, second.Page, second.Items.Count));
        Assert.Equal("EVT_19", second.Items[0].EventType);

        var beyond = Page(await controller.HistoryPaged(null, null, null, null, null, null, 50, 15));
        Assert.Equal((3, 5), (beyond.Page, beyond.Items.Count));

        var supply = Page(await controller.HistoryPaged(null, null, "Abastecimiento", null, null, null, 1, 15));
        Assert.Equal((7, 1), (supply.Total, supply.PageCount));
    }

    [Fact]
    public async Task Dashboard_WithoutConsumptionInPeriod_ReportsTheLastRecord()
    {
        await using var db = Db();
        var lastRecord = DateTime.UtcNow.Date.AddDays(-40);
        db.WaterConsumptionRecords.Add(new WaterConsumptionRecord { IrrigationRunId = 1, IrrigationZoneId = Guid.NewGuid(), VolumeLiters = 120, RecordedAtUtc = lastRecord });
        await db.SaveChangesAsync();

        // Con 30 dias no hay consumos: la pantalla mostraba tarjetas vacias sin decir hasta cuando hay datos.
        var json = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await new Week14AnalyticsController(db).Dashboard(30)).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(0, json.GetProperty("eventCount").GetInt32());
        Assert.Equal(lastRecord, json.GetProperty("lastRecordedAtUtc").GetDateTime().Date);
    }

    private static Week14AnalyticsController.HistoryPage Page(ActionResult result) =>
        Assert.IsType<Week14AnalyticsController.HistoryPage>(Assert.IsType<OkObjectResult>(result).Value);

    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
