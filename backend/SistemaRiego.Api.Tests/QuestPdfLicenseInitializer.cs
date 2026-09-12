using System.Runtime.CompilerServices;
using QuestPDF.Infrastructure;

namespace SistemaRiego.Api.Tests;

internal static class QuestPdfLicenseInitializer
{
    // La aplicacion declara la licencia en Program.cs, que las pruebas no ejecutan;
    // sin esto QuestPDF lanza al generar el primer documento.
    [ModuleInitializer]
    internal static void Initialize() => QuestPDF.Settings.License = LicenseType.Community;
}
