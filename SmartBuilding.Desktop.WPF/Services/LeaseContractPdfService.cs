using System.IO;
using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartBuilding.Domain.Entities.Location;

namespace SmartBuilding.Desktop.WPF.Services;

public class LeaseContractPdfService
{
    private readonly string _navy = PdfThemeHelper.ResolveHeaderColor();
    private readonly string _green = PdfThemeHelper.ResolveAccentColor();

    static LeaseContractPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string Generate(LeaseContract contract, string companyName = "Bloom Prosperity")
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
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
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(_navy));

                page.Content().Column(root =>
                {
                    root.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text("Bloom Prosperity").Bold().FontSize(20).FontColor(_navy);
                            left.Item().Text(companyName).FontSize(8).FontColor("#64748B");
                        });
                        row.RelativeItem(2).AlignCenter().Column(center =>
                        {
                            center.Item().Text("CONTRAT DE LOCATION").Bold().FontSize(14).FontColor(_navy);
                            center.Item().Text("Document contractuel officiel").FontSize(8).FontColor("#64748B");
                            center.Item().PaddingTop(6).AlignCenter().Background(_navy).PaddingVertical(4).PaddingHorizontal(10)
                                .Text($"N° {contract.ContractNumber}").Bold().FontSize(9).FontColor(Colors.White);
                        });
                        row.RelativeItem().AlignRight().Background(PdfThemeHelper.GrayBg).Border(1).BorderColor(PdfThemeHelper.Border).Padding(8).Column(meta =>
                        {
                            PdfThemeHelper.MetaLine(meta, "Type", contract.ContractType);
                            PdfThemeHelper.MetaLine(meta, "Statut", contract.Status.ToString());
                            PdfThemeHelper.MetaLine(meta, "Émis le", DateTime.Now.ToString("dd/MM/yyyy HH:mm", culture));
                        });
                    });

                    root.Item().PaddingTop(12).Row(row =>
                    {
                        row.RelativeItem().Element(c => PdfThemeHelper.SectionBox(c, "PARTIES", _navy, col =>
                        {
                            PdfThemeHelper.InfoLine(col, "Locataire", contract.Tenant.Name);
                            PdfThemeHelper.InfoLine(col, "Local", $"{contract.Premise.Code} — {contract.Premise.Name}");
                            PdfThemeHelper.InfoLine(col, "Bâtiment", contract.Premise.Building);
                        }));
                        row.ConstantItem(12);
                        row.RelativeItem().Element(c => PdfThemeHelper.SectionBox(c, "CONDITIONS FINANCIÈRES", _navy, col =>
                        {
                            PdfThemeHelper.InfoLine(col, "Loyer mensuel", MoneyFormatter.Format(contract.MonthlyRent));
                            PdfThemeHelper.InfoLine(col, "Caution / garantie", MoneyFormatter.Format(contract.Deposit));
                            PdfThemeHelper.InfoLine(col, "Période", $"{contract.StartDate:dd/MM/yyyy} → {contract.EndDate:dd/MM/yyyy}");
                            col.Item().PaddingTop(6).Background(PdfThemeHelper.NavyLight).Padding(8).Text($"Statut : {contract.Status}")
                                .Bold().FontColor(_green);
                        }));
                    });

                    if (!string.IsNullOrWhiteSpace(contract.Clauses))
                    {
                        root.Item().PaddingTop(12).Element(c => PdfThemeHelper.SectionBox(c, "CLAUSES", _navy, col =>
                        {
                            col.Item().Text(contract.Clauses).FontSize(9).LineHeight(1.35f);
                        }));
                    }

                    root.Item().PaddingTop(14).AlignCenter().Text(t =>
                    {
                        t.Span("Document généré par SBMS — ").FontSize(7).FontColor("#94A3B8");
                        t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(7).FontColor("#94A3B8");
                    });
                });
            });
        }).GeneratePdf(path);

        return path;
    }
}
