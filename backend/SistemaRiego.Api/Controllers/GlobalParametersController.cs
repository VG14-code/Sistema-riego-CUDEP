using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/settings"), Authorize(Policy = PermissionPolicies.SettingsRead)]
public sealed class GlobalParametersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ParameterResponse>>> Get(CancellationToken ct)
    {
        var parameters = await db.GlobalParameters.AsNoTracking().OrderBy(x => x.Category).ThenBy(x => x.Key).ToListAsync(ct);
        return Ok(parameters.Select(ToResponse));
    }

    [HttpPut("{key}"), Authorize(Policy = PermissionPolicies.SettingsManage)]
    public async Task<ActionResult<ParameterResponse>> Upsert(string key, ParameterRequest request, CancellationToken ct)
    {
        var normalized = key.Trim().ToUpperInvariant();
        var dataType = request.DataType.Trim().ToLowerInvariant();
        if (!ValidValue(dataType, request.Value.Trim())) return BadRequest(new { message = "El valor no corresponde al tipo seleccionado." });
        var parameter = await db.GlobalParameters.SingleOrDefaultAsync(x => x.Key == normalized, ct);
        if (parameter is null)
        {
            parameter = new GlobalParameter { Key = normalized, Value = request.Value.Trim(), DataType = dataType, Category = request.Category.Trim(), Description = request.Description.Trim(), IsEditable = request.IsEditable, UpdatedByUserId = CurrentUserId() };
            db.GlobalParameters.Add(parameter);
        }
        else
        {
            if (!parameter.IsEditable) return BadRequest(new { message = "Este parámetro no es editable." });
            parameter.Value = request.Value.Trim(); parameter.DataType = dataType; parameter.Category = request.Category.Trim(); parameter.Description = request.Description.Trim(); parameter.IsEditable = request.IsEditable; parameter.UpdatedAtUtc = DateTime.UtcNow; parameter.UpdatedByUserId = CurrentUserId();
        }
        db.AccessAudits.Add(new AccessAudit { UserId = CurrentUserId(), EventType = "GLOBAL_PARAMETER_UPDATED", Detail = normalized });
        await db.SaveChangesAsync(ct);
        return Ok(ToResponse(parameter));
    }

    private Guid? CurrentUserId() => Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
    private static bool ValidValue(string dataType, string value) => dataType switch
    {
        "integer" => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
        "decimal" => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
        "boolean" => bool.TryParse(value, out _),
        "time" => TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
        "text" => !string.IsNullOrWhiteSpace(value),
        _ => false
    };
    private static ParameterResponse ToResponse(GlobalParameter x) => new(x.Id, x.Key, x.Value, x.DataType, x.Category, x.Description, x.IsEditable, x.UpdatedAtUtc);
}
