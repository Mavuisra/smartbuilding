using System.Globalization;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Domain.Entities.Location;
using BuildingInfoDefaults = SmartBuilding.Domain.Entities.Building.BuildingInfoDefaults;

namespace SmartBuilding.Desktop.WPF.Services;

public class GuaranteeDischargePdfService
{
    private const string Border = PdfThemeHelper.Border;
    private const string GrayBg = PdfThemeHelper.GrayBg;
    private const string NavyLight = PdfThemeHelper.NavyLight;

    private string _navy = "#1B365D";
    private string _green = "#16A34A";

    static GuaranteeDischargePdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string Generate(
        LeaseGuarantee guarantee,
        LeaseContract contract,
        Tenant tenant,
        Premise premise,
        BuildingInfo? building,
        decimal amountRefunded,
        DateTime refundDate)
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var companyName = string.IsNullOrWhiteSpace(building?.Name)
            ? PdfThemeHelper.ResolveCompanyName()
            : building!.Name;
        var companyAddress = FormatAddress(building);
        var dischargeNo = $"DCH-{refundDate:yyyyMMdd}-{guarantee.Id.ToString("N")[..8].ToUpperInvariant()}";

        _navy = AppConfigurationService.Instance?.Current.PdfHeaderHex ?? "#1B365D";
        _green = AppConfigurationService.Instance?.Current.PdfAccentHex ?? "#16A34A";

        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SBMS", "Discharges");
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"Decharge_{contract.ContractNumber.Replace('/', '-')}_{dischargeNo}.pdf");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(_navy));

                page.Content().Column(root =>
                {
                    root.Item().Element(c => DrawHeader(c, companyName, dischargeNo, refundDate, culture));
                    root.Item().PaddingTop(16).Element(c =>
                        DrawTenantSection(c, tenant, culture));
                    root.Item().PaddingTop(12).Element(c =>
                        DrawRefundDetails(c, contract, premise, amountRefunded, refundDate, culture));
                    root.Item().PaddingTop(14).Element(c =>
                        DrawOfficialText(c, tenant, contract, amountRefunded, refundDate, culture));
                    root.Item().PaddingTop(16).Row(row =>
                    {
                        row.RelativeItem().Element(c => DrawTenantSignature(c, tenant, refundDate, culture));
                        row.ConstantItem(24);
                        row.RelativeItem().Element(c => DrawLandlordSignature(c, companyName, refundDate, culture));
                    });
                    root.Item().PaddingTop(20).LineHorizontal(1).LineColor(Border);
                    root.Item().PaddingTop(8).AlignCenter().Text(t =>
                    {
                        t.Span($"{companyName} — {companyAddress}").FontSize(7).FontColor("#64748B");
                    });
                });
            });
        }).GeneratePdf(path);

        return path;
    }

    private void DrawHeader(IContainer container, string companyName, string dischargeNo, DateTime date, CultureInfo culture)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(companyName).Bold().FontSize(16).FontColor(_navy);
                    left.Item().Text("Gestion locative").FontSize(9).FontColor("#64748B");
                });
                row.RelativeItem(2).AlignCenter().Column(center =>
                {
                    center.Item().Text("DÉCHARGE DE GARANTIE LOCATIVE").Bold().FontSize(15).FontColor(_navy);
                    center.Item().AlignCenter().Text("Attestation de restitution intégrale").FontSize(9).Italic().FontColor("#64748B");
                });
                row.RelativeItem().AlignRight().Background(GrayBg).Border(1).BorderColor(Border).Padding(8).Column(meta =>
                {
                    meta.Item().Text(t =>
                    {
                        t.Span("N° document : ").FontSize(7).FontColor("#64748B");
                        t.Span(dischargeNo).FontSize(8).SemiBold();
                    });
                    meta.Item().Text(t =>
                    {
                        t.Span("Date : ").FontSize(7).FontColor("#64748B");
                        t.Span(date.ToString("dd MMMM yyyy", culture)).FontSize(8).SemiBold();
                    });
                });
            });
        });
    }

    private void DrawTenantSection(IContainer container, Tenant tenant, CultureInfo culture)
    {
        SectionBox(container, "IDENTITÉ DU LOCATAIRE", col =>
        {
            InfoLine(col, "Nom complet", tenant.Name);
            InfoLine(col, "Téléphone", Display(tenant.Phone));
            InfoLine(col, "Email", Display(tenant.Email));
            InfoLine(col, "Adresse", Display(tenant.Address));
            InfoLine(col, "Pièce d'identité", Display(tenant.NationalId));
            if (tenant.DateOfBirth.HasValue)
                InfoLine(col, "Date de naissance", tenant.DateOfBirth.Value.ToString("dd/MM/yyyy", culture));
        });
    }

    private void DrawRefundDetails(
        IContainer container,
        LeaseContract contract,
        Premise premise,
        decimal amountRefunded,
        DateTime refundDate,
        CultureInfo culture)
    {
        SectionBox(container, "DÉTAILS DU REMBOURSEMENT", col =>
        {
            InfoLine(col, "Référence contrat", contract.ContractNumber);
            InfoLine(col, "Local concerné", $"{premise.Code} — {premise.Name}");
            InfoLine(col, "Bâtiment", Display(premise.Building));
            InfoLine(col, "Montant remboursé", MoneyFormatter.Format(amountRefunded));
            InfoLine(col, "Date du remboursement", refundDate.ToString("dd MMMM yyyy", culture));
            InfoLine(col, "Garantie initiale", MoneyFormatter.Format(contract.Deposit));
            col.Item().PaddingTop(8).Background(NavyLight).Padding(10).Text(t =>
            {
                t.Span("Montant en lettres : ").Italic();
                t.Span(FrenchAmountInWords.ToFrancsCongolais(amountRefunded)).Bold().FontColor(_green);
            });
        });
    }

    private void DrawOfficialText(
        IContainer container,
        Tenant tenant,
        LeaseContract contract,
        decimal amountRefunded,
        DateTime refundDate,
        CultureInfo culture)
    {
        var text =
            $"Je soussigné(e), représentant légal du bailleur, atteste par la présente que la garantie locative " +
            $"relative au contrat n° {contract.ContractNumber}, conclu avec {tenant.Name}, d'un montant de " +
            $"{MoneyFormatter.Format(amountRefunded)}, a été intégralement restituée au locataire le " +
            $"{refundDate.ToString("dd MMMM yyyy", culture)}.\n\n" +
            "Par la présente décharge, le locataire reconnaît avoir reçu la totalité des sommes dues au titre " +
            "de la garantie locative et déclare qu'aucune dette, réclamation ou somme complémentaire liée à " +
            "cette garantie ne subsiste à ce jour entre les parties.\n\n" +
            "Le présent document est établi en deux exemplaires originaux, dont un remis au locataire et un " +
            "conservé par le bailleur, et peut être produit en cas de litige ou de contrôle administratif.";

        SectionBox(container, "TEXTE OFFICIEL", col =>
        {
            col.Item().Text(text).FontSize(9).LineHeight(1.4f).FontColor("#334155");
        });
    }

    private void DrawTenantSignature(IContainer container, Tenant tenant, DateTime date, CultureInfo culture)
    {
        SectionBox(container, "SIGNATURE DU LOCATAIRE", col =>
        {
            col.Item().Text("Lu et approuvé — bon pour décharge").FontSize(8).Italic().FontColor("#64748B");
            col.Item().PaddingTop(24).Height(50).Text("_________________________").FontSize(10);
            col.Item().Text(tenant.Name).SemiBold().FontSize(9);
            col.Item().Text($"Fait le {date:dd/MM/yyyy}").FontSize(7).FontColor("#64748B");
        });
    }

    private void DrawLandlordSignature(IContainer container, string companyName, DateTime date, CultureInfo culture)
    {
        SectionBox(container, "SIGNATURE & CACHET DU BAILLEUR", col =>
        {
            col.Item().Text("Pour le bailleur").FontSize(8).Italic().FontColor("#64748B");
            col.Item().PaddingTop(24).Height(50).Text("_________________________").FontSize(10);
            col.Item().Text(companyName).SemiBold().FontSize(9);
            col.Item().Text($"Fait le {date:dd/MM/yyyy}").FontSize(7).FontColor("#64748B");
        });
    }

    private void SectionBox(IContainer container, string title, Action<ColumnDescriptor> content)
        => PdfThemeHelper.SectionBox(container, title, _navy, content);

    private static void InfoLine(ColumnDescriptor col, string label, string value)
        => PdfThemeHelper.InfoLine(col, label, value);

    private static string Display(string? value, string fallback = "—") =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string FormatAddress(BuildingInfo? building)
    {
        if (building is null)
            return $"{BuildingInfoDefaults.Address}, {BuildingInfoDefaults.City}";
        var parts = new[] { building.Address, building.City, building.Country }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return parts.Any() ? string.Join(", ", parts) : BuildingInfoDefaults.Address;
    }
}
