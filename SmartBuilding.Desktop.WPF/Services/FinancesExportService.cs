using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public static class FinancesExportService
{
    public static string ExportPdf(IEnumerable<FinanceTransactionItem> items)
    {
        var list = items.ToList();
        return PdfListExportService.Export(
            moduleFolder: "Finances",
            filePrefix: "finances",
            documentTitle: "Journal financier",
            documentSubtitle: "Transactions et mouvements de trésorerie",
            headers: ["Référence", "Date", "Type", "Catégorie", "Description", "Source", "Mode", "Montant", "Statut", "Utilisateur"],
            rows: list.Select(t => new[]
            {
                t.Reference, t.DateDisplay, t.TypeLabel, t.Category, t.Description,
                t.Source, t.PaymentMethod, t.AmountDisplay, t.StatusLabel, t.RecordedBy
            }),
            kpis:
            [
                ("Transactions", list.Count.ToString()),
                ("Recettes", list.Count(x => x.IsRevenue).ToString()),
                ("Dépenses", list.Count(x => !x.IsRevenue).ToString())
            ]);
    }

    /// <summary>Compatibilité — redirige vers l'export PDF.</summary>
    public static string ExportCsv(IEnumerable<FinanceTransactionItem> items) => ExportPdf(items);

    public static bool PrintTransactionsList(IEnumerable<FinanceTransactionItem> items, string title)
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
