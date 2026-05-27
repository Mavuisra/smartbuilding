using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartBuilding.Domain.Entities.Location;

namespace SmartBuilding.Desktop.WPF.Services;

public class LeaseContractPdfService
{
    static LeaseContractPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string Generate(LeaseContract contract, string companyName = "SBMS")
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SBMS", "Contracts");
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"Contrat_{contract.ContractNumber}_{contract.Id:N}.pdf");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text(companyName).Bold().FontSize(18).FontColor(Colors.Green.Darken3);
                    col.Item().Text("Contrat de location").FontSize(14).SemiBold();
                    col.Item().Text($"N° {contract.ContractNumber} — {contract.ContractType}");
                });

                page.Content().PaddingVertical(16).Column(col =>
                {
                    col.Item().Text("Parties").Bold().FontSize(12);
                    col.Item().Text($"Locataire : {contract.Tenant.Name}");
                    col.Item().Text($"Local : {contract.Premise.Code} — {contract.Premise.Name}");
                    col.Item().Text($"Bâtiment : {contract.Premise.Building}");
                    col.Item().PaddingTop(12).LineHorizontal(1);
                    col.Item().PaddingTop(8).Text("Conditions financières").Bold();
                    col.Item().Text($"Loyer mensuel : {contract.MonthlyRent:N2} FC");
                    col.Item().Text($"Caution / garantie : {contract.Deposit:N2} FC");
                    col.Item().Text($"Période : {contract.StartDate:dd/MM/yyyy} → {contract.EndDate:dd/MM/yyyy}");
                    col.Item().Text($"Statut : {contract.Status}");
                    if (!string.IsNullOrWhiteSpace(contract.Clauses))
                    {
                        col.Item().PaddingTop(12).Text("Clauses").Bold();
                        col.Item().Text(contract.Clauses);
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Document généré par SBMS — ");
                    t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                });
            });
        }).GeneratePdf(path);

        return path;
    }
}
