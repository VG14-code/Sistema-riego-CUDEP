using System.Security.Claims;

namespace SistemaRiego.Api.Middleware;

public sealed class RequiredPasswordChangeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var required = context.User.Identity?.IsAuthenticated == true && context.User.FindFirstValue("pwd_change_required") == "true";
        var allowed = context.Request.Path.StartsWithSegments("/api/auth/change-required-password") || context.Request.Path.StartsWithSegments("/api/auth/logout");
        if (required && !allowed)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { message = "Debes cambiar la contraseña temporal antes de continuar." });
            return;
        }
        await next(context);
    }
}