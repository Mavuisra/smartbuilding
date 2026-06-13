using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public class TechnicalReportPdfService
{
    public string ExportEquipmentList(IEnumerable<TechnicalEquipmentItem> items, string title) =>
        PdfListExportService.Export(
            moduleFolder: "Technique",
            filePrefix: "equipements",
            documentTitle: title,
            documentSubtitle: "Parc technique et maintenance",
            headers: ["Code", "Équipement", "Catégorie", "Emplacement", "Statut", "Dern. maint.", "Proch. maint.", "Coût"],
            rows: items.Select(e => new[]
            {
                e.Code, e.Name, e.Category, e.Location, e.StatusLabel,
                e.LastMaintenanceDisplay, e.NextMaintenanceDisplay, e.MaintenanceCostDisplay
            }),
            kpis: [("Équipements", items.Count().ToString())],
            landscape: true);
}
