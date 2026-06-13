using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public static class LocationsExportService
{
    public static string ExportPremisesPdf(IEnumerable<LocationsPremiseItem> rows, string title = "Liste des locaux")
    {
        var list = rows.ToList();
        return PdfListExportService.Export(
            moduleFolder: "Locations",
            filePrefix: "locaux",
            documentTitle: title,
            documentSubtitle: "Patrimoine et occupation",
            headers: ["Code", "Nom", "Bâtiment", "Étage", "Type", "Locataire", "Téléphone", "Loyer", "Statut", "Fin contrat"],
            rows: list.Select(r => new[]
            {
                r.Code, r.Name, r.Building, r.Floor, r.PremiseType, r.TenantName,
                r.TenantPhone, r.RentDisplay, r.StatusLabel, r.EndContractDisplay
            }),
            kpis: [("Locaux", list.Count.ToString())]);
    }

    public static bool ExportPremisesCsv(IEnumerable<LocationsPremiseItem> rows) =>
        TryOpenPdf(ExportPremisesPdf(rows));

    public static bool ExportPremisesHtml(IEnumerable<LocationsPremiseItem> rows, string title) =>
        TryOpenPdf(ExportPremisesPdf(rows, title));

    public static bool PrintPremises(IEnumerable<LocationsPremiseItem> rows, string title) =>
        TryOpenPdf(ExportPremisesPdf(rows, title));

    private static bool TryOpenPdf(string path)
    {
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
