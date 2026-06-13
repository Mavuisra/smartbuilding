using System.Globalization;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Domain.Entities.Personnel;
using BuildingInfoDefaults = SmartBuilding.Domain.Entities.Building.BuildingInfoDefaults;

namespace SmartBuilding.Desktop.WPF.Services;

/// <summary>Fiche de paie — même charte graphique que le récapitulatif de contrat (structure RH).</summary>
public class PersonnelPaySlipPdfService
{
    private const string Border = "#CBD5E1";
    private const string GrayBg = "#F8FAFC";
    private const string NavyLight = "#E8EEF5";

    private string _navy = "#1B365D";
    private string _green = "#16A34A";

    static PersonnelPaySlipPdfService() => PdfThemeHelper.EnsureLicense();

    public string Generate(SalaryPayment payment, Employee employee, string? companyName = null)
    {
        var building = AppConfigurationService.Instance is not null
            ? AppConfigurationService.Instance.ToBuildingInfo()
            : null;

        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var company = string.IsNullOrWhiteSpace(companyName)
            ? PdfThemeHelper.ResolveCompanyName()
            : companyName.Trim();

        _navy = PdfThemeHelper.ResolveHeaderColor();
        _green = PdfThemeHelper.ResolveAccentColor();

        var net = payment.NetAmount > 0 ? payment.NetAmount : payment.Amount;
        var gross = payment.GrossSalary > 0 ? payment.GrossSalary : payment.Amount;
        var periodLabel = $"{culture.DateTimeFormat.GetMonthName(payment.Month)} {payment.Year}";

        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SBMS", "PaySlips");
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"FichePaie_{employee.Matricule}_{payment.Year}{payment.Month:00}_{payment.Id:N}.pdf");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                PdfThemeHelper.ConfigurePage(page);

                page.Content().Column(root =>
                {
                    root.Item().Element(c => DrawHeader(c, company, employee, payment, periodLabel, culture));
                    root.Item().PaddingTop(14).Row(row =>
                    {
                        row.RelativeItem().Element(c => DrawEmployeeBlock(c, employee, building));
                        row.ConstantItem(12);
                        row.RelativeItem().Element(c => DrawContractBlock(c, employee));
                    });
                    root.Item().PaddingTop(12).Element(c => DrawPayrollBlock(c, payment, gross, net));
                    root.Item().PaddingTop(12).Element(c => DrawPeriodBlock(c, payment, periodLabel, culture));
                    root.Item().PaddingTop(14).Element(c => DrawSummaryBlock(c, employee, net, periodLabel));
                    root.Item().PaddingTop(16).Element(c => DrawFooterSignature(c, company, culture));
                });
            });
        }).GeneratePdf(path);

        return path;
    }

    private void DrawHeader(
        IContainer container,
        string companyName,
        Employee employee,
        SalaryPayment payment,
        string periodLabel,
        CultureInfo culture)
    {
        container.Element(c => PdfThemeHelper.DocumentHeader(c, new PdfThemeHelper.PdfHeaderOptions(
            DocumentTitle: "Fiche de paie",
            DocumentSubtitle: "Document officiel — ressources humaines",
            DepartmentLine: "Ressources humaines",
            BadgeText: periodLabel.ToUpper(culture),
            Meta:
            [
                ("Matricule", employee.Matricule),
                ("Statut paie", payment.Status),
                ("Émis le", DateTime.Now.ToString("dd/MM/yyyy HH:mm", culture))
            ])));
    }

    private static void MetaLine(ColumnDescriptor col, string label, string value)
    {
        col.Item().Text(t =>
        {
            t.Span($"{label} : ").FontSize(7).FontColor("#64748B");
            t.Span(value).FontSize(8).SemiBold();
        });
    }

    private void DrawEmployeeBlock(IContainer container, Employee employee, BuildingInfo? building)
    {
        SectionBox(container, "EMPLOYÉ", col =>
        {
            col.Item().Text($"{employee.FirstName} {employee.LastName}".Trim()).Bold().FontSize(11);
            InfoLine(col, "Matricule", employee.Matricule);
            InfoLine(col, "Poste", Display(employee.Position));
            InfoLine(col, "Département", Display(employee.Department));
            InfoLine(col, "Téléphone", Display(employee.Phone));
            InfoLine(col, "Email", Display(employee.Email));
            if (building is not null)
                InfoLine(col, "Siège", $"{Display(building.Address)}, {Display(building.City)}");
        });
    }

    private static void DrawContractBlock(IContainer container, Employee employee)
    {
        SectionBox(container, "CONTRAT DE TRAVAIL", col =>
        {
            InfoLine(col, "Type", Display(employee.ContractType));
            InfoLine(col, "Date d'embauche", employee.HireDate.ToString("dd/MM/yyyy"));
            InfoLine(col, "Fin de contrat", employee.ContractEndDate?.ToString("dd/MM/yyyy") ?? "—");
            InfoLine(col, "Salaire de base", MoneyFormatter.Format(employee.BaseSalary));
            InfoLine(col, "Statut", employee.IsActive ? "Actif" : "Inactif");
        });
    }

    private void DrawPayrollBlock(IContainer container, SalaryPayment payment, decimal gross, decimal net)
    {
        SectionBox(container, "RÉMUNÉRATION", col =>
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2);
                    c.RelativeColumn(3);
                });

                void Row(string label, decimal value, bool highlight = false)
                {
                    table.Cell().Element(Td).Text(label).FontSize(9).FontColor("#64748B");
                    var cell = table.Cell().Element(Td).AlignRight();
                    var text = MoneyFormatter.Format(Math.Abs(value));
                    if (value < 0)
                        text = $"- {text}";
                    if (highlight)
                        cell.Text(text).Bold().FontSize(10).FontColor(_navy);
                    else
                        cell.Text(text).FontSize(9);
                }

                Row("Salaire brut", gross);
                Row("Primes", payment.Bonuses);
                Row("Heures supplémentaires", payment.OvertimePay);
                Row("Pénalités", -payment.Penalties);
                Row("Avances sur salaire", -payment.Advances);
                Row("Retenues / déductions", -payment.Deductions);
                Row("NET À PAYER", net, highlight: true);
            });
        });
    }

    private static void DrawPeriodBlock(IContainer container, SalaryPayment payment, string periodLabel, CultureInfo culture)
    {
        SectionBox(container, "PÉRIODE & PAIEMENT", col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    InfoLine(c, "Période", periodLabel);
                    InfoLine(c, "Date de paiement", payment.PaymentDate.ToString("dd MMMM yyyy", culture));
                });
                row.RelativeItem().Column(c =>
                {
                    InfoLine(c, "Heures sup. (qty)", payment.OvertimePay > 0 ? "Voir bulletin" : "—");
                    InfoLine(c, "Notes", Display(payment.Notes));
                });
            });
        });
    }

    private void DrawSummaryBlock(IContainer container, Employee employee, decimal net, string periodLabel)
    {
        var summary =
            $"La présente fiche de paie atteste les éléments de rémunération dus à {employee.FirstName} {employee.LastName} " +
            $"({employee.Matricule}) pour la période {periodLabel}. Le net à payer s'élève à {MoneyFormatter.Format(net)} " +
            $"({FrenchAmountInWords.ToDollarsUs(net)}). Ce document est établi conformément aux données enregistrées dans SBMS " +
            "et peut être imprimé, archivé ou remis à l'employé.";

        container.Background(PdfThemeHelper.BrandMuted).Border(1).BorderColor(PdfThemeHelper.Border).Padding(12).Column(col =>
        {
            col.Item().Text("RÉSUMÉ").Bold().FontSize(9).FontColor(_green);
            col.Item().PaddingTop(6).Text(summary).FontSize(9).LineHeight(1.4f).FontColor("#334155");
        });
    }

    private void DrawFooterSignature(IContainer container, string companyName, CultureInfo culture)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Element(c => SectionBox(c, "SIGNATURE EMPLOYÉ", inner =>
                {
                    inner.Item().Height(45).Text("_________________________");
                    inner.Item().Text("L'employé").FontSize(8);
                }));
                row.ConstantItem(16);
                row.RelativeItem().Element(c => SectionBox(c, "SIGNATURE RH / EMPLOYEUR", inner =>
                {
                    inner.Item().Height(45).Text("_________________________");
                    inner.Item().Text(companyName).FontSize(8).SemiBold();
                }));
            });
            col.Item().PaddingTop(10).AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(7).FontColor("#94A3B8"));
                t.Span($"Document généré automatiquement par {companyName} — ");
                t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm", culture)).Italic();
            });
        });
    }

    private static void SectionBox(IContainer container, string title, Action<ColumnDescriptor> content) =>
        PdfThemeHelper.SectionBox(container, title, content);

    private static void InfoLine(ColumnDescriptor col, string label, string value) =>
        PdfThemeHelper.InfoLine(col, label, value);

    private static IContainer Td(IContainer c) => c.BorderBottom(1).BorderColor(Border).PaddingVertical(5).PaddingHorizontal(4);

    private static string Display(string? value, string fallback = "—") =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
