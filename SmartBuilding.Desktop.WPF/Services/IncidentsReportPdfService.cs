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
    private const string Border = "#CBD5E1";
    private string _navy = "#1B365D";

    static IncidentsReportPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string ExportIncidentsList(IEnumerable<IncidentListItem> items, string title)
    {
        _navy = AppConfigurationService.Instance?.Current.PdfHeaderHex ?? "#1B365D";
        var company = AppConfigurationService.Instance?.Current.CompanyName ?? BuildingInfoDefaults.CompanyName;
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
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(8).FontColor(_navy));

                page.Content().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text("SBMS").Bold().FontSize(18).FontColor(_navy);
                            left.Item().Text(company).FontSize(8).FontColor("#64748B");
                        });
                        row.RelativeItem(2).AlignCenter().Text(title).Bold().FontSize(13).FontColor(_navy);
                        row.RelativeItem().AlignRight().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm", culture))
                            .FontSize(8).FontColor("#64748B");
                    });

                    col.Item().PaddingTop(12).Table(table =>
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
                            table.Cell().Element(Td).Text(i.Location);
                            table.Cell().Element(Td).Text(i.SeverityLabel);
                            table.Cell().Element(Td).Text(i.Responsible);
                            table.Cell().Element(Td).Text(i.StatusLabel);
                            table.Cell().Element(Td).AlignRight().Text(i.CostDisplay);
                            table.Cell().Element(Td).Text(i.InterventionSummary);
                        }
                    });

                    col.Item().PaddingTop(10).Text($"{list.Count} incident(s) — SBMS Incidents & Sécurité")
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
}
