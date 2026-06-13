using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public static class IncidentsExportService
{
    public static string ExportPdf(IEnumerable<IncidentListItem> items)
    {
        var list = items.ToList();
        return PdfListExportService.Export(
            moduleFolder: "Incidents",
            filePrefix: "incidents",
            documentTitle: "Registre des incidents",
            documentSubtitle: "Suivi maintenance et interventions",
            headers: ["ID", "Date", "Type", "Matériel", "Emplacement", "Gravité", "Responsable", "Statut", "Coût", "Intervention"],
            rows: list.Select(i => new[]
            {
                i.Code, i.DateDisplay, i.TypeLabel, i.EquipmentLabel, i.Location, i.SeverityLabel,
                i.Responsible, i.StatusLabel, i.CostDisplay, i.InterventionSummary
            }),
            kpis: [("Incidents", list.Count.ToString())]);
    }

    public static bool ExportCsv(IEnumerable<IncidentListItem> items)
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

    public static bool PrintIncidentsList(IEnumerable<IncidentListItem> items, string title)
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
