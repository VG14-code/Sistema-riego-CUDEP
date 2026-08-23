using Microsoft.AspNetCore.Authorization;

namespace SistemaRiego.Api.Services;

public sealed class PermissionRequirement(string code) : IAuthorizationRequirement
{
    public string Code { get; } = code;
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasClaim("perm", requirement.Code)) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
