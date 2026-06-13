using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public class IncidentsReportPdfService
{
    public string ExportIncidentsList(IEnumerable<IncidentListItem> items, string title) =>
        IncidentsExportService.ExportPdf(items);
}
