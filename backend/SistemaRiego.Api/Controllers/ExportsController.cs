using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Controllers;

public sealed record TableExportRequest(
    [Required, MaxLength(120)] string Title,
    [Required] IReadOnlyList<string> Headers,
    [Required] IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>
/// Exporta a Excel o PDF la tabla que el usuario tiene en pantalla, con sus
/// filtros ya aplicados. No consulta la base: solo da formato a datos que la
/// sesion ya recibio, de modo que lo exportado coincide con lo que se ve.
/// </summary>
[ApiController, Route("api/exports"), Authorize]
public sealed class ExportsController(TableExportService exports) : ControllerBase
{
    [HttpPost("xlsx")]
    public IActionResult Excel(TableExportRequest request)
    {
        var error = Validate(request);
        if (error is not null) return BadRequest(new { message = error });
        return File(exports.Excel(request.Title, request.Headers, request.Rows),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", FileName(request.Title, "xlsx"));
    }

    [HttpPost("pdf")]
    public IActionResult Pdf(TableExportRequest request)
    {
        var error = Validate(request);
        if (error is not null) return BadRequest(new { message = error });
        return File(exports.Pdf(request.Title, request.Headers, request.Rows), "application/pdf", FileName(request.Title, "pdf"));
    }

    private static string? Validate(TableExportRequest request)
    {
        if (request.Headers.Count == 0) return "La exportación necesita al menos una columna.";
        if (request.Headers.Count > TableExportService.MaxColumns) return $"La exportación admite hasta {TableExportService.MaxColumns} columnas.";
        if (request.Rows.Count > TableExportService.MaxRows) return $"La exportación admite hasta {TableExportService.MaxRows} filas; aplica un filtro para reducir el resultado.";
        return null;
    }

    private static string FileName(string title, string extension)
    {
        var slug = new string(title.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        slug = slug.Trim('-');
        if (slug.Length == 0) slug = "export";
        return $"{slug}-{DateTime.Now:yyyyMMdd-HHmm}.{extension}";
    }
}
