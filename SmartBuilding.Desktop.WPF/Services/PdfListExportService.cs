using System.Globalization;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace SmartBuilding.Desktop.WPF.Services;

/// <summary>Export PDF tabulaire unifié pour toutes les listes modules.</summary>
public static class PdfListExportService
{
    public static string Export(
        string moduleFolder,
        string filePrefix,
        string documentTitle,
        string? documentSubtitle,
        IReadOnlyList<string> headers,
        IEnumerable<string[]> rows,
        IReadOnlyList<(string Label, string Value)>? kpis = null,
        bool landscape = true)
    {
        PdfThemeHelper.EnsureLicense();
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var list = rows.ToList();
        var green = PdfThemeHelper.ResolveHeaderColor();

        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SBMS", moduleFolder);
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder,
            $"{PdfThemeHelper.SanitizeFileName(filePrefix)}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                PdfThemeHelper.ConfigurePage(page, landscape);

                page.Content().Column(root =>
                {
                    root.Item().Element(c => PdfThemeHelper.DocumentHeader(c, new PdfThemeHelper.PdfHeaderOptions(
                        DocumentTitle: documentTitle,
                        DocumentSubtitle: documentSubtitle ?? "Export officiel SBMS",
                        DepartmentLine: PdfThemeHelper.ResolveCompanySubtitle(),
                        Meta:
                        [
                            ("Date", DateTime.Now.ToString("dd MMMM yyyy", culture)),
                            ("Heure", DateTime.Now.ToString("HH:mm", culture)),
                            ("Lignes", list.Count.ToString("N0", culture))
                        ])));

                    if (kpis is { Count: > 0 })
                        root.Item().PaddingTop(12).Element(c => PdfThemeHelper.KpiRow(c, kpis));

                    root.Item().PaddingTop(12).Element(c =>
                        PdfThemeHelper.DataTable(c, headers, list));

                    root.Item().PaddingTop(14).Element(c =>
                        PdfThemeHelper.DocumentFooter(c,
                            $"{list.Count} enregistrement(s) — document PDF généré par {PdfThemeHelper.ResolveCompanyName()}"));
                });
            });
        }).GeneratePdf(path);

        return path;
    }
}
