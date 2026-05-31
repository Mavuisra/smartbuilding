using System.Globalization;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartBuilding.Desktop.WPF.Helpers;
using SmartBuilding.Desktop.WPF.Models;
using BuildingInfoDefaults = SmartBuilding.Domain.Entities.Building.BuildingInfoDefaults;

namespace SmartBuilding.Desktop.WPF.Services;

public class FinancesReportPdfService
{
    private const string Border = PdfThemeHelper.Border;
    private const string NavyLight = PdfThemeHelper.NavyLight;
    private const string GrayBg = PdfThemeHelper.GrayBg;
    private const string GelGreen = "#4cc26b";
    private string _navy = "#1B365D";
    private string _accent = GelGreen;

    static FinancesReportPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string ExportTransactionsReport(FinancePageData summary, IEnumerable<FinanceTransactionItem> items, string title)
    {
        _navy = AppConfigurationService.Instance?.Current.PdfHeaderHex ?? "#3D6B52";
        _accent = AppConfigurationService.Instance?.Current.PdfAccentHex ?? GelGreen;
        var company = AppConfigurationService.Instance?.Current.CompanyName ?? BuildingInfoDefaults.CompanyName;
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
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(8).FontColor(_navy));

                page.Content().Column(root =>
                {
                    root.Item().Element(c => DrawHeader(c, title, company, culture, list.Count));

                    root.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().Element(c => KpiBox(c, "Loyers collectés (mois)", MoneyFormatter.Format(summary.RentCollected)));
                        row.ConstantItem(8);
                        row.RelativeItem().Element(c => KpiBox(c, "Dépenses (mois)", MoneyFormatter.Format(summary.MonthlyExpenses)));
                        row.ConstantItem(8);
                        row.RelativeItem().Element(c => KpiBox(c, "Bénéfice net", MoneyFormatter.Format(summary.NetProfit)));
                        row.ConstantItem(8);
                        row.RelativeItem().Element(c => KpiBox(c, "Trésorerie", MoneyFormatter.Format(summary.TreasuryBalance)));
                    });

                    root.Item().PaddingTop(12).Element(c => SectionBox(c, "JOURNAL DES TRANSACTIONS", col =>
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(1.1f);
                                c.RelativeColumn(0.9f);
                                c.RelativeColumn(0.8f);
                                c.RelativeColumn(1f);
                                c.RelativeColumn(1.6f);
                                c.RelativeColumn(1f);
                                c.RelativeColumn(0.9f);
                                c.RelativeColumn(0.8f);
                            });

                            void Header(string text) =>
                                table.Cell().Element(Th).Text(text).Bold().FontColor(Colors.White);

                            Header("Référence");
                            Header("Date");
                            Header("Type");
                            Header("Catégorie");
                            Header("Description");
                            Header("Source");
                            Header("Montant");
                            Header("Statut");

                            foreach (var t in list)
                            {
                                table.Cell().Element(Td).Text(t.Reference);
                                table.Cell().Element(Td).Text(t.DateDisplay);
                                table.Cell().Element(Td).Text(t.TypeLabel);
                                table.Cell().Element(Td).Text(t.Category);
                                table.Cell().Element(Td).Text(t.Description);
                                table.Cell().Element(Td).Text(t.Source);
                                table.Cell().Element(Td).AlignRight().Text(t.AmountDisplay);
                                table.Cell().Element(Td).Text(t.StatusLabel);
                            }
                        });
                    }));

                    root.Item().PaddingTop(8).Text($"{list.Count} transaction(s) — SBMS Finances")
                        .FontSize(7).FontColor("#94A3B8");
                });
            });
        }).GeneratePdf(path);

        return path;
    }

    private static IContainer Th(IContainer c) =>
        c.Background("#3D6B52").PaddingVertical(5).PaddingHorizontal(4);

    private static IContainer Td(IContainer c) =>
        c.BorderBottom(1).BorderColor(Border).PaddingVertical(4).PaddingHorizontal(4);

    private void KpiBox(IContainer container, string label, string value)
    {
        container.Border(1).BorderColor(Border).Background(GrayBg).Padding(10).Column(col =>
        {
            col.Item().Text(label).FontSize(7).FontColor("#64748B");
            col.Item().PaddingTop(4).Text(value).Bold().FontSize(11).FontColor(_accent);
        });
    }

    private void DrawHeader(IContainer container, string title, string company, CultureInfo culture, int count)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(left =>
            {
                left.Item().Text("SBMS").Bold().FontSize(18).FontColor(_navy);
                left.Item().Text(company).FontSize(8).FontColor("#64748B");
            });
            row.RelativeItem(2).AlignCenter().Column(center =>
            {
                center.Item().Text(title).Bold().FontSize(13).FontColor(_navy);
                center.Item().Text("Gestion financière et trésorerie").FontSize(8).FontColor("#64748B");
            });
            row.RelativeItem().AlignRight().Background(GrayBg).Border(1).BorderColor(Border).Padding(8).Column(meta =>
            {
                PdfThemeHelper.MetaLine(meta, "Date", DateTime.Now.ToString("dd MMMM yyyy", culture));
                PdfThemeHelper.MetaLine(meta, "Heure", DateTime.Now.ToString("HH:mm", culture));
                meta.Item().PaddingTop(4).Background(NavyLight).Padding(4)
                    .Text($"{count} transaction(s)").Bold().FontSize(8).FontColor(_accent);
            });
        });
    }

    private void SectionBox(IContainer container, string title, Action<ColumnDescriptor> content)
    {
        container.Border(1).BorderColor(Border).Column(col =>
        {
            col.Item().Background(NavyLight).PaddingVertical(4).PaddingHorizontal(6)
                .Text(title).Bold().FontSize(7).FontColor(_navy);
            col.Item().Padding(8).Column(content);
        });
    }
}
