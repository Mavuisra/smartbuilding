using System.Globalization;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public class ConsumptionsReportPdfService
{
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
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("SBMS - Détail consommation").Bold().FontSize(16).FontColor("#1B3D3B");
                    col.Item().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm", culture)).FontSize(9).FontColor("#64748B");
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text($"Date : {item.DateDisplay}");
                    col.Item().Text($"Type : {item.TypeLabel}").Bold();
                    col.Item().Text($"Équipement/source : {item.EquipmentSource}");
                    col.Item().Text($"Bâtiment : {item.Building}");
                    col.Item().Text($"Responsable : {item.Responsible}");
                    col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    col.Item().Text($"Coût : {item.CostDisplay}").Bold().FontColor("#1D4ED8");
                    col.Item().Text($"Variation : {item.VariationDisplay}");
                    col.Item().Text($"Statut : {item.StatusLabel}");
                    col.Item().Text($"Période : {item.PeriodType}");
                    col.Item().Text($"Compteur : {item.MeterReference}");
                    col.Item().Text($"Notes : {item.Notes}");
                });
            });
        }).GeneratePdf(path);

        return path;
    }
}
