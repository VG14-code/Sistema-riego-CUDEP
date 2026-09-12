using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Controllers;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Tests;

public sealed class AgronomyCropTypeTests
{
    [Fact]
    public async Task CreateRequirement_ResponseSerializesWithoutObjectCycle()
    {
        await using var db = Db();
        var type = new CropType { Code = "HORT", Name = "Hortaliza" };
        var crop = new Crop { CropType = type, Code = "TOM", Name = "Tomate" };
        db.AddRange(type, crop);
        await db.SaveChangesAsync();

        var request = new RequirementRequest(crop.Id, null, null, 40, 60, 80, 900, 24, 20, 12, 32, new TimeOnly(5, 0), new TimeOnly(9, 0), true, 30, 85);
        var result = await new AgronomyController(db).CreateRequirement(request, default);

        // Igual que en el alta de zonas, comprobar OkObjectResult no basta: la
        // entidad cruda arrastra navegaciones y el endpoint respondia 500 al
        // serializarla. La respuesta debe poder convertirse a JSON.
        var value = Assert.IsType<OkObjectResult>(result).Value;
        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("\"crop\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("targetMoisturePercent", json);
        Assert.Single(await db.CropWaterRequirements.ToListAsync());
    }

    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task CropTypeCrud_CreatesEditsAndDeactivatesWithoutAffectingExistingCrop()
    {
        await using var db = Db();
        var vegetable = new CropType { Code = "HORTALIZA", Name = "Hortaliza" };
        var tomato = new Crop { CropType = vegetable, Code = "TOMATE", Name = "Tomate" };
        tomato.Stages.Add(new PhenologicalStage { Name = "Germinación", Sequence = 1, EstimatedDays = 12 });
        tomato.Stages.Add(new PhenologicalStage { Name = "Crecimiento", Sequence = 2, EstimatedDays = 30 });
        tomato.Stages.Add(new PhenologicalStage { Name = "Floración", Sequence = 3, EstimatedDays = 45 });
        tomato.Stages.Add(new PhenologicalStage { Name = "Maduración", Sequence = 4, EstimatedDays = 30 });
        db.AddRange(vegetable, tomato);
        await db.SaveChangesAsync();
        var controller = new AgronomyController(db);

        var created = Assert.IsType<OkObjectResult>(await controller.CreateCropType(new("FRUTAL", "Frutal"), default));
        var fruit = Assert.IsType<CropType>(created.Value);
        Assert.IsType<NoContentResult>(await controller.UpdateCropType(fruit.Id, new("FRUTAL", "Frutales", false), default));

        var stored = await db.CropTypes.SingleAsync(x => x.Id == fruit.Id);
        Assert.Equal("Frutales", stored.Name);
        Assert.False(stored.IsActive);
        var unchangedTomato = await db.Crops.Include(x => x.CropType).Include(x => x.Stages).SingleAsync(x => x.Code == "TOMATE");
        Assert.Equal("Hortaliza", unchangedTomato.CropType.Name);
        Assert.Equal(4, unchangedTomato.Stages.Count);
    }

    [Fact]
    public async Task UpdateCropType_RejectsDuplicateCode()
    {
        await using var db = Db();
        var vegetable = new CropType { Code = "HORTALIZA", Name = "Hortaliza" };
        var fruit = new CropType { Code = "FRUTAL", Name = "Frutal" };
        db.AddRange(vegetable, fruit);
        await db.SaveChangesAsync();
        var controller = new AgronomyController(db);

        var result = await controller.UpdateCropType(fruit.Id, new("HORTALIZA", "Duplicado"), default);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("FRUTAL", fruit.Code);
    }
}