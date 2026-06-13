using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public static class ConsumptionsExportService
{
    public static string ExportPdf(IEnumerable<ConsumptionListItem> items)
    {
        var list = items.ToList();
        return PdfListExportService.Export(
            moduleFolder: "Consommations",
            filePrefix: "consommations",
            documentTitle: "Relevé de consommations",
            documentSubtitle: "Suivi énergétique et charges",
            headers: ["Date", "Type", "Poste", "Motif", "Payé par", "Montant", "Variation", "Remboursement", "Statut", "Bâtiment"],
            rows: list.Select(i => new[]
            {
                i.DateDisplay, i.TypeLabel, i.EquipmentSource, i.ExpenseMotif, i.PaidBy,
                i.CostDisplay, i.VariationDisplay, i.ReimbursementDisplay, i.StatusLabel, i.Building
            }),
            kpis: [("Relevés", list.Count.ToString())]);
    }

    public static string ExportCsv(IEnumerable<ConsumptionListItem> items) => ExportPdf(items);
}
