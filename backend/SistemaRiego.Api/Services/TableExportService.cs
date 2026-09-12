using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SistemaRiego.Api.Services;

/// <summary>
/// Exporta a Excel y PDF cualquier tabla que el usuario tenga en pantalla. Los
/// reportes del Sprint 6 generan su propio contenido desde la base; aqui el
/// contenido llega ya preparado por la interfaz, de modo que lo exportado
/// coincide exactamente con lo que se ve, filtros incluidos.
/// </summary>
public sealed class TableExportService
{
    public const int MaxRows = 5000;
    public const int MaxColumns = 30;
    private const int MaxCellLength = 500;

    public byte[] Excel(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SheetName(title));

        sheet.Cell(1, 1).Value = title;
        sheet.Range(1, 1, 1, Math.Max(1, headers.Count)).Merge().Style
            .Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(XLColor.White)
            .Fill.SetBackgroundColor(XLColor.FromHtml("#0F4C5C"))
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        sheet.Row(1).Height = 24;

        sheet.Cell(2, 1).Value = $"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}";
        sheet.Range(2, 1, 2, Math.Max(1, headers.Count)).Merge().Style.Font.SetItalic().Font.SetFontColor(XLColor.Gray);

        for (var column = 0; column < headers.Count; column++)
        {
            var cell = sheet.Cell(4, column + 1);
            cell.Value = Clip(headers[column]);
            cell.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#E3EFF2"));
        }

        for (var row = 0; row < rows.Count; row++)
            for (var column = 0; column < rows[row].Count && column < headers.Count; column++)
                sheet.Cell(row + 5, column + 1).Value = Clip(rows[row][column]);

        if (headers.Count > 0)
        {
            sheet.Range(4, 1, 4 + rows.Count, headers.Count).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetInsideBorder(XLBorderStyleValues.Hair);
            sheet.SheetView.FreezeRows(4);
            sheet.Columns().AdjustToContents();
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] Pdf(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        // Con muchas columnas la orientacion vertical vuelve la tabla ilegible.
        var landscape = headers.Count > 5;
        return Document.Create(document => document.Page(page =>
        {
            page.Size(landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
            page.Margin(28);
            page.DefaultTextStyle(style => style.FontSize(9).FontFamily(Fonts.Calibri));

            page.Header().Column(column =>
            {
                column.Item().Text(title).FontSize(16).Bold().FontColor("#0F4C5C");
                column.Item().Text($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm} · {rows.Count} registro(s)").FontSize(9).FontColor(Colors.Grey.Darken1);
                column.Item().PaddingTop(6).LineHorizontal(1).LineColor("#0F4C5C");
            });

            page.Content().PaddingVertical(10).Table(table =>
            {
                table.ColumnsDefinition(definition => { for (var i = 0; i < headers.Count; i++) definition.RelativeColumn(); });
                table.Header(header =>
                {
                    foreach (var heading in headers)
                        header.Cell().Background("#E3EFF2").Padding(5).Text(Clip(heading)).Bold().FontColor("#0F4C5C");
                });
                foreach (var row in rows)
                    for (var column = 0; column < headers.Count; column++)
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5)
                            .Text(column < row.Count ? Clip(row[column]) : string.Empty);
            });

            page.Footer().AlignRight().Text(text =>
            {
                text.Span("Sistema de Riego CUDEP · página ").FontSize(8).FontColor(Colors.Grey.Darken1);
                text.CurrentPageNumber().FontSize(8);
                text.Span(" de ").FontSize(8);
                text.TotalPages().FontSize(8);
            });
        })).GeneratePdf();
    }

    private static string Clip(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Length <= MaxCellLength ? value : value[..MaxCellLength];

    // Excel rechaza :\/?*[] y limita el nombre de la hoja a 31 caracteres.
    private static string SheetName(string title)
    {
        var clean = new string(title.Where(c => !":\\/?*[]".Contains(c)).ToArray()).Trim();
        if (clean.Length == 0) clean = "Datos";
        return clean.Length <= 31 ? clean : clean[..31];
    }
}
