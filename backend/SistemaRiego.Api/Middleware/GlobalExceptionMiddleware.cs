using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SistemaRiego.Api.Middleware;

public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Solicitud cancelada por el cliente: {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        catch (Exception exception)
        {
            await WriteProblemAsync(context, exception);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception exception)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var status = exception switch
        {
            DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
            DbUpdateException => StatusCodes.Status409Conflict,
            ArgumentException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        logger.LogError(exception, "Error no controlado {TraceId} en {Method} {Path}", traceId, context.Request.Method, context.Request.Path);
        if (context.Response.HasStarted) throw exception;

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Status = status,
            Title = status == 500 ? "Error interno del servidor" : "No fue posible completar la operación",
            Detail = status == 500
                ? "Ocurrió un error inesperado. Usa el identificador de seguimiento para consultar el registro."
                : "La solicitud entra en conflicto con el estado actual de los datos.",
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = traceId;
        await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
    }
}
