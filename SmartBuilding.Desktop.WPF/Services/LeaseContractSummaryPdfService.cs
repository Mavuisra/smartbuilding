using System.Globalization;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Domain.Enums;
using BuildingInfoDefaults = SmartBuilding.Domain.Entities.Building.BuildingInfoDefaults;

namespace SmartBuilding.Desktop.WPF.Services;

public class LeaseContractSummaryPdfService
{
    private const string Border = PdfThemeHelper.Border;
    private const string GrayBg = PdfThemeHelper.GrayBg;
    private const string NavyLight = PdfThemeHelper.NavyLight;

    private string _navy = "#1B365D";
    private string _green = "#16A34A";

    static LeaseContractSummaryPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string Generate(
        LeaseContract contract,
        BuildingInfo? building,
        string? paymentFrequency = null,
        string? paymentMethod = null)
    {
        var tenant = contract.Tenant;
        var premise = contract.Premise;
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var companyName = string.IsNullOrWhiteSpace(building?.Name)
            ? BuildingInfoDefaults.CompanyName
            : building!.Name;
        var durationMonths = Math.Max(1,
            (contract.EndDate.Year - contract.StartDate.Year) * 12 +
            contract.EndDate.Month - contract.StartDate.Month);
        var statusLabel = LocationContractStatusHelper.ToLabel(contract.Status);

        _navy = AppConfigurationService.Instance?.Current.PdfHeaderHex ?? "#1B365D";
        _green = AppConfigurationService.Instance?.Current.PdfAccentHex ?? "#16A34A";

        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SBMS", "Contracts");
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"Recapitulatif_{contract.ContractNumber}_{contract.Id:N}.pdf");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(_navy));

                page.Content().Column(root =>
                {
                    root.Item().Element(c => DrawHeader(c, companyName, contract, statusLabel, culture));
                    root.Item().PaddingTop(14).Row(row =>
                    {
                        row.RelativeItem().Element(c => DrawTenantBlock(c, tenant));
                        row.ConstantItem(12);
                        row.RelativeItem().Element(c => DrawPremiseBlock(c, premise));
                    });
                    root.Item().PaddingTop(12).Element(c => DrawFinancialBlock(c, contract, paymentFrequency, paymentMethod, culture));
                    root.Item().PaddingTop(12).Element(c => DrawDatesBlock(c, contract, durationMonths, culture));
                    if (!string.IsNullOrWhiteSpace(contract.Clauses))
                        root.Item().PaddingTop(12).Element(c => DrawClausesBlock(c, contract.Clauses));
                    root.Item().PaddingTop(14).Element(c => DrawExecutiveSummary(c, contract, tenant, premise, durationMonths, culture));
                    root.Item().PaddingTop(16).Element(c => DrawFooterSignature(c, companyName, culture));
                });
            });
        }).GeneratePdf(path);

        return path;
    }

    private void DrawHeader(
        IContainer container,
        string companyName,
        LeaseContract contract,
        string statusLabel,
        CultureInfo culture)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text("SBMS").Bold().FontSize(20).FontColor(_navy);
                    left.Item().Text(companyName).FontSize(8).FontColor("#64748B");
                });
                row.RelativeItem(2).AlignCenter().Column(center =>
                {
                    center.Item().Text("RÉCAPITULATIF DE CONTRAT DE LOCATION").Bold().FontSize(14).FontColor(_navy);
                    center.Item().Text("Document officiel — prêt à l'impression").FontSize(8).FontColor("#64748B");
                    center.Item().PaddingTop(6).AlignCenter()
                        .Background(_navy).PaddingVertical(4).PaddingHorizontal(10)
                        .Text($"N° {contract.ContractNumber}").FontSize(9).Bold().FontColor(Colors.White);
                });
                row.RelativeItem().AlignRight().Background(GrayBg).Border(1).BorderColor(Border).Padding(8).Column(meta =>
                {
                    MetaLine(meta, "Type", contract.ContractType);
                    MetaLine(meta, "Statut", statusLabel);
                    MetaLine(meta, "Émis le", DateTime.Now.ToString("dd/MM/yyyy HH:mm", culture));
                });
            });
        });
    }

    private static void MetaLine(ColumnDescriptor col, string label, string value)
        => PdfThemeHelper.MetaLine(col, label, value);

    private void DrawTenantBlock(IContainer container, Tenant tenant)
    {
        SectionBox(container, "LOCATAIRE", col =>
        {
            col.Item().Text(tenant.Name).Bold().FontSize(11);
            InfoLine(col, "Téléphone", Display(tenant.Phone));
            InfoLine(col, "Email", Display(tenant.Email));
            InfoLine(col, "Adresse", Display(tenant.Address));
            InfoLine(col, "Profession", Display(tenant.Profession));
            InfoLine(col, "Entreprise", Display(tenant.Company));
            InfoLine(col, "N° dossier", Display(tenant.DossierNumber));
            InfoLine(col, "ID nationale", Display(tenant.NationalId));
        });
    }

    private void DrawPremiseBlock(IContainer container, Premise premise)
    {
        SectionBox(container, "LOCAL / ESPACE", col =>
        {
            col.Item().Text($"{premise.Code} — {premise.Name}").Bold().FontSize(11);
            InfoLine(col, "Bâtiment", Display(premise.Building));
            InfoLine(col, "Étage", Display(premise.Floor));
            InfoLine(col, "Type", Display(premise.PremiseType));
            InfoLine(col, "Superficie", premise.AreaSqM > 0 ? $"{premise.AreaSqM:N0} m²" : "—");
            InfoLine(col, "Capacité", premise.Capacity > 0 ? premise.Capacity.ToString() : "—");
        });
    }

    private void DrawFinancialBlock(
        IContainer container,
        LeaseContract contract,
        string? paymentFrequency,
        string? paymentMethod,
        CultureInfo culture)
    {
        SectionBox(container, "CONDITIONS FINANCIÈRES", col =>
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2);
                    c.RelativeColumn(3);
                });

                void Row(string label, string value, bool highlight = false)
                {
                    table.Cell().Element(Td).Text(label).FontSize(9).FontColor("#64748B");
                    var cell = table.Cell().Element(Td);
                    if (highlight)
                        cell.Text(value).Bold().FontSize(10).FontColor(_navy);
                    else
                        cell.Text(value).FontSize(9);
                }

                Row("Loyer mensuel", MoneyFormatter.Format(contract.MonthlyRent), true);
                Row("Garantie locative (caution)", MoneyFormatter.Format(contract.Deposit));
                Row("Fréquence de paiement", Display(paymentFrequency, "Mensuelle"));
                Row("Mode de paiement", Display(paymentMethod, "Virement bancaire"));
                Row("Prochaine échéance", contract.StartDate.AddMonths(1).ToString("dd/MM/yyyy", culture));
            });
        });
    }

    private void DrawDatesBlock(IContainer container, LeaseContract contract, int durationMonths, CultureInfo culture)
    {
        SectionBox(container, "DATES & DURÉE", col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    InfoLine(c, "Date de début", contract.StartDate.ToString("dd MMMM yyyy", culture));
                    InfoLine(c, "Date de fin", contract.EndDate.ToString("dd MMMM yyyy", culture));
                });
                row.RelativeItem().Column(c =>
                {
                    InfoLine(c, "Durée du contrat", $"{durationMonths} mois");
                    InfoLine(c, "Validation", contract.ValidatedAt?.ToString("dd/MM/yyyy", culture) ?? "En attente");
                });
            });
        });
    }

    private void DrawClausesBlock(IContainer container, string clauses)
    {
        SectionBox(container, "CONDITIONS PRINCIPALES & CLAUSES", col =>
        {
            col.Item().Text(clauses).FontSize(9).LineHeight(1.35f);
        });
    }

    private void DrawExecutiveSummary(
        IContainer container,
        LeaseContract contract,
        Tenant tenant,
        Premise premise,
        int durationMonths,
        CultureInfo culture)
    {
        var summary =
            $"Le présent récapitulatif confirme la conclusion d'un contrat de location de type « {contract.ContractType} » " +
            $"entre le bailleur et {tenant.Name}, portant sur le local {premise.Code} ({premise.Name}), " +
            $"pour une durée de {durationMonths} mois, du {contract.StartDate:dd/MM/yyyy} au {contract.EndDate:dd/MM/yyyy}. " +
            $"Le loyer mensuel est fixé à {MoneyFormatter.Format(contract.MonthlyRent)} et la garantie locative à " +
            $"{MoneyFormatter.Format(contract.Deposit)}. Ce document synthétise l'ensemble des informations contractuelles " +
            "et peut être imprimé, archivé ou transmis aux parties prenantes.";

        container.Background(NavyLight).Border(1).BorderColor(Border).Padding(12).Column(col =>
        {
            col.Item().Text("RÉSUMÉ").Bold().FontSize(9).FontColor(_navy);
            col.Item().PaddingTop(6).Text(summary).FontSize(9).LineHeight(1.4f).FontColor("#334155");
        });
    }

    private void DrawFooterSignature(IContainer container, string companyName, CultureInfo culture)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Element(c => SectionBox(c, "SIGNATURE LOCATAIRE", inner =>
                {
                    inner.Item().Height(45).Text("_________________________");
                    inner.Item().Text("Locataire").FontSize(8);
                }));
                row.ConstantItem(16);
                row.RelativeItem().Element(c => SectionBox(c, "SIGNATURE BAILLEUR", inner =>
                {
                    inner.Item().Height(45).Text("_________________________");
                    inner.Item().Text(companyName).FontSize(8).SemiBold();
                }));
            });
            col.Item().PaddingTop(10).AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(7).FontColor("#94A3B8"));
                t.Span("Document généré automatiquement par SBMS — ");
                t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm", culture)).Italic();
            });
        });
    }

    private void SectionBox(IContainer container, string title, Action<ColumnDescriptor> content)
        => PdfThemeHelper.SectionBox(container, title, _navy, content);

    private static void InfoLine(ColumnDescriptor col, string label, string value)
        => PdfThemeHelper.InfoLine(col, label, value);

    private IContainer Td(IContainer c) => c.BorderBottom(1).BorderColor(Border).PaddingVertical(5).PaddingHorizontal(4);

    private static string Display(string? value, string fallback = "—") =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
