using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using SistemaRiego.Api.Controllers;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Tests;

public sealed class TableExportTests
{
    private static readonly string[] Headers = ["Código", "Nombre", "Estado"];
    private static readonly IReadOnlyList<IReadOnlyList<string>> Rows =
    [
        ["CULT-0001", "Papa", "Activo"],
        ["CULT-0002", "Camote", "Inactivo"],
    ];

    [Fact]
    public void Excel_WritesTitleHeadersAndRows()
    {
        var bytes = new TableExportService().Excel("Cultivos", Headers, Rows);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet(1);
        Assert.Equal("Cultivos", sheet.Name);
        Assert.Equal("Cultivos", sheet.Cell(1, 1).GetString());
        // Fila 4 es el encabezado; los datos empiezan en la 5.
        Assert.Equal("Código", sheet.Cell(4, 1).GetString());
        Assert.Equal("CULT-0001", sheet.Cell(5, 1).GetString());
        Assert.Equal("Inactivo", sheet.Cell(6, 3).GetString());
    }

    [Fact]
    public void Excel_SanitisesTheSheetName()
    {
        // Excel rechaza estos caracteres y corta a 31: sin limpiarlos el archivo
        // sale corrupto y no abre.
        var bytes = new TableExportService().Excel("Riegos: zona A1 / 2026 [total]", Headers, Rows);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheetName = workbook.Worksheet(1).Name;
        Assert.All(new[] { ":", "/", "[", "]" }, c => Assert.DoesNotContain(c, sheetName));
        Assert.True(sheetName.Length <= 31);
    }

    [Fact]
    public void Pdf_ProducesANonEmptyDocument()
    {
        var bytes = new TableExportService().Pdf("Cultivos", Headers, Rows);

        Assert.True(bytes.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void Export_HandlesRaggedRowsAndEmptyData()
    {
        var service = new TableExportService();
        // Una fila con menos celdas que encabezados no debe romper la exportacion.
        IReadOnlyList<IReadOnlyList<string>> ragged = [["solo-una"]];
        Assert.True(service.Excel("Parcial", Headers, ragged).Length > 0);
        Assert.True(service.Pdf("Parcial", Headers, ragged).Length > 0);
        Assert.True(service.Excel("Vacío", Headers, []).Length > 0);
    }

    [Fact]
    public void Controller_RejectsTooManyRowsAndColumns()
    {
        var controller = new ExportsController(new TableExportService());
        var manyRows = Enumerable.Range(0, TableExportService.MaxRows + 1).Select(_ => (IReadOnlyList<string>)["x"]).ToList();
        var manyColumns = Enumerable.Range(0, TableExportService.MaxColumns + 1).Select(x => $"c{x}").ToList();

        Assert.IsType<BadRequestObjectResult>(controller.Excel(new TableExportRequest("Datos", ["c"], manyRows)));
        Assert.IsType<BadRequestObjectResult>(controller.Pdf(new TableExportRequest("Datos", manyColumns, Rows)));
        Assert.IsType<BadRequestObjectResult>(controller.Excel(new TableExportRequest("Datos", [], Rows)));
    }

    [Fact]
    public void Controller_NamesTheFileAfterTheTable()
    {
        var controller = new ExportsController(new TableExportService());

        var file = Assert.IsType<FileContentResult>(controller.Excel(new TableExportRequest("Sesiones activas", Headers, Rows)));

        Assert.StartsWith("sesiones-activas-", file.FileDownloadName);
        Assert.EndsWith(".xlsx", file.FileDownloadName);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
    }
}
