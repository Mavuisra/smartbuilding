using System.Globalization;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public class RapportsReportPdfService
{
    static RapportsReportPdfService() => PdfThemeHelper.EnsureLicense();

    public string ExportSectionReport(
        string sectionTitle,
        IReadOnlyList<string> kpiLabels,
        IReadOnlyList<string> kpiValues,
        IReadOnlyList<string> headers,
        IEnumerable<string[]> rows)
    {
        var list = rows.ToList();
        var kpis = new List<(string, string)>();
        for (var i = 0; i < kpiLabels.Count; i++)
            kpis.Add((kpiLabels[i], i < kpiValues.Count ? kpiValues[i] : "—"));

        return PdfListExportService.Export(
            moduleFolder: "Rapports",
            filePrefix: $"rapport_{PdfThemeHelper.SanitizeFileName(sectionTitle)}",
            documentTitle: sectionTitle,
            documentSubtitle: "Rapport analytique SBMS",
            headers: headers,
            rows: list,
            kpis: kpis,
            landscape: true);
    }

    public string ExportFinancierReport(RapportFinancierSummary summary, RapportsPageData data)
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SBMS", "Rapports");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"rapport_financier_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        var revenusRows = new[]
        {
            new[] { "Loyers encaissés", summary.LoyersEncaissesDisplay },
            new[] { "Garanties", summary.GarantiesDisplay },
            new[] { "Services", summary.ServicesDisplay },
            new[] { "Revenus divers", summary.RevenusDiversDisplay }
        };
        var depensesRows = new[]
        {
            new[] { "Salaires", summary.SalairesDisplay },
            new[] { "Consommations", summary.ConsommationsDisplay },
            new[] { "Maintenance", summary.MaintenanceDisplay },
            new[] { "Fournisseurs", summary.FournisseursDisplay },
            new[] { "Charges diverses", summary.ChargesDiversesDisplay }
        };
        var resultatRows = new[]
        {
            new[] { "Total entrées", summary.TotalEntreesDisplay },
            new[] { "Total sorties", summary.TotalSortiesDisplay },
            new[] { "Solde actuel", summary.SoldeActuelDisplay },
            new[] { "Bénéfice", summary.BeneficeDisplay },
            new[] { "Perte", summary.PerteDisplay }
        };

        Document.Create(container =>
        {
            container.Page(page =>
            {
                PdfThemeHelper.ConfigurePage(page);

                page.Content().Column(root =>
                {
                    root.Item().Element(c => PdfThemeHelper.DocumentHeader(c, new PdfThemeHelper.PdfHeaderOptions(
                        DocumentTitle: "Rapport financier global",
                        DocumentSubtitle: "Synthèse revenus, dépenses et résultat",
                        Meta:
                        [
                            ("Date", DateTime.Now.ToString("dd MMMM yyyy", culture)),
                            ("Période", "Exercice en cours")
                        ])));

                    root.Item().PaddingTop(14).Element(c => PdfThemeHelper.SectionBox(c, "Revenus", col =>
                    {
                        col.Item().Element(t => PdfThemeHelper.DataTable(t,
                            new List<string> { "Poste", "Montant" },
                            revenusRows));
                    }));

                    root.Item().PaddingTop(10).Element(c => PdfThemeHelper.SectionBox(c, "Dépenses", col =>
                    {
                        col.Item().Element(t => PdfThemeHelper.DataTable(t,
                            new List<string> { "Poste", "Montant" },
                            depensesRows));
                    }));

                    root.Item().PaddingTop(10).Element(c => PdfThemeHelper.SectionBox(c, "Résultat", col =>
                    {
                        col.Item().Element(t => PdfThemeHelper.DataTable(t,
                            new List<string> { "Indicateur", "Montant" },
                            resultatRows));
                    }));

                    root.Item().PaddingTop(14).Element(c =>
                        PdfThemeHelper.DocumentFooter(c, "Rapport PDF - " + PdfThemeHelper.ResolveCompanyName()));
                });
            });
        }).GeneratePdf(path);

        return path;
    }
}
