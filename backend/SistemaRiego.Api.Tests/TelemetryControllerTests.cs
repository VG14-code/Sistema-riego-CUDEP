using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Controllers;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Hubs;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Tests;

public sealed class TelemetryControllerTests
{
    [Fact]
    public async Task Latest_ReturnsNewestReadingsFirst()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var status = new MasterCatalogItem { Kind = CatalogKind.OperationalStatus, Code = "ONLINE", Name = "En línea" };
        var type = new MasterCatalogItem { Kind = CatalogKind.SensorType, Code = "MOISTURE", Name = "Humedad" };
        var unit = new MasterCatalogItem { Kind = CatalogKind.MeasurementUnit, Code = "PERCENT", Name = "Porcentaje", Symbol = "%" };
        var sensor = new IoTSensor
        {
            Code = "HUM-TEST", Name = "Humedad de prueba", SerialNumber = "TEST-01",
            SensorType = type, MeasurementUnit = unit, OperationalStatus = status
        };
        db.AddRange(status, type, unit, sensor);
        var older = DateTime.UtcNow.AddMinutes(-1);
        db.SensorReadings.AddRange(
            new SensorReading { Sensor = sensor, CapturedAtUtc = older, Value = 40, MessageId = "ORDER-OLD" },
            new SensorReading { Sensor = sensor, CapturedAtUtc = older.AddSeconds(30), Value = 45, MessageId = "ORDER-NEW" });
        await db.SaveChangesAsync();

        var controller = new TelemetryController(db, new NoopIngestion(), new NoopPublisher());
        var result = Assert.IsType<OkObjectResult>(await controller.Latest(default));
        var readings = Assert.IsAssignableFrom<IReadOnlyList<ReadingResponse>>(result.Value);

        Assert.Equal(45, readings[0].Value);
        Assert.Equal(40, readings[1].Value);
    }

    private sealed class NoopIngestion : ITelemetryIngestionService
    {
        public Task<TelemetryIngestionResult> IngestAsync(TelemetryRequest request, string transport, CancellationToken cancellationToken) =>
            Task.FromResult(new TelemetryIngestionResult(TelemetryIngestionStatus.Accepted));
    }

    private sealed class NoopPublisher : IMqttCommandPublisher
    {
        public Task PublishCommandAsync(string zone, Guid deviceId, object payload, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
