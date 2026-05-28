using System.Globalization;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public class SuppliersContractPdfService
{
    static SuppliersContractPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string ExportContractDetails(SupplierListItem supplier)
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SBMS", "Fournisseurs");
        Directory.CreateDirectory(folder);

        var fileSafe = string.Concat(supplier.Name.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)));
        if (string.IsNullOrWhiteSpace(fileSafe))
            fileSafe = "fournisseur";

        var path = Path.Combine(folder, $"contrat_{fileSafe}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("SBMS - Détail contrat fournisseur").FontSize(16).Bold().FontColor("#1B3D3B");
                    col.Item().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm", culture)).FontSize(9).FontColor("#64748B");
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text($"Fournisseur : {supplier.Name}").Bold();
                    col.Item().Text($"Code : {supplier.Code}");
                    col.Item().Text($"Contact : {supplier.ContactName} | {supplier.Phone} | {supplier.Email}");
                    col.Item().Text($"Bâtiment : {supplier.Building}");
                    col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    col.Item().Text($"N° contrat : {supplier.ContractDisplay}");
                    col.Item().Text($"Statut : {supplier.ContractStatus}");
                    col.Item().Text($"Début : {supplier.ContractStartDisplay}");
                    col.Item().Text($"Fin : {supplier.ContractEndDisplay}");
                    col.Item().Text($"Montant : {supplier.ContractAmountDisplay}").Bold().FontColor("#166534");
                    col.Item().Text($"Description : {supplier.ContractDescription}");
                    col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    col.Item().Text($"Dépenses totales fournisseur : {supplier.TotalExpensesDisplay}");
                });
            });
        }).GeneratePdf(path);

        return path;
    }
}
