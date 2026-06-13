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

        var company = building?.Name ?? "Bloom Prosperity";
        const string green = "#2D6A4F";
        const string text = "#0F172A";
        const string muted = "#64748B";
        const string border = "#C5DDD0";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(text));

                page.Header().Column(col =>
                {
                    col.Item().Height(4).Background(green);
                    col.Item().PaddingTop(12).Text(company).Bold().FontSize(15).FontColor(green);
                    col.Item().PaddingTop(4).Text($"Rapport financier — {month:D2}/{year}")
                        .FontSize(12).FontColor(text);
                });

                page.Content().PaddingTop(16).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Border(1).BorderColor(border).Background("#F6FAF8").Padding(10).Column(k =>
                        {
                            k.Item().Text("Recettes").FontSize(7).FontColor(muted);
                            k.Item().Text(Fmt(revenue)).Bold().FontSize(11).FontColor(green);
                        });
                        row.ConstantItem(8);
                        row.RelativeItem().Border(1).BorderColor(border).Background("#F6FAF8").Padding(10).Column(k =>
                        {
                            k.Item().Text("Dépenses").FontSize(7).FontColor(muted);
                            k.Item().Text(Fmt(expenses)).Bold().FontSize(11).FontColor(green);
                        });
                        row.ConstantItem(8);
                        row.RelativeItem().Border(1).BorderColor(border).Background("#F6FAF8").Padding(10).Column(k =>
                        {
                            k.Item().Text("Solde").FontSize(7).FontColor(muted);
                            k.Item().Text(Fmt(revenue - expenses)).Bold().FontSize(11).FontColor(green);
                        });
                    });

                    col.Item().PaddingTop(14).Text("Détail des opérations").Bold().FontSize(10).FontColor(green);

                    col.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1.2f);
                            c.RelativeColumn();
                            c.RelativeColumn(2f);
                            c.RelativeColumn();
                        });

                        table.Header(h =>
                        {
                            foreach (var head in new[] { "Date", "Type", "Description", "Montant" })
                                h.Cell().Background(green).Padding(5).Text(head).FontColor(Colors.White).Bold().FontSize(8);
                        });

                        var i = 0;
                        foreach (var t in transactions)
                        {
                            var alt = i % 2 == 1;
                            var bg = alt ? "#F6FAF8" : "#FFFFFF";
                            table.Cell().Background(bg).BorderBottom(1).BorderColor(border).Padding(4)
                                .Text(t.TransactionDate.ToString("dd/MM/yyyy"));
                            table.Cell().Background(bg).BorderBottom(1).BorderColor(border).Padding(4)
                                .Text(t.Type.ToString());
                            table.Cell().Background(bg).BorderBottom(1).BorderColor(border).Padding(4)
                                .Text(t.Description ?? "—");
                            table.Cell().Background(bg).BorderBottom(1).BorderColor(border).Padding(4).AlignRight()
                                .Text(Fmt(t.Amount));
                            i++;
                        }
                    });
                });

                page.Footer().PaddingTop(8).Row(row =>
                {
                    row.RelativeItem().Text(company).FontSize(7).FontColor(muted);
                    row.RelativeItem().AlignCenter().Text(text =>
                    {
                        text.CurrentPageNumber().FontSize(7).FontColor(muted);
                        text.Span(" / ").FontSize(7).FontColor(muted);
                        text.TotalPages().FontSize(7).FontColor(muted);
                    });
                    row.RelativeItem().AlignRight().Text($"Généré le {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(7).FontColor(muted);
                });
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
