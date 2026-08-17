using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Controllers;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Tests;

public sealed class Week2ControllerTests
{
    [Fact]
    public async Task Catalog_CreateAndList_ReturnsStoredItem()
    {
        var db = CreateDb(); var controller = WithUser(new MasterDataController(db));
        var created = await controller.Create("SensorType", new("PH_SENSOR", "Sensor de pH", "Mide acidez", "pH", true), default);
        Assert.IsType<CreatedAtActionResult>(created.Result);
        var listed = await controller.Get("SensorType", default);
        var ok = Assert.IsType<OkObjectResult>(listed.Result);
        var items = Assert.IsAssignableFrom<IEnumerable<CatalogItemResponse>>(ok.Value);
        Assert.Contains(items, x => x.Code == "PH_SENSOR" && x.Kind == CatalogKind.SensorType);
    }

    [Fact]
    public async Task Catalog_CreateDuplicateCode_ReturnsConflict()
    {
        var db = CreateDb(); var controller = WithUser(new MasterDataController(db)); var request = new CatalogItemRequest("NODE", "Nodo", null, null, true);
        await controller.Create("DeviceType", request, default);
        var duplicate = await controller.Create("DeviceType", request, default);
        Assert.IsType<ConflictObjectResult>(duplicate.Result);
    }

    [Fact]
    public async Task GlobalParameter_Upsert_PersistsAndAuditsChange()
    {
        var db = CreateDb(); var controller = WithUser(new GlobalParametersController(db));
        var result = await controller.Upsert("READING_FREQUENCY", new("30", "integer", "Telemetría", "Frecuencia de lectura", true), default);
        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("30", (await db.GlobalParameters.SingleAsync()).Value);
        Assert.Contains(await db.AccessAudits.ToListAsync(), x => x.EventType == "GLOBAL_PARAMETER_UPDATED");
    }

    [Fact]
    public async Task Audit_Get_ReturnsNewestEntriesFirst()
    {
        var db = CreateDb();
        db.AccessAudits.AddRange(
            new AccessAudit { EventType = "LOGIN_FAILED", Detail = "Primero", OccurredAtUtc = DateTime.UtcNow.AddMinutes(-2) },
            new AccessAudit { EventType = "LOGIN_SUCCESS", Detail = "Último", OccurredAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var result = await new AuditController(db).Get(null, 10, default);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IEnumerable<AuditResponse>>(ok.Value).ToList();
        Assert.Equal("LOGIN_SUCCESS", items[0].EventType);
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static T WithUser<T>(T controller) where T : ControllerBase
    {
        var id = Guid.NewGuid();
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, RoleNames.Administrator)], "tests")) } };
        return controller;
    }
}
