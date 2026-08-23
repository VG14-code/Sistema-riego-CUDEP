using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Tests;

public sealed class PermissionAuthorizationHandlerTests
{
    [Fact]
    public async Task Succeeds_WhenUserHasMatchingPermClaim()
    {
        var requirement = new PermissionRequirement("dispositivos.catalogos.eliminar");
        var identity = new ClaimsIdentity([new Claim("perm", "dispositivos.catalogos.eliminar")], "tests");
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(identity), null);

        await new PermissionAuthorizationHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Fails_WhenUserLacksPermClaim()
    {
        var requirement = new PermissionRequirement("dispositivos.catalogos.eliminar");
        var identity = new ClaimsIdentity([new Claim("perm", "dispositivos.catalogos.leer")], "tests");
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(identity), null);

        await new PermissionAuthorizationHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
