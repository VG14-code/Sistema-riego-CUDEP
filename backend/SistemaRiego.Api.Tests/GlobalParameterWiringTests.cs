using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Controllers;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Tests;

// Estos parametros existian en GlobalParameters y eran editables, pero ningun
// codigo los leia: cambiarlos no alteraba nada. Cada prueba fija que el valor
// almacenado influye de verdad en el comportamiento.
public sealed class GlobalParameterWiringTests
{
    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Theory]
    [InlineData(null, "10", 600)]        // sin anulacion manda el parametro global
    [InlineData(null, null, 20)]         // sin parametro, el valor de referencia
    [InlineData(20, "10", 20)]           // la anulacion del entorno gana
    [InlineData(null, "0", 20)]          // valor invalido cae al de referencia
    [InlineData(null, "no-es-numero", 20)]
    public void SensorOfflineMinutes_ResolvesWithDocumentedPrecedence(int? configuredSeconds, string? parameterMinutes, int expected) =>
        Assert.Equal(expected, IoTHealthOptions.ResolveOfflineSeconds(configuredSeconds, parameterMinutes));

    [Fact]
    public async Task TelemetryIntervalSeconds_DrivesExpectedReadingsAndCompleteness()
    {
        await using var db = Db();
        var status = new MasterCatalogItem { Kind = CatalogKind.OperationalStatus, Code = "ACTIVE", Name = "Activo" };
        var sensor = new IoTSensor { Code = "S-1", Name = "Sensor", SerialNumber = "SN-1", SensorTypeId = Guid.NewGuid(), MeasurementUnitId = Guid.NewGuid(), OperationalStatusId = status.Id, MinimumValue = 0, MaximumValue = 100 };
        db.AddRange(status, sensor);
        db.GlobalParameters.Add(new GlobalParameter { Key = "TELEMETRY_INTERVAL_SECONDS", Value = "3600", DataType = "integer", Category = "Telemetría", Description = "Prueba" });
        for (var i = 0; i < 6; i++)
            db.SensorReadings.Add(new SensorReading { MessageId = $"TEST-{i:00}", SensorId = sensor.Id, CapturedAtUtc = DateTime.UtcNow.AddHours(-i), ReceivedAtUtc = DateTime.UtcNow.AddHours(-i), Value = 50, IsValid = true, ValidationStatus = "Válida" });
        await db.SaveChangesAsync();

        var response = Assert.IsType<OkObjectResult>(await new TelemetryController(db, null!, null!).Quality(default));
        var payload = response.Value!;
        // Un sensor con intervalo de una hora deberia entregar 24 lecturas al dia.
        Assert.Equal(3600, Read<int>(payload, "telemetryIntervalSeconds"));
        Assert.Equal(24, Read<int>(payload, "expectedLastDay"));
        Assert.Equal(6, Read<int>(payload, "receivedLastDay"));
        Assert.Equal(25.0m, Read<decimal>(payload, "completenessPercent"));
    }

    [Fact]
    public async Task TelemetryIntervalSeconds_FallsBackWhenParameterIsMissing()
    {
        await using var db = Db();
        var status = new MasterCatalogItem { Kind = CatalogKind.OperationalStatus, Code = "ACTIVE", Name = "Activo" };
        db.AddRange(status, new IoTSensor { Code = "S-2", Name = "Sensor", SerialNumber = "SN-2", SensorTypeId = Guid.NewGuid(), MeasurementUnitId = Guid.NewGuid(), OperationalStatusId = status.Id, MinimumValue = 0, MaximumValue = 100 });
        await db.SaveChangesAsync();

        var response = Assert.IsType<OkObjectResult>(await new TelemetryController(db, null!, null!).Quality(default));
        Assert.Equal(60, Read<int>(response.Value!, "telemetryIntervalSeconds"));
        Assert.Equal(1440, Read<int>(response.Value!, "expectedLastDay"));
    }

    private static T Read<T>(object payload, string property) =>
        (T)payload.GetType().GetProperty(property)!.GetValue(payload)!;
}
