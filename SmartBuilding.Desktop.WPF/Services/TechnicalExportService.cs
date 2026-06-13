using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public static class TechnicalExportService
{
    public static string ExportPdf(IEnumerable<TechnicalEquipmentItem> items)
    {
        var list = items.ToList();
        return PdfListExportService.Export(
            moduleFolder: "Technique",
            filePrefix: "equipements",
            documentTitle: "Parc technique",
            documentSubtitle: "Équipements et maintenance",
            headers: ["Code", "Équipement", "Catégorie", "Emplacement", "Statut", "Dern. maint.", "Proch. maint.", "Coût"],
            rows: list.Select(e => new[]
            {
                e.Code, e.Name, e.Category, e.Location, e.StatusLabel,
                e.LastMaintenanceDisplay, e.NextMaintenanceDisplay, e.MaintenanceCostDisplay
            }),
            kpis: [("Équipements", list.Count.ToString())]);
    }

    public static bool ExportCsv(IEnumerable<TechnicalEquipmentItem> items)
    {
        var path = ExportPdf(items);
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool PrintEquipmentList(IEnumerable<TechnicalEquipmentItem> items, string title)
    {
        var path = ExportPdf(items);
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
