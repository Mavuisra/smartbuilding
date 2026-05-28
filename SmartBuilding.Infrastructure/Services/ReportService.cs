using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Shared.Money;

namespace SmartBuilding.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly SmartBuildingDbContext _context;

    public ReportService(SmartBuildingDbContext context)
    {
        _context = context;
        QuestPDF.Settings.License = LicenseType.Community;
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    public async Task<byte[]> GenerateFinancialPdfAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);
        var transactions = await _context.FinancialTransactions
            .Where(t => t.TransactionDate >= start && t.TransactionDate < end)
            .OrderBy(t => t.TransactionDate)
            .ToListAsync(cancellationToken);

        var revenue = transactions.Where(t => t.Type == TransactionType.Recette).Sum(t => t.Amount);
        var expenses = transactions.Where(t => t.Type == TransactionType.Depense).Sum(t => t.Amount);

        var building = await _context.BuildingInfos.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var currency = building?.Currency ?? "USD";
        var usdRate = building?.UsdExchangeRate > 0 ? building.UsdExchangeRate : 2850m;
        string Fmt(decimal amount) => BuildingMoneyFormat.Format(amount, currency, usdRate);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.Header().Text($"Smart Building — Rapport financier {month:D2}/{year}")
                    .FontSize(18).Bold();
                page.Content().Column(col =>
                {
                    col.Item().Text($"Recettes : {Fmt(revenue)}");
                    col.Item().Text($"Dépenses : {Fmt(expenses)}");
                    col.Item().Text($"Solde : {Fmt(revenue - expenses)}").Bold();
                    col.Item().PaddingTop(20).Text("Détail des opérations :").Bold();
                    foreach (var t in transactions)
                        col.Item().Text($"{t.TransactionDate:dd/MM/yyyy} — {t.Type} — {t.Description} — {Fmt(t.Amount)}");
                });
                page.Footer().AlignCenter().Text($"Généré le {DateTime.Now:dd/MM/yyyy HH:mm}");
            });
        });

        return document.GeneratePdf();
    }

    public async Task<byte[]> GenerateFinancialExcelAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);
        var transactions = await _context.FinancialTransactions
            .Where(t => t.TransactionDate >= start && t.TransactionDate < end)
            .OrderBy(t => t.TransactionDate)
            .ToListAsync(cancellationToken);

        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add("Finances");
        sheet.Cells[1, 1].Value = "Date";
        sheet.Cells[1, 2].Value = "Type";
        sheet.Cells[1, 3].Value = "Catégorie";
        sheet.Cells[1, 4].Value = "Description";
        sheet.Cells[1, 5].Value = "Montant";

        var row = 2;
        foreach (var t in transactions)
        {
            sheet.Cells[row, 1].Value = t.TransactionDate;
            sheet.Cells[row, 2].Value = t.Type.ToString();
            sheet.Cells[row, 3].Value = t.Category;
            sheet.Cells[row, 4].Value = t.Description;
            sheet.Cells[row, 5].Value = t.Amount;
            row++;
        }

        return await package.GetAsByteArrayAsync(cancellationToken);
    }
}
