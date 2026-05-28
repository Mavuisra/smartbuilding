using System.Globalization;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public class ConsumptionsReportPdfService
{
    private const string NavyLight = PdfThemeHelper.NavyLight;
    private const string GrayBg = PdfThemeHelper.GrayBg;
    private const string Border = PdfThemeHelper.Border;

    private readonly string _navy = PdfThemeHelper.ResolveHeaderColor();
    private readonly string _green = PdfThemeHelper.ResolveAccentColor();

    static ConsumptionsReportPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string ExportRecordDetails(ConsumptionListItem item)
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SBMS", "Consommations");
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"consommation_detail_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(_navy));

                page.Content().Column(root =>
                {
                    root.Item().Element(c => DrawHeader(c, item, culture));

                    root.Item().PaddingTop(12).Row(row =>
                    {
                        row.RelativeItem().Element(c => SectionBox(c, "INFORMATIONS CONSOMMATION", col =>
                        {
                            InfoLine(col, "Date", item.DateDisplay);
                            InfoLine(col, "Type", item.TypeLabel);
                            InfoLine(col, "Bâtiment", item.Building);
                            InfoLine(col, "Responsable", item.Responsible);
                            InfoLine(col, "Équipement / source", item.EquipmentSource);
                        }));

                        row.ConstantItem(12);

                        row.RelativeItem().Element(c => SectionBox(c, "SUIVI TECHNIQUE", col =>
                        {
                            InfoLine(col, "Période", item.PeriodType);
                            InfoLine(col, "Compteur", item.MeterReference);
                            InfoLine(col, "Statut", item.StatusLabel);
                            InfoLine(col, "Variation", item.VariationDisplay);
                            InfoLine(col, "Coût", item.CostDisplay);
                        }));
                    });

                    root.Item().PaddingTop(12).Element(c => SectionBox(c, "DÉTAIL FINANCIER", col =>
                    {
                        col.Item().Border(1).BorderColor(Border).Column(table =>
                        {
                            table.Item().Background(_navy).Padding(8).Row(r =>
                            {
                                r.RelativeItem().Text("DÉSIGNATION").Bold().FontColor(Colors.White).FontSize(9);
                                r.ConstantItem(140).AlignRight().Text("VALEUR").Bold().FontColor(Colors.White).FontSize(9);
                            });
                            TableRow(table, "Coût total", item.CostDisplay);
                            TableRow(table, "Variation", item.VariationDisplay);
                            TableRow(table, "Statut", item.StatusLabel);
                        });
                    }));

                    root.Item().PaddingTop(10).Element(c => SectionBox(c, "NOTES", col =>
                    {
                        col.Item().Background(GrayBg).Padding(8).Text(
                            string.IsNullOrWhiteSpace(item.Notes) ? "Aucune note." : item.Notes);
                    }));

                    root.Item().PaddingTop(16).Element(c => SectionBox(c, "SIGNATURE & CACHET", col =>
                    {
                        col.Item().Text("Rapport généré automatiquement par SBMS").FontSize(8).FontColor("#64748B");
                        col.Item().PaddingTop(12).Text("_________________________").FontSize(10);
                        col.Item().Text("Validation responsable").SemiBold().FontSize(8);
                        col.Item().Text($"Fait le {DateTime.Now.ToString("dd/MM/yyyy HH:mm", culture)}").FontSize(7).FontColor("#64748B");
                    }));
                });
            });
        }).GeneratePdf(path);

        return path;
    }

    private void DrawHeader(IContainer container, ConsumptionListItem item, CultureInfo culture)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("SBMS").Bold().FontSize(20).FontColor(_navy);
                col.Item().Text("Smart Building Management System").FontSize(8).FontColor("#64748B");
            });

            row.RelativeItem(2).Column(col =>
            {
                col.Item().AlignCenter().Text("RAPPORT DE CONSOMMATION").Bold().FontSize(16).FontColor(_navy);
                col.Item().AlignCenter().Text("SUIVI ÉNERGÉTIQUE").FontSize(9).FontColor("#64748B");
            });

            row.RelativeItem().AlignRight().Background(GrayBg).Border(1).BorderColor(Border).Padding(8).Column(meta =>
            {
                MetaLine(meta, "Type", item.TypeLabel);
                MetaLine(meta, "Date", DateTime.Now.ToString("dd MMMM yyyy", culture));
                MetaLine(meta, "Heure", DateTime.Now.ToString("HH:mm", culture));
                meta.Item().PaddingTop(4).Background(NavyLight).Padding(4)
                    .Text($"Coût : {item.CostDisplay}").Bold().FontSize(8).FontColor(_green);
            });
        });
    }

    private void SectionBox(IContainer container, string title, Action<ColumnDescriptor> content)
        => PdfThemeHelper.SectionBox(container, title, _navy, content);

    private void InfoLine(ColumnDescriptor col, string label, string value)
        => PdfThemeHelper.InfoLine(col, label, value);

    private void MetaLine(ColumnDescriptor col, string label, string value)
        => PdfThemeHelper.MetaLine(col, label, value);

    private void TableRow(ColumnDescriptor col, string label, string value)
    {
        col.Item().BorderBottom(1).BorderColor(Border).PaddingVertical(6).PaddingHorizontal(8).Row(r =>
        {
            r.RelativeItem().Text(label).FontSize(9);
            r.ConstantItem(140).AlignRight().Text(string.IsNullOrWhiteSpace(value) ? "—" : value).FontSize(9);
        });
    }
}
