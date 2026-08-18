using System.Security.Claims;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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

    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
