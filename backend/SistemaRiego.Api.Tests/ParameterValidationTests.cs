using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Controllers;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Tests;

public sealed class ParameterValidationTests
{
    [Fact]
    public async Task Upsert_RejectsValueThatDoesNotMatchSelectedType()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var controller = new GlobalParametersController(db)
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

        var response = await controller.Upsert("IRRIGATION_MINUTES", new ParameterRequest("texto", "integer", "Riego", "Duración", true), default);

        Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Empty(db.GlobalParameters);
    }
}
