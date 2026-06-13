using System.Globalization;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Domain.Entities.Location;
using BuildingInfoDefaults = SmartBuilding.Domain.Entities.Building.BuildingInfoDefaults;

namespace SmartBuilding.Desktop.WPF.Services;

public class RentReceiptPdfService
{
    private const string NavyLight = "#E8EEF5";
    private const string GrayBg = "#F8FAFC";
    private const string Border = "#CBD5E1";
    private const string GreenBg = "#DCFCE7";

    private string _navy = PdfThemeHelper.BrandPrimary;
    private string _green = PdfThemeHelper.BrandPrimary;

    static RentReceiptPdfService()
    {
        PdfThemeHelper.EnsureLicense();
    }

    public string Generate(
        RentPayment payment,
        LeaseContract contract,
        BuildingInfo? building,
        decimal amountThisReceipt,
        IReadOnlyList<RentPayment> paymentHistory)
    {
        var tenant = contract.Tenant;
        var premise = contract.Premise;
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var issuedAt = payment.PaidDate ?? DateTime.Today;
        var receiptNo = payment.ReceiptNumber ?? FormatReceiptNumber(payment);
        var periodLabel = $"{FrenchMonths[payment.Month - 1]} {payment.Year}";
        var totalPaid = amountThisReceipt > 0 ? amountThisReceipt : payment.AmountPaid;
        var rentLine = payment.AmountDue > 0 ? payment.AmountDue : contract.MonthlyRent;
        var penalty = payment.PenaltyAmount;
        var otherFees = Math.Max(0, totalPaid - rentLine - penalty);

        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SBMS", "Receipts");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"Quittance_{receiptNo.Replace('/', '-')}.pdf");

        var landlordName = string.IsNullOrWhiteSpace(building?.Name)
            ? BuildingInfoDefaults.CompanyName
            : building.Name;
        var landlordAddress = FormatAddress(building);
        var landlordPhone = Display(building?.Phone, BuildingInfoDefaults.Phone);
        var landlordEmail = Display(building?.Email, BuildingInfoDefaults.Email);
        var landlordIdNat = Display(building?.NationalId, BuildingInfoDefaults.NationalId);
        var landlordWebsite = Display(building?.Website, BuildingInfoDefaults.Website);
        var durationMonths = Math.Max(1,
            (contract.EndDate.Year - contract.StartDate.Year) * 12 +
            contract.EndDate.Month - contract.StartDate.Month);

        _navy = PdfThemeHelper.ResolveHeaderColor();
        _green = PdfThemeHelper.ResolveAccentColor();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                PdfThemeHelper.ConfigurePage(page);

                page.Content().Column(root =>
                {
                    root.Item().Element(c => DrawHeader(c, landlordName, receiptNo, issuedAt, periodLabel, payment.PaymentStatus));
                    root.Item().PaddingTop(14).Element(c =>
                        DrawParties(c, landlordName, landlordAddress, landlordPhone, landlordEmail, landlordIdNat, tenant));
                    root.Item().PaddingTop(12).Element(c =>
                        DrawInfoRow(c, premise, contract, payment, durationMonths, culture));
                    root.Item().PaddingTop(12).Element(c =>
                        DrawPaymentTable(c, payment, periodLabel, rentLine, penalty, otherFees, totalPaid));
                    root.Item().PaddingTop(6).Element(c =>
                        PdfThemeHelper.AmountHighlight(c, "Montant en lettres : ",
                            FrenchAmountInWords.ToFrancsCongolais(totalPaid)));
                    root.Item().PaddingTop(12).Row(row =>
                    {
                        row.RelativeItem().Element(c => DrawHistory(c, paymentHistory, culture));
                        row.ConstantItem(16);
                        row.RelativeItem().Element(c => DrawSignature(c, landlordName, issuedAt));
                    });
                    root.Item().PaddingTop(16).LineHorizontal(1).LineColor(Border);
                    root.Item().PaddingTop(8).Element(c =>
                        DrawFooter(c, landlordAddress, landlordPhone, landlordEmail, landlordWebsite));
                });
            });
        }).GeneratePdf(path);

        return path;
    }

    private void DrawHeader(IContainer container, string companyName, string receiptNo, DateTime issuedAt, string period, string status)
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        container.Element(c => PdfThemeHelper.DocumentHeader(c, new PdfThemeHelper.PdfHeaderOptions(
            DocumentTitle: "Quittance de loyer",
            DocumentSubtitle: "Reçu de paiement officiel",
            DepartmentLine: "Gestion immobilière",
            BadgeText: $"N° {receiptNo}",
            Meta:
            [
                ("Date d'émission", issuedAt.ToString("dd MMMM yyyy", culture)),
                ("Période", period),
                ("Statut", status)
            ])));
    }

    private void MetaLine(ColumnDescriptor col, string label, string value)
    {
        col.Item().Text(t =>
        {
            t.Span($"{label} : ").FontSize(7).FontColor("#64748B");
            t.Span(value).FontSize(8).SemiBold();
        });
    }

    private void DrawParties(
        IContainer container,
        string landlordName,
        string landlordAddress,
        string landlordPhone,
        string landlordEmail,
        string landlordIdNat,
        Tenant tenant)
    {
        container.Row(row =>
        {
            row.RelativeItem().Element(c => SectionBox(c, "INFORMATIONS PROPRIÉTAIRE / BAILLEUR", col =>
            {
                col.Item().Text(landlordName).Bold().FontSize(10);
                col.Item().PaddingTop(4).Text(landlordAddress).FontSize(8);
                col.Item().Text($"Tél : {landlordPhone}").FontSize(8);
                col.Item().Text($"Email : {landlordEmail}").FontSize(8);
                col.Item().Text(landlordIdNat.StartsWith("ID Nat", StringComparison.OrdinalIgnoreCase)
                    ? landlordIdNat
                    : $"ID Nat. : {landlordIdNat}").FontSize(8);
            }));

            row.ConstantItem(12);

            row.RelativeItem().Element(c => SectionBox(c, "INFORMATIONS LOCATAIRE", col =>
            {
                col.Item().Row(r =>
                {
                    r.RelativeItem().Column(t =>
                    {
                        t.Item().Text(tenant.Name).Bold().FontSize(10);
                        t.Item().PaddingTop(4).Text($"Profession : {Display(tenant.Profession)}").FontSize(8);
                        t.Item().Text($"Téléphone : {Display(tenant.Phone)}").FontSize(8);
                        t.Item().Text($"Email : {Display(tenant.Email)}").FontSize(8);
                        t.Item().Text($"Adresse : {Display(tenant.Address)}").FontSize(8);
                        t.Item().Text($"ID Nat. : {Display(tenant.NationalId)}").FontSize(8);
                    });
                    r.ConstantItem(48).Height(48).Background(PdfThemeHelper.BrandMuted).Border(1).BorderColor(PdfThemeHelper.Border)
                        .AlignCenter().AlignMiddle()
                        .Text(GetInitials(tenant.Name)).Bold().FontSize(14).FontColor(_green);
                });
            }));
        });
    }

    private void DrawInfoRow(
        IContainer container,
        Premise premise,
        LeaseContract contract,
        RentPayment payment,
        int durationMonths,
        CultureInfo culture)
    {
        container.Row(row =>
        {
            row.RelativeItem().Element(c => SectionBox(c, "INFORMATIONS BIEN LOUÉ", col =>
            {
                InfoLine(col, "Bâtiment", Display(premise.Building));
                InfoLine(col, "Appartement / Espace", $"{premise.Code} — {premise.Name}");
                InfoLine(col, "Étage", Display(premise.Floor, "—"));
                InfoLine(col, "Type d'espace", Display(premise.PremiseType));
                InfoLine(col, "Superficie", premise.AreaSqM > 0 ? $"{premise.AreaSqM:N0} m²" : "—");
            }));

            row.ConstantItem(8);

            row.RelativeItem().Element(c => SectionBox(c, "INFORMATIONS CONTRAT", col =>
            {
                InfoLine(col, "N° Contrat", contract.ContractNumber);
                InfoLine(col, "Date début", contract.StartDate.ToString("dd/MM/yyyy", culture));
                InfoLine(col, "Date fin", contract.EndDate.ToString("dd/MM/yyyy", culture));
                InfoLine(col, "Durée", $"{durationMonths} mois");
                InfoLine(col, "Loyer mensuel", MoneyFormatter.Format(contract.MonthlyRent));
            }));

            row.ConstantItem(8);

            row.RelativeItem().Element(c => SectionBox(c, "MODE DE PAIEMENT", col =>
            {
                InfoLine(col, "Mode", Display(payment.PaymentMethod));
                InfoLine(col, "Banque", "—");
                InfoLine(col, "N° Transaction", Display(payment.TransactionReference, "—"));
                InfoLine(col, "Référence", Display(payment.ReceiptNumber, "—"));
                InfoLine(col, "Date transaction", (payment.PaidDate ?? DateTime.Today).ToString("dd/MM/yyyy", culture));
            }));
        });
    }

    private void DrawPaymentTable(
        IContainer container,
        RentPayment payment,
        string periodLabel,
        decimal rentLine,
        decimal penalty,
        decimal otherFees,
        decimal totalPaid)
    {
        container.Border(1).BorderColor(Border).Column(col =>
        {
            col.Item().Background(_navy).Padding(8).Row(h =>
            {
                h.RelativeItem().Text("DÉSIGNATION").Bold().FontColor(Colors.White).FontSize(9);
                h.ConstantItem(120).AlignRight().Text($"MONTANT ({MoneyFormatter.CurrencyCode})").Bold().FontColor(Colors.White).FontSize(9);
            });

            TableRow(col, $"Loyer du mois de {periodLabel}", rentLine);
            if (otherFees > 0)
                TableRow(col, "Charges / frais annexes", otherFees);
            TableRow(col, "Pénalité / Retard", penalty);
            TableRow(col, "Autres frais", 0);

            col.Item().Background(NavyLight).Padding(10).Row(total =>
            {
                total.RelativeItem().AlignMiddle().Text("TOTAL PAYÉ").Bold().FontSize(11).FontColor(_navy);
                total.ConstantItem(120).AlignRight().Text(MoneyFormatter.Format(totalPaid)).Bold().FontSize(12).FontColor(_navy);
                total.ConstantItem(56).AlignRight().AlignMiddle().Border(2).BorderColor(_green)
                    .Background(GreenBg).Padding(6).Text("PAYÉ").Bold().FontSize(10).FontColor(_green);
            });
        });
    }

    private void TableRow(ColumnDescriptor col, string label, decimal amount)
    {
        col.Item().BorderBottom(1).BorderColor(Border).PaddingVertical(6).PaddingHorizontal(8).Row(r =>
        {
            r.RelativeItem().Text(label).FontSize(9);
            r.ConstantItem(120).AlignRight().Text($"{amount:N0}").FontSize(9);
        });
    }

    private void DrawHistory(IContainer container, IReadOnlyList<RentPayment> history, CultureInfo culture)
    {
        SectionBox(container, "HISTORIQUE DES PAIEMENTS", col =>
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2);
                    c.RelativeColumn(2);
                    c.RelativeColumn(2);
                    c.RelativeColumn(1);
                });

                table.Header(h =>
                {
                    h.Cell().Element(Th).Text("Période");
                    h.Cell().Element(Th).Text("Montant");
                    h.Cell().Element(Th).Text("Date");
                    h.Cell().Element(Th).Text("Statut");
                });

                foreach (var p in history.Take(6))
                {
                    var period = $"{FrenchMonths[p.Month - 1]} {p.Year}";
                    table.Cell().Element(Td).Text(period).FontSize(7);
                    table.Cell().Element(Td).Text($"{p.AmountPaid:N0}").FontSize(7);
                    table.Cell().Element(Td)
                        .Text(p.PaidDate?.ToString("dd/MM/yy", culture) ?? "—").FontSize(7);
                    table.Cell().Element(Td).Text(p.PaymentStatus).FontSize(7).FontColor(_green);
                }
            });
        });
    }

    private IContainer Th(IContainer c) =>
        c.Background(NavyLight).Padding(4).DefaultTextStyle(x => x.Bold().FontSize(7));

    private IContainer Td(IContainer c) => c.BorderBottom(1).BorderColor(Border).Padding(4);

    private void DrawSignature(IContainer container, string landlordName, DateTime date)
    {
        SectionBox(container, "NOTES & SIGNATURE", col =>
        {
            col.Item().Background(GrayBg).Padding(8).Text(
                "Paiement reçu en totalité. Cette quittance annule tout reçu à valoir sur la période indiquée.")
                .FontSize(8).Italic().FontColor("#475569");
            col.Item().PaddingTop(12).Text("SIGNATURE & CACHET").Bold().FontSize(8);
            col.Item().PaddingTop(8).Height(50).Text("_________________________").FontSize(10);
            col.Item().Text(landlordName).FontSize(8).SemiBold();
            col.Item().Text($"Fait le {date:dd/MM/yyyy}").FontSize(7).FontColor("#64748B");
        });
    }

    private void DrawFooter(IContainer container, string address, string phone, string email, string website)
    {
        container.Row(row =>
        {
            row.RelativeItem().Text(address).FontSize(7).FontColor("#64748B");
            row.RelativeItem().AlignCenter().Text($"Tél : {phone}  |  {email}").FontSize(7).FontColor("#64748B");
            row.RelativeItem().AlignRight().Text(website).FontSize(7).FontColor("#64748B");
        });
    }

    private void SectionBox(IContainer container, string title, Action<ColumnDescriptor> content) =>
        PdfThemeHelper.SectionBox(container, title, content);

    private void InfoLine(ColumnDescriptor col, string label, string value) =>
        PdfThemeHelper.InfoLine(col, label, value);

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return "?";
        if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
        return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
    }

    private static string FormatReceiptNumber(RentPayment payment) =>
        $"QTL-{payment.Year}-{payment.Month:D2}-{payment.Id.ToString("N")[..8].ToUpperInvariant()}";

    private static string FormatAddress(BuildingInfo? building)
    {
        if (building is null)
            return $"{BuildingInfoDefaults.Address}, {BuildingInfoDefaults.City} — {BuildingInfoDefaults.Country}";
        var parts = new[] { building.Address, building.City, building.Country }
            .Where(p => !string.IsNullOrWhiteSpace(p) && !p.Contains("configurer", StringComparison.OrdinalIgnoreCase));
        return parts.Any()
            ? string.Join(", ", parts)
            : $"{BuildingInfoDefaults.Address}, {BuildingInfoDefaults.City} — {BuildingInfoDefaults.Country}";
    }

    private static string Display(string? value, string fallback = "—") =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static readonly string[] FrenchMonths =
    [
        "Janvier", "Février", "Mars", "Avril", "Mai", "Juin",
        "Juillet", "Août", "Septembre", "Octobre", "Novembre", "Décembre"
    ];
}
