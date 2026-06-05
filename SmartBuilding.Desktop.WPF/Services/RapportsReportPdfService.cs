using System.Globalization;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartBuilding.Desktop.WPF.Helpers;
using SmartBuilding.Desktop.WPF.Models;
using BuildingInfoDefaults = SmartBuilding.Domain.Entities.Building.BuildingInfoDefaults;

namespace SmartBuilding.Desktop.WPF.Services;

public class RapportsReportPdfService
{
    private string _navy = "#1B365D";
    private string _accent = "#4cc26b";

    static RapportsReportPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string ExportSectionReport(
        string sectionTitle,
        IReadOnlyList<string> kpiLabels,
        IReadOnlyList<string> kpiValues,
        IReadOnlyList<string> headers,
        IEnumerable<string[]> rows)
    {
        _navy = AppConfigurationService.Instance?.Current.PdfHeaderHex ?? "#3D6B52";
        _accent = AppConfigurationService.Instance?.Current.PdfAccentHex ?? "#4cc26b";
        var company = AppConfigurationService.Instance?.Current.CompanyName ?? BuildingInfoDefaults.CompanyName;
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var list = rows.ToList();

        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SBMS", "Rapports");
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"rapport_{Sanitize(sectionTitle)}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(8).FontColor(_navy));

                page.Content().Column(root =>
                {
                    root.Item().Row(r =>
                    {
                        r.RelativeItem().Column(col =>
                        {
                            col.Item().Text(company).Bold().FontSize(14).FontColor(_navy);
                            col.Item().Text(sectionTitle).FontSize(11).FontColor(_accent);
                        });
                        r.ConstantItem(120).AlignRight().Text($"Généré le {DateTime.Now.ToString("dd/MM/yyyy HH:mm", culture)}")
                            .FontSize(8).FontColor("#64748B");
                    });

                    if (kpiLabels.Count > 0)
                    {
                        root.Item().PaddingTop(10).Row(row =>
                        {
                            for (var i = 0; i < kpiLabels.Count; i++)
                            {
                                if (i > 0)
                                    row.ConstantItem(8);
                                var label = kpiLabels[i];
                                var value = i < kpiValues.Count ? kpiValues[i] : "—";
                                row.RelativeItem().Border(1).BorderColor("#E2E8F0").Background("#F8FAFC")
                                    .Padding(8).Column(c =>
                                    {
                                        c.Item().Text(label).FontSize(7).FontColor("#64748B");
                                        c.Item().Text(value).Bold().FontSize(10);
                                    });
                            }
                        });
                    }

                    root.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            for (var i = 0; i < headers.Count; i++)
                                c.RelativeColumn();
                        });

                        table.Header(h =>
                        {
                            foreach (var header in headers)
                                h.Cell().Background(_navy).Padding(4)
                                    .Text(header).FontColor(Colors.White).Bold().FontSize(7);
                        });

                        foreach (var data in list)
                        {
                            foreach (var cell in data)
                            {
                                table.Cell().BorderBottom(1).BorderColor("#E2E8F0").Padding(4)
                                    .Text(cell ?? "—").FontSize(7);
                            }
                        }
                    });

                    root.Item().PaddingTop(8).AlignRight()
                        .Text($"{list.Count} enregistrement(s)").FontSize(7).FontColor("#94A3B8");
                });
            });
        }).GeneratePdf(path);

        return path;
    }

    public string ExportFinancierReport(RapportFinancierSummary summary, RapportsPageData data)
    {
        _navy = AppConfigurationService.Instance?.Current.PdfHeaderHex ?? "#3D6B52";
        _accent = AppConfigurationService.Instance?.Current.PdfAccentHex ?? "#4cc26b";
        var company = AppConfigurationService.Instance?.Current.CompanyName ?? BuildingInfoDefaults.CompanyName;

        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SBMS", "Rapports");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"rapport_financier_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(_navy));

                page.Content().Column(root =>
                {
                    root.Item().Text(company).Bold().FontSize(16);
                    root.Item().Text("Rapport Financier Global").FontSize(12).FontColor(_accent);
                    root.Item().PaddingTop(16).Text("REVENUS").Bold().FontSize(11);
                    root.Item().PaddingTop(4).Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); });
                        void Row(string label, string value)
                        {
                            t.Cell().Padding(4).Text(label);
                            t.Cell().Padding(4).AlignRight().Text(value).Bold();
                        }
                        Row("Loyers encaissés", summary.LoyersEncaissesDisplay);
                        Row("Garanties", summary.GarantiesDisplay);
                        Row("Services", summary.ServicesDisplay);
                        Row("Revenus divers", summary.RevenusDiversDisplay);
                    });

                    root.Item().PaddingTop(12).Text("DÉPENSES").Bold().FontSize(11);
                    root.Item().PaddingTop(4).Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); });
                        void Row(string label, string value)
                        {
                            t.Cell().Padding(4).Text(label);
                            t.Cell().Padding(4).AlignRight().Text(value).Bold();
                        }
                        Row("Salaires", summary.SalairesDisplay);
                        Row("Consommations", summary.ConsommationsDisplay);
                        Row("Maintenance", summary.MaintenanceDisplay);
                        Row("Fournisseurs", summary.FournisseursDisplay);
                        Row("Charges diverses", summary.ChargesDiversesDisplay);
                    });

                    root.Item().PaddingTop(12).Text("RÉSULTAT").Bold().FontSize(11);
                    root.Item().PaddingTop(4).Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); });
                        void Row(string label, string value, bool highlight = false)
                        {
                            var labelCell = t.Cell().Padding(4).Text(label);
                            if (highlight) labelCell.Bold();
                            t.Cell().Padding(4).AlignRight().Text(value).Bold();
                        }
                        Row("Total entrées", summary.TotalEntreesDisplay);
                        Row("Total sorties", summary.TotalSortiesDisplay);
                        Row("Solde actuel", summary.SoldeActuelDisplay, true);
                        Row("Bénéfice", summary.BeneficeDisplay);
                        Row("Perte", summary.PerteDisplay);
                    });
                });
            });
        }).GeneratePdf(path);

        return path;
    }

    private static string Sanitize(string name) =>
        string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
}
