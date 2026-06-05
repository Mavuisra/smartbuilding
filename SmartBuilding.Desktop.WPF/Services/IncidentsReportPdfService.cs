using System.Globalization;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartBuilding.Desktop.WPF.Models;
using BuildingInfoDefaults = SmartBuilding.Domain.Entities.Building.BuildingInfoDefaults;

namespace SmartBuilding.Desktop.WPF.Services;

public class IncidentsReportPdfService
{
    private const string Border = PdfThemeHelper.Border;
    private const string NavyLight = PdfThemeHelper.NavyLight;
    private const string GrayBg = PdfThemeHelper.GrayBg;
    private string _navy = "#1B365D";
    private string _green = "#16A34A";

    static IncidentsReportPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string ExportIncidentsList(IEnumerable<IncidentListItem> items, string title)
    {
        _navy = AppConfigurationService.Instance?.Current.PdfHeaderHex ?? "#1B365D";
        _green = AppConfigurationService.Instance?.Current.PdfAccentHex ?? "#16A34A";
        var company = PdfThemeHelper.ResolveCompanyName();
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var list = items.ToList();

        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SBMS", "Incidents");
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"incidents_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

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

                    root.Item().PaddingTop(12).Element(c => SectionBox(c, "TABLEAU DES INCIDENTS", col =>
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(1f);
                                c.RelativeColumn(1.2f);
                                c.RelativeColumn(1.2f);
                                c.RelativeColumn(1.5f);
                                c.RelativeColumn(1f);
                                c.RelativeColumn(1f);
                                c.RelativeColumn(1f);
                                c.RelativeColumn(0.8f);
                                c.RelativeColumn(1f);
                            });

                            void Header(string text) =>
                                table.Cell().Element(Th).Text(text).Bold().FontColor(Colors.White);

                            Header("ID");
                            Header("Date");
                            Header("Type");
                            Header("Matériel");
                            Header("Emplacement");
                            Header("Gravité");
                            Header("Responsable");
                            Header("Statut");
                            Header("Coût");
                            Header("Intervention");

                            foreach (var i in list)
                            {
                                table.Cell().Element(Td).Text(i.Code);
                                table.Cell().Element(Td).Text(i.DateDisplay);
                                table.Cell().Element(Td).Text(i.TypeLabel);
                                table.Cell().Element(Td).Text(i.EquipmentLabel);
                                table.Cell().Element(Td).Text(i.Location);
                                table.Cell().Element(Td).Text(i.SeverityLabel);
                                table.Cell().Element(Td).Text(i.Responsible);
                                table.Cell().Element(Td).Text(i.StatusLabel);
                                table.Cell().Element(Td).AlignRight().Text(i.CostDisplay);
                                table.Cell().Element(Td).Text(i.InterventionSummary);
                            }
                        });
                    }));

                    root.Item().PaddingTop(10).Text($"{list.Count} incident(s) — {company}")
                        .FontSize(7).FontColor("#94A3B8");
                });
            });
        }).GeneratePdf(path);

        return path;
    }

    private static IContainer Th(IContainer c) =>
        c.Background("#1B365D").PaddingVertical(5).PaddingHorizontal(4);

    private static IContainer Td(IContainer c) =>
        c.BorderBottom(1).BorderColor(Border).PaddingVertical(4).PaddingHorizontal(4);

    private void DrawHeader(IContainer container, string title, string company, CultureInfo culture, int count)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(left =>
            {
                left.Item().Text(company).Bold().FontSize(16).FontColor(_navy);
                left.Item().Text("Rapport incidents & sécurité").FontSize(8).FontColor("#64748B");
            });
            row.RelativeItem(2).AlignCenter().Column(center =>
            {
                center.Item().Text(title).Bold().FontSize(13).FontColor(_navy);
                center.Item().Text("Rapport incidents & sécurité").FontSize(8).FontColor("#64748B");
            });
            row.RelativeItem().AlignRight().Background(GrayBg).Border(1).BorderColor(Border).Padding(8).Column(meta =>
            {
                MetaLine(meta, "Date", DateTime.Now.ToString("dd MMMM yyyy", culture));
                MetaLine(meta, "Heure", DateTime.Now.ToString("HH:mm", culture));
                meta.Item().PaddingTop(4).Background(NavyLight).Padding(4)
                    .Text($"{count} incident(s)").Bold().FontSize(8).FontColor(_green);
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

    private static void MetaLine(ColumnDescriptor col, string label, string value)
        => PdfThemeHelper.MetaLine(col, label, value);
}
