using System.Security.Claims;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Controllers;
using QuestPDF.Infrastructure;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Middleware;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Tests;

public sealed class Sprint6ReportingAuditTests
{
    [Fact]
    public void ReportService_GeneratesThreePdfDocumentsAndAnalyticalWorkbook()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        using var db = Db();
        var service = new Sprint6ReportService(db);
        var filter = new ReportFilter(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(1));
        var data = new ReportData(
            [new(DateTime.UtcNow, "Zona 1", "Sector 1", "Tomate", 120, 100, 20, 1.5m, "Medido")],
            [new(1, DateTime.UtcNow, "Zona 1", "Manual", "Completado", 10, "OPEN", "Confirmado", "operador@test.local", "Prueba")],
            [new(DateTime.UtcNow, "Incidente", "Bomba", "P1", "Inspección", "Pendiente", "tecnico@test.local", "Revisar")]);

        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(service.ConsumptionPdf(data, filter), 0, 4));
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(service.IrrigationPdf(data, filter), 0, 4));
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(service.MaintenancePdf(data, filter), 0, 4));
        using var workbook = new XLWorkbook(new MemoryStream(service.Excel(data, filter)));
        Assert.Equal(["Consumo", "Riegos", "Mantenimiento", "Totales"], workbook.Worksheets.Select(x => x.Name));
        Assert.Equal(120d, workbook.Worksheet("Totales").Cell("B4").GetDouble());
    }

    [Fact]
    public async Task AuditInterceptor_RecordsBeforeAfterActorIpAndCorrelation()
    {
        var http = new DefaultHttpContext { TraceIdentifier = "corr-test" };
        http.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Email, "admin@test.local")], "test"));
        var accessor = new HttpContextAccessor { HttpContext = http };
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(new AuditSaveChangesInterceptor(accessor)).Options;
        await using var db = new AppDbContext(options);
        var parameter = new GlobalParameter { Key = "TEST", Value = "1", DataType = "integer", Category = "Prueba", Description = "Prueba" };
        db.Add(parameter); await db.SaveChangesAsync();
        parameter.Value = "2"; await db.SaveChangesAsync();

        var audit = await db.AuditEntries.OrderByDescending(x => x.Id).FirstAsync();
        Assert.Equal("Actualización", audit.ActionType);
        Assert.Equal("GlobalParameter", audit.EntityType);
        Assert.Contains("\"Value\":\"1\"", audit.BeforeJson);
        Assert.Contains("\"Value\":\"2\"", audit.AfterJson);
        Assert.Equal("admin@test.local", audit.UserEmail);
        Assert.Equal("127.0.0.1", audit.IpAddress);
        Assert.Equal("corr-test", audit.CorrelationId);
    }

    [Fact]
    public async Task AuditInterceptor_RecordsTerritoryAndAgronomyChanges()
    {
        var http = new DefaultHttpContext { TraceIdentifier = "corr-territorio" };
        http.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Email, "admin@test.local")], "test"));
        var accessor = new HttpContextAccessor { HttpContext = http };
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(new AuditSaveChangesInterceptor(accessor)).Options;
        await using var db = new AppDbContext(options);

        // Crear, modificar y borrar una zona debe quedar registrado: reconstruir esto
        // desde los logs de Serilog fue lo que costo rastrear las zonas huerfanas.
        var zone = new IrrigationZone { IrrigationSectorId = Guid.NewGuid(), Code = "Z-AUD", Name = "Zona auditada", AreaHectares = 1, OperationalStatusId = Guid.NewGuid() };
        db.Add(zone); await db.SaveChangesAsync();
        zone.Name = "Zona renombrada"; await db.SaveChangesAsync();
        db.Remove(zone); await db.SaveChangesAsync();

        var zoneAudits = await db.AuditEntries.Where(x => x.EntityType == "IrrigationZone").OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(["Creación", "Actualización", "Eliminación"], zoneAudits.Select(x => x.ActionType));
        Assert.Equal("admin@test.local", zoneAudits[0].UserEmail);
        Assert.Equal("corr-territorio", zoneAudits[0].CorrelationId);
        Assert.Contains("\"Name\":\"Zona auditada\"", zoneAudits[1].BeforeJson);
        Assert.Contains("\"Name\":\"Zona renombrada\"", zoneAudits[1].AfterJson);
        Assert.Null(zoneAudits[2].AfterJson);

        var crop = new Crop { CropTypeId = Guid.NewGuid(), Code = "C-AUD", Name = "Cultivo auditado" };
        var center = new UniversityCenter { Code = "U-AUD", Name = "Centro auditado" };
        db.AddRange(crop, center); await db.SaveChangesAsync();
        Assert.Single(await db.AuditEntries.Where(x => x.EntityType == "Crop").ToListAsync());
        Assert.Single(await db.AuditEntries.Where(x => x.EntityType == "UniversityCenter").ToListAsync());
    }

    [Fact]
    public async Task CorrelationMiddleware_PreservesIncomingIdAndReturnsIt()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "incoming-123";
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context);
        Assert.Equal("incoming-123", context.TraceIdentifier);
        Assert.Equal("incoming-123", context.Response.Headers[CorrelationIdMiddleware.HeaderName]);
    }

    [Fact]
    public async Task ReportService_LoadsFilteredAnalyticalViews()
    {
        await using var db = Db();
        var status = new MasterCatalogItem { Kind = CatalogKind.OperationalStatus, Code = "OK", Name = "Operativo" };
        var center = new UniversityCenter { Code = "C", Name = "Centro" }; var farm = new Farm { UniversityCenter = center, Code = "F", Name = "Finca" };
        var block = new FarmBlock { Farm = farm, Code = "L", Name = "Lote" }; var sector = new IrrigationSector { FarmBlock = block, Code = "S", Name = "Sector" };
        var zone = new IrrigationZone { IrrigationSector = sector, OperationalStatus = status, Code = "Z", Name = "Zona", AreaHectares = 1 };
        var run = new IrrigationRun { IrrigationZone = zone, Reason = "Prueba", PlannedDurationMinutes = 10, RequestedAtUtc = DateTime.UtcNow };
        db.Add(new WaterConsumptionRecord { IrrigationRun = run, IrrigationZone = zone, VolumeLiters = 50, DurationMinutes = 10, FlowRateLitersMinute = 5, RecordedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var data = await new Sprint6ReportService(db).LoadAsync(new(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), zone.Id), default);
        Assert.Single(data.Consumption); Assert.Single(data.Irrigation); Assert.Equal("Zona", data.Consumption[0].Zone);
    }


    [Fact]
    public async Task AuditInterceptor_RecordsIoTConfigurationButIgnoresHeartbeats()
    {
        var http = new DefaultHttpContext { TraceIdentifier = "corr-iot" };
        http.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Email, "tecnico@test.local")], "test"));
        var accessor = new HttpContextAccessor { HttpContext = http };
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(new AuditSaveChangesInterceptor(accessor)).Options;
        await using var db = new AppDbContext(options);

        var sensor = new IoTSensor { Code = "SEN-AUD", Name = "Sensor auditado", SerialNumber = "SN-AUD", SensorTypeId = Guid.NewGuid(), MeasurementUnitId = Guid.NewGuid(), OperationalStatusId = Guid.NewGuid(), MinimumValue = 0, MaximumValue = 100 };
        db.Add(sensor); await db.SaveChangesAsync();
        Assert.Single(await db.AuditEntries.Where(x => x.EntityType == "IoTSensor").ToListAsync());

        // La ingestion de telemetria reescribe LastReadingUtc en cada mensaje MQTT:
        // con ~2000 lecturas diarias, auditarlo inundaria la bitacora sin aportar nada.
        sensor.LastReadingUtc = DateTime.UtcNow; await db.SaveChangesAsync();
        sensor.LastReadingUtc = DateTime.UtcNow.AddMinutes(1); await db.SaveChangesAsync();
        Assert.Single(await db.AuditEntries.Where(x => x.EntityType == "IoTSensor").ToListAsync());

        // Un cambio real de configuracion si se registra, aunque venga acompanado
        // de la marca de latido.
        sensor.MaximumValue = 80; sensor.LastReadingUtc = DateTime.UtcNow.AddMinutes(2);
        await db.SaveChangesAsync();
        var audits = await db.AuditEntries.Where(x => x.EntityType == "IoTSensor").OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(2, audits.Count);
        Assert.Equal("Actualización", audits[1].ActionType);
        Assert.Contains("\"MaximumValue\":80", audits[1].AfterJson);
        Assert.Equal("tecnico@test.local", audits[1].UserEmail);

        var calibration = new SensorCalibration { SensorId = sensor.Id, CalibratedAtUtc = DateTime.UtcNow, ReferenceValue = 50, MeasuredValue = 48, AppliedOffset = 2 };
        db.Add(calibration); await db.SaveChangesAsync();
        Assert.Single(await db.AuditEntries.Where(x => x.EntityType == "SensorCalibration").ToListAsync());
    }

    [Fact]
    public async Task AuditInterceptor_IgnoresAutomationEvaluationStamps()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(new AuditSaveChangesInterceptor(new HttpContextAccessor())).Options;
        await using var db = new AppDbContext(options);
        var rule = new IrrigationRule { Name = "Regla auditada", IrrigationZoneId = Guid.NewGuid(), MinimumMoisturePercent = 35, TargetMoisturePercent = 60 };
        db.Add(rule); await db.SaveChangesAsync();
        Assert.Single(await db.AuditEntries.Where(x => x.EntityType == "IrrigationRule").ToListAsync());

        // AutomationEngine reescribe estas marcas en cada ciclo de 10 s: eran el 86 % de la bitacora.
        for (var i = 0; i < 3; i++)
        {
            rule.LastEvaluatedAtUtc = DateTime.UtcNow.AddSeconds(i * 10); rule.LastDecision = i % 2 == 0 ? "No regar" : "Fuera de ventana"; rule.LastReason = $"Humedad {40 + i}%";
            await db.SaveChangesAsync();
        }
        Assert.Single(await db.AuditEntries.Where(x => x.EntityType == "IrrigationRule").ToListAsync());

        rule.MinimumMoisturePercent = 30; rule.LastEvaluatedAtUtc = DateTime.UtcNow.AddMinutes(1);
        await db.SaveChangesAsync();
        var audits = await db.AuditEntries.Where(x => x.EntityType == "IrrigationRule").OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(2, audits.Count);
        Assert.Contains("\"MinimumMoisturePercent\":30", audits[1].AfterJson);
    }

    [Fact]
    public async Task AuditTrail_PagedReturnsNewestFirstWithFilteredTotals()
    {
        await using var db = Db();
        for (var i = 0; i < 45; i++)
            db.AuditEntries.Add(new AuditEntry { ActionType = "Actualización", EntityType = i % 3 == 0 ? "User" : "IrrigationRule", EntityId = i.ToString(), Detail = "prueba", OccurredAtUtc = new DateTime(2026, 9, 1).AddMinutes(i), CorrelationId = "corr", Origin = "test", IpAddress = "127.0.0.1" });
        await db.SaveChangesAsync();
        var controller = new AuditTrailController(db);

        var second = Assert.IsType<AuditPage>(Assert.IsType<OkObjectResult>(await controller.Paged(null, null, null, null, null, 2, 20)).Value);
        Assert.Equal((45, 3, 2, 20), (second.Total, second.PageCount, second.Page, second.Items.Count));
        Assert.Equal("24", second.Items[0].EntityId);

        var beyond = Assert.IsType<AuditPage>(Assert.IsType<OkObjectResult>(await controller.Paged(null, null, null, null, null, 99, 20)).Value);
        Assert.Equal((3, 5), (beyond.Page, beyond.Items.Count));

        var users = Assert.IsType<AuditPage>(Assert.IsType<OkObjectResult>(await controller.Paged(null, null, null, null, "User", 1, 20)).Value);
        Assert.Equal((15, 1), (users.Total, users.PageCount));
    }

    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
