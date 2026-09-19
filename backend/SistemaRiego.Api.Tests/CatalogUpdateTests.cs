using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Controllers;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Tests;

public sealed class CatalogUpdateTests
{
    [Fact]
    public async Task Update_ChangesExistingCatalogItem()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var item = new MasterCatalogItem { Kind = CatalogKind.SensorType, Code = "TEMP", Name = "Temperatura", Description = "Original", Symbol = "C", IsActive = true };
        db.MasterCatalogItems.Add(item);
        await db.SaveChangesAsync();
        var controller = new MasterDataController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([
                        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                        new Claim(ClaimTypes.Role, RoleNames.Administrator)
                    ], "tests"))
                }
            }
        };

        var response = await controller.Update("SensorType", item.Id, new CatalogItemRequest("TEMP_AMBIENTE", "Temperatura ambiente", "Descripción actualizada", "°C", false), default);

        Assert.IsType<OkObjectResult>(response.Result);
        var stored = await db.MasterCatalogItems.SingleAsync();
        Assert.Equal("TEMP_AMBIENTE", stored.Code);
        Assert.Equal("Temperatura ambiente", stored.Name);
        Assert.Equal("Descripción actualizada", stored.Description);
        Assert.Equal("°C", stored.Symbol);
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task Convert_UsesFactorsForCompatibleUnits()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var liter = new MasterCatalogItem { Kind = CatalogKind.MeasurementUnit, Code = "L", Name = "Litro", Symbol = "L", BaseUnitCode = "LITER", ConversionFactorToBase = 1m };
        var milliliter = new MasterCatalogItem { Kind = CatalogKind.MeasurementUnit, Code = "ML", Name = "Mililitro", Symbol = "mL", BaseUnitCode = "LITER", ConversionFactorToBase = 0.001m };
        db.AddRange(liter, milliliter); await db.SaveChangesAsync();

        var response = await new MasterDataController(db).Convert(new UnitConversionRequest(liter.Id, milliliter.Id, 2m), default);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var value = Assert.IsType<UnitConversionResponse>(ok.Value);
        Assert.Equal(2000m, value.ConvertedValue);
    }}
