using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartBuilding.Domain.Entities.Personnel;

namespace SmartBuilding.Desktop.WPF.Services;

public class PersonnelPaySlipPdfService
{
    static PersonnelPaySlipPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string Generate(SalaryPayment payment, Employee employee, string companyName = "SBMS Smart Building")
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SBMS", "PaySlips");
        Directory.CreateDirectory(folder);

        var fileName = $"FichePaie_{employee.Matricule}_{payment.Year}{payment.Month:00}_{payment.Id:N}.pdf";
        var path = Path.Combine(folder, fileName);

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
                    col.Item().Text("Fiche de paie").FontSize(14).SemiBold();
                    col.Item().Text($"Période : {payment.Month:00}/{payment.Year}").FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingVertical(20).Column(col =>
                {
                    col.Item().Text("Informations employé").Bold().FontSize(12);
                    col.Item().Text($"{employee.FirstName} {employee.LastName}");
                    col.Item().Text($"Matricule : {employee.Matricule}");
                    col.Item().Text($"Poste : {employee.Position} — {employee.Department}");
                    col.Item().PaddingTop(12).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    col.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(1);
                        });

                        void Row(string label, decimal value, bool bold = false)
                        {
                            table.Cell().Element(CellStyle).Text(label);
                            var cell = table.Cell().Element(CellStyle).AlignRight().Text($"{value:N2} €");
                            if (bold) cell.Bold();
                        }

                        Row("Salaire brut", payment.GrossSalary > 0 ? payment.GrossSalary : payment.Amount);
                        Row("Primes", payment.Bonuses);
                        Row("Heures supplémentaires", payment.OvertimePay);
                        Row("Pénalités", -payment.Penalties);
                        Row("Avances", -payment.Advances);
                        Row("Retenues / déductions", -payment.Deductions);
                        table.Cell().Element(CellStyle).Text("Net à payer").Bold();
                        table.Cell().Element(CellStyle).AlignRight()
                            .Text($"{(payment.NetAmount > 0 ? payment.NetAmount : payment.Amount):N2} €").Bold();
                    });

                    col.Item().PaddingTop(24).Text($"Statut : {payment.Status}");
                    col.Item().Text($"Date de paiement : {payment.PaymentDate:dd/MM/yyyy}");
                    if (!string.IsNullOrWhiteSpace(payment.Notes))
                        col.Item().Text($"Notes : {payment.Notes}");
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Document généré par SBMS — ");
                    text.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                    text.Span(" — Signature RH : _________________________");
                });
            });
        }).GeneratePdf(path);

        return path;
    }

    private static IContainer CellStyle(IContainer container) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(6);
}
