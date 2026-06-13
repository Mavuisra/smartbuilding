using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public static class SuppliersExportService
{
    public static string ExportPdf(IEnumerable<SupplierListItem> items)
    {
        var list = items.ToList();
        return PdfListExportService.Export(
            moduleFolder: "Fournisseurs",
            filePrefix: "fournisseurs",
            documentTitle: "Annuaire fournisseurs",
            documentSubtitle: "Partenaires et prestataires",
            headers: ["Code", "Entreprise", "Catégorie", "Téléphone", "Email", "Contrat", "Dépenses", "Dernière intervention", "Statut"],
            rows: list.Select(s => new[]
            {
                s.Code, s.Name, s.Category, s.Phone, s.Email, s.ContractDisplay,
                s.TotalExpensesDisplay, s.LastInterventionDisplay, s.StatusLabel
            }),
            kpis: [("Fournisseurs", list.Count.ToString())]);
    }

    public static string ExportCsv(IEnumerable<SupplierListItem> items) => ExportPdf(items);
}
