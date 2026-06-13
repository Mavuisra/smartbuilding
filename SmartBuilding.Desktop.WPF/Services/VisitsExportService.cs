using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public static class VisitsExportService
{
    public static string ExportPdf(IReadOnlyList<VisitListItem> visits)
    {
        return PdfListExportService.Export(
            moduleFolder: "Visites",
            filePrefix: "visites",
            documentTitle: "Registre des visites",
            documentSubtitle: "Contrôle d'accès et accueil",
            headers: ["Code", "Visiteur", "Téléphone", "Personne visitée", "Motif", "Type", "Entrée", "Sortie", "Statut", "Badge", "Zone"],
            rows: visits.Select(v => new[]
            {
                v.VisitCode, v.FullName, v.Phone, v.HostName, v.Purpose, v.VisitType,
                v.CheckInDisplay, v.CheckOutDisplay, v.AccessStatus, v.BadgeNumber, v.Zone
            }),
            kpis: [("Visites", visits.Count.ToString())]);
    }

    public static string ExportCsv(IReadOnlyList<VisitListItem> visits) => ExportPdf(visits);

    public static bool PrintPdfReport(IReadOnlyList<VisitListItem> visits, string title)
    {
        var path = ExportPdf(visits);
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
