using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public static class InventoryExportService
{
    public static string ExportPdf(IEnumerable<InventoryListItem> items)
    {
        var list = items.ToList();
        return PdfListExportService.Export(
            moduleFolder: "Inventaire",
            filePrefix: "inventaire",
            documentTitle: "Inventaire du parc",
            documentSubtitle: "Équipements et actifs immobiliers",
            headers: ["Code", "Nom", "Catégorie", "Emplacement", "État", "Responsable", "Dernière maint.", "Prochaine maint.", "Valeur"],
            rows: list.Select(i => new[]
            {
                i.Code, i.Name, i.Category, i.Location, i.StatusLabel, i.Responsible,
                i.LastMaintenanceDisplay, i.NextMaintenanceDisplay, i.EstimatedValueDisplay
            }),
            kpis: [("Équipements", list.Count.ToString())]);
    }

    public static string ExportCsv(IEnumerable<InventoryListItem> items) => ExportPdf(items);
}
