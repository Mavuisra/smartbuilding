using System.Globalization;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public class FinancesReportPdfService
{
    static FinancesReportPdfService() => PdfThemeHelper.EnsureLicense();

    public string ExportTransactionsReport(FinancePageData summary, IEnumerable<FinanceTransactionItem> items, string title)
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var list = items.ToList();

        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SBMS", "Finances");
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"finances_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                PdfThemeHelper.ConfigurePage(page, landscape: true);

                page.Content().Column(root =>
                {
                    root.Item().Element(c => PdfThemeHelper.DocumentHeader(c, new PdfThemeHelper.PdfHeaderOptions(
                        DocumentTitle: title,
                        DocumentSubtitle: "Gestion financière et trésorerie",
                        BadgeText: $"{list.Count} transaction(s)",
                        Meta:
                        [
                            ("Date", DateTime.Now.ToString("dd MMMM yyyy", culture)),
                            ("Heure", DateTime.Now.ToString("HH:mm", culture)),
                            ("Devise", MoneyFormatter.CurrencyCode)
                        ])));

                    root.Item().PaddingTop(12).Element(c => PdfThemeHelper.KpiRow(c,
                    [
                        ("Loyers collectés", MoneyFormatter.Format(summary.RentCollected)),
                        ("Dépenses (mois)", MoneyFormatter.Format(summary.MonthlyExpenses)),
                        ("Bénéfice net", MoneyFormatter.Format(summary.NetProfit)),
                        ("Trésorerie", MoneyFormatter.Format(summary.TreasuryBalance))
                    ]));

                    root.Item().PaddingTop(12).Element(c => PdfThemeHelper.SectionBox(c, "Journal des transactions", inner =>
                    {
                        inner.Item().Element(t => PdfThemeHelper.DataTable(t,
                            ["Référence", "Date", "Type", "Catégorie", "Description", "Source", "Montant", "Statut"],
                            list.Select(x => new[]
                            {
                                x.Reference, x.DateDisplay, x.TypeLabel, x.Category, x.Description,
                                x.Source, x.AmountDisplay, x.StatusLabel
                            })));
                    }));

                    root.Item().PaddingTop(12).Element(c =>
                        PdfThemeHelper.DocumentFooter(c, $"Document PDF officiel — {PdfThemeHelper.ResolveCompanyName()}"));
                });
            });
        }).GeneratePdf(path);

        return path;
    }
}
