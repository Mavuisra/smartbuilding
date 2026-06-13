using System.Globalization;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SmartBuilding.Domain.Entities.Location;

namespace SmartBuilding.Desktop.WPF.Services;

public class LeaseContractPdfService
{
    static LeaseContractPdfService() => PdfThemeHelper.EnsureLicense();

    public string Generate(LeaseContract contract, string? companyName = null)
    {
        companyName ??= PdfThemeHelper.ResolveCompanyName();
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SBMS", "Contracts");
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"Contrat_{contract.ContractNumber}_{contract.Id:N}.pdf");
        var green = PdfThemeHelper.ResolveHeaderColor();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                PdfThemeHelper.ConfigurePage(page);

                page.Content().Column(root =>
                {
                    root.Item().Element(c => PdfThemeHelper.DocumentHeader(c, new PdfThemeHelper.PdfHeaderOptions(
                        DocumentTitle: "Contrat de location",
                        DocumentSubtitle: "Document contractuel officiel",
                        DepartmentLine: "Gestion locative",
                        BadgeText: $"N° {contract.ContractNumber}",
                        Meta:
                        [
                            ("Type", contract.ContractType),
                            ("Statut", contract.Status.ToString()),
                            ("Émis le", DateTime.Now.ToString("dd/MM/yyyy HH:mm", culture))
                        ])));

                    root.Item().PaddingTop(14).Row(row =>
                    {
                        row.RelativeItem().Element(c => PdfThemeHelper.SectionBox(c, "Parties", col =>
                        {
                            PdfThemeHelper.InfoLine(col, "Locataire", contract.Tenant.Name);
                            PdfThemeHelper.InfoLine(col, "Local", $"{contract.Premise.Code} — {contract.Premise.Name}");
                            PdfThemeHelper.InfoLine(col, "Bâtiment", contract.Premise.Building);
                        }));
                        row.ConstantItem(12);
                        row.RelativeItem().Element(c => PdfThemeHelper.SectionBox(c, "Conditions financières", col =>
                        {
                            PdfThemeHelper.InfoLine(col, "Loyer mensuel", MoneyFormatter.Format(contract.MonthlyRent));
                            PdfThemeHelper.InfoLine(col, "Caution / garantie", MoneyFormatter.Format(contract.Deposit));
                            PdfThemeHelper.InfoLine(col, "Période",
                                $"{contract.StartDate:dd/MM/yyyy} → {contract.EndDate:dd/MM/yyyy}");
                            col.Item().PaddingTop(8).Background(PdfThemeHelper.BrandMuted).Padding(8)
                                .Text($"Statut : {contract.Status}").Bold().FontSize(9).FontColor(green);
                        }));
                    });

                    if (!string.IsNullOrWhiteSpace(contract.Clauses))
                    {
                        root.Item().PaddingTop(12).Element(c => PdfThemeHelper.SectionBox(c, "Clauses", col =>
                        {
                            col.Item().Text(contract.Clauses).FontSize(9).LineHeight(1.45f).FontColor(PdfThemeHelper.TextPrimary);
                        }));
                    }

                    root.Item().PaddingTop(16).Row(row =>
                    {
                        row.RelativeItem().Element(c => PdfThemeHelper.SignatureBlock(c, "Signature locataire"));
                        row.ConstantItem(16);
                        row.RelativeItem().Element(c => PdfThemeHelper.SignatureBlock(c, "Signature bailleur", inner =>
                        {
                            inner.Item().Text(companyName).FontSize(8).SemiBold().FontColor(PdfThemeHelper.TextPrimary);
                        }));
                    });

                    root.Item().PaddingTop(14).Element(c =>
                        PdfThemeHelper.DocumentFooter(c,
                            $"Document généré par {companyName} — {DateTime.Now:dd/MM/yyyy HH:mm}"));
                });
            });
        }).GeneratePdf(path);

        return path;
    }
}
