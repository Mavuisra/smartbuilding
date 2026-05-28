using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Finance;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Services;

namespace SmartBuilding.Desktop.WPF.Services;

public class FinancesService
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    private readonly SmartBuildingDbContext _db;
    private readonly FinanceLedgerService _financeLedger;

    public FinancesService(SmartBuildingDbContext db, FinanceLedgerService financeLedger)
    {
        _db = db;
        _financeLedger = financeLedger;
    }

    public async Task<FinancePageData> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _financeLedger.ReconcileAllAsync(cancellationToken);

        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var prevMonthStart = monthStart.AddMonths(-1);
        var chartStart = monthStart.AddMonths(-11);

        var transactions = await _db.FinancialTransactions
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync(cancellationToken);

        var monthTx = transactions.Where(t => t.TransactionDate >= monthStart).ToList();
        var prevMonthTx = transactions.Where(t => t.TransactionDate >= prevMonthStart && t.TransactionDate < monthStart).ToList();

        var allRentPayments = await _db.RentPayments
            .Select(p => new { p.Year, p.Month, p.AmountPaid })
            .ToListAsync(cancellationToken);

        var guaranteeIn = monthTx
            .Where(t => t.Type == TransactionType.Recette &&
                        IsGuaranteeCategory(t.Category))
            .Sum(t => t.Amount);
        var expenses = monthTx
            .Where(t => t.Type == TransactionType.Depense &&
                        t.Status != "En attente validation PDG")
            .Sum(t => t.Amount);
        var prevRevenue = allRentPayments
            .Where(p => p.Year == prevMonthStart.Year && p.Month == prevMonthStart.Month)
            .Sum(p => p.AmountPaid);
        var prevExpenses = prevMonthTx
            .Where(t => t.Type == TransactionType.Depense &&
                        t.Status != "En attente validation PDG")
            .Sum(t => t.Amount);
        var prevNet = prevRevenue - prevExpenses;

        var payments = await _db.RentPayments
            .Include(p => p.LeaseContract)
            .ThenInclude(c => c!.Premise)
            .Include(p => p.LeaseContract)
            .ThenInclude(c => c!.Tenant)
            .Where(p => p.Year == today.Year && p.Month == today.Month)
            .ToListAsync(cancellationToken);

        var rentPlanned = payments.Sum(p => p.AmountDue);
        var rentCollected = payments.Sum(p => p.AmountPaid);
        var rentRevenue = rentCollected;
        var revenue = rentRevenue;
        var net = revenue - expenses;
        var rentLate = payments.Where(p => p.IsLate || p.AmountPaid < p.AmountDue).Sum(p => p.AmountDue - p.AmountPaid);
        var rentDenom = Math.Max(rentPlanned, 1);

        var cashPosition = await _financeLedger.GetCashPositionAsync(cancellationToken);

        var pending = monthTx.Count(t => t.Status is "En attente" or "En retard" or "En attente validation PDG");
        var pendingAmount = monthTx
            .Where(t => t.Status is "En attente" or "En retard" or "En attente validation PDG")
            .Sum(t => t.Amount);

        var maintenance = monthTx
            .Where(t => t.Type == TransactionType.Depense &&
                        t.Category.Contains("Maintenance", StringComparison.OrdinalIgnoreCase))
            .Sum(t => t.Amount);

        var trend = new List<FinanceMonthPoint>();
        for (var i = 11; i >= 0; i--)
        {
            var m = monthStart.AddMonths(-i);
            var end = m.AddMonths(1);
            var slice = transactions.Where(t => t.TransactionDate >= m && t.TransactionDate < end);
            trend.Add(new FinanceMonthPoint
            {
                Label = m.ToString("MMM", Fr),
                Revenue = allRentPayments
                    .Where(p => p.Year == m.Year && p.Month == m.Month)
                    .Sum(p => p.AmountPaid),
                Expense = slice.Where(t => t.Type == TransactionType.Depense).Sum(t => t.Amount)
            });
        }

        var expenseBreakdown = monthTx
            .Where(t => t.Type == TransactionType.Depense)
            .GroupBy(t => string.IsNullOrWhiteSpace(t.Category) ? "Autre" : t.Category)
            .OrderByDescending(g => g.Sum(x => x.Amount))
            .Select(g => new FinanceCategorySlice { Category = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToList();

        var items = transactions.Select(Map).ToList();
        var categories = transactions.Select(t => t.Category).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().OrderBy(c => c).ToList();
        var sources = transactions.Select(t => t.Source).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList();

        var lateRents = payments
            .Where(p => p.IsLate || p.AmountPaid < p.AmountDue)
            .Select(p => new FinanceLateRentItem
            {
                PremiseLabel = p.LeaseContract?.Premise?.Name ?? "—",
                TenantName = p.LeaseContract?.Tenant?.Name ?? "—",
                AmountDisplay = Fc(p.AmountDue - p.AmountPaid)
            })
            .Take(6)
            .ToList();

        var alerts = BuildAlerts(rentLate, expenses, prevExpenses, pending, lateRents.Count, rentCollected, rentPlanned, cashPosition);
        var treasuryLines = BuildTreasury(cashPosition, monthStart, expenses, guaranteeIn, rentCollected);

        return new FinancePageData
        {
            MonthlyRevenue = revenue,
            RentRevenue = rentRevenue,
            GuaranteeDeposits = guaranteeIn,
            MonthlyExpenses = expenses,
            NetProfit = net,
            RevenueTrend = Trend(revenue, prevRevenue),
            ExpenseTrend = Trend(expenses, prevExpenses),
            ProfitTrend = Trend(net, prevNet),
            RentCollected = rentCollected,
            RentPlanned = rentPlanned,
            RentLate = rentLate,
            RentCollectedPercent = $"{rentCollected * 100 / rentDenom:F1}%",
            RentLatePercent = $"{rentLate * 100 / rentDenom:F1}%",
            TreasuryBalance = cashPosition.AvailableBalance,
            RentCollectedTotal = cashPosition.RentCollectedTotal,
            TotalExpensesAllTime = cashPosition.TotalExpenses,
            AvailableBalance = cashPosition.AvailableBalance,
            PendingInvoices = pending,
            PendingInvoicesAmount = pendingAmount,
            MaintenanceCost = maintenance,
            Transactions = items,
            Alerts = alerts,
            TreasuryLines = treasuryLines,
            LateRents = lateRents,
            RevenueVsExpenseTrend = trend,
            ExpenseBreakdown = expenseBreakdown,
            RentBarPlanned = rentPlanned,
            RentBarCollected = rentCollected,
            RentBarLate = rentLate,
            Categories = categories,
            Sources = sources
        };
    }

    public async Task<string> CreateTransactionAsync(
        TransactionType type,
        string category,
        string description,
        decimal amount,
        string paymentMethod,
        string source,
        string recordedBy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(category))
            return "La catégorie est obligatoire.";
        if (string.IsNullOrWhiteSpace(description))
            return "La description est obligatoire.";
        if (amount <= 0)
            return "Le montant doit être supérieur à zéro.";

        if (type == TransactionType.Recette)
            return "Aucune recette manuelle n'est autorisée. Seuls les loyers encaissés via Locations alimentent la trésorerie.";

        if (IsGuaranteeCategory(category))
            return "Les cautions sont gérées dans Locations → Garanties (hors revenus et trésorerie loyers).";
        if (category.Contains("Remboursement", StringComparison.OrdinalIgnoreCase) &&
            category.Contains("caution", StringComparison.OrdinalIgnoreCase))
            return "Les remboursements de garantie passent par Locations → Garanties.";

        try
        {
            await _financeLedger.RecordExpenseAsync(
                amount,
                category,
                description,
                string.IsNullOrWhiteSpace(source) ? FinanceConstants.SourceFinances : source.Trim(),
                string.IsNullOrWhiteSpace(recordedBy) ? FinanceConstants.RecordedByFinances : recordedBy,
                relatedEntityId: null,
                cancellationToken);
            return string.Empty;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }

    public async Task<string> CreatePendingExpenseForPdgApprovalAsync(
        string category,
        string description,
        decimal amount,
        string source,
        string recordedBy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(category))
            return "La catégorie est obligatoire.";
        if (string.IsNullOrWhiteSpace(description))
            return "La description est obligatoire.";
        if (amount <= 0)
            return "Le montant doit être supérieur à zéro.";

        if (IsGuaranteeCategory(category))
            return "Les cautions sont gérées dans Locations → Garanties.";

        try
        {
            await _financeLedger.RecordExpensePendingPdgApprovalAsync(
                amount,
                category,
                description,
                string.IsNullOrWhiteSpace(source) ? FinanceConstants.SourceFinances : source.Trim(),
                string.IsNullOrWhiteSpace(recordedBy) ? FinanceConstants.RecordedByFinances : recordedBy,
                relatedEntityId: null,
                cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            return string.Empty;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }

    private static FinanceTransactionItem Map(FinancialTransaction t)
    {
        var isRevenue = t.Type == TransactionType.Recette;
        var isRent = IsRentCategory(t.Category);
        var isGuarantee = IsGuaranteeCategory(t.Category);
        var isRefund = t.Category.Contains("Remboursement", StringComparison.OrdinalIgnoreCase);
        var fromLocations = t.Source.Equals(FinanceConstants.SourceLocations, StringComparison.OrdinalIgnoreCase);

        return new FinanceTransactionItem
        {
            Id = t.Id,
            Reference = string.IsNullOrWhiteSpace(t.Reference) ? $"TX-{t.Id.ToString()[..8].ToUpper()}" : t.Reference,
            TransactionDate = t.TransactionDate,
            DateDisplay = t.TransactionDate.ToString("dd/MM/yyyy"),
            TypeLabel = isRevenue ? "Revenu" : "Dépense",
            TypeBadgeBackground = isRevenue ? "#DCFCE7" : "#FEE2E2",
            TypeBadgeForeground = isRevenue ? "#166534" : "#DC2626",
            Category = t.Category,
            Description = t.Description,
            Source = string.IsNullOrWhiteSpace(t.Source) ? ResolveSource(t.Category) : t.Source,
            PaymentMethod = string.IsNullOrWhiteSpace(t.PaymentMethod) ? "Virement" : t.PaymentMethod,
            Amount = t.Amount,
            AmountDisplay = $"{(isRevenue ? "+" : "-")}{Fc(t.Amount)}",
            AmountColor = isRevenue ? "#166534" : "#DC2626",
            StatusLabel = string.IsNullOrWhiteSpace(t.Status) ? "Payé" : t.Status,
            StatusBadgeBackground = StatusBg(t.Status),
            StatusBadgeForeground = StatusFg(t.Status),
            RecordedBy = string.IsNullOrWhiteSpace(t.RecordedBy) ? "Admin Principal" : t.RecordedBy,
            IsRevenue = isRevenue,
            IsRent = isRent,
            IsGuarantee = isGuarantee,
            IsRefund = isRefund,
            IsFromLocations = fromLocations
        };
    }

    public Task<FinanceCashPosition> GetCashPositionAsync(CancellationToken cancellationToken = default) =>
        _financeLedger.GetCashPositionAsync(cancellationToken);

    private static List<FinanceAlertItem> BuildAlerts(
        decimal rentLate, decimal expenses, decimal prevExpenses, int pending, int lateCount,
        decimal rentCollected, decimal rentPlanned, FinanceCashPosition cash)
    {
        var alerts = new List<FinanceAlertItem>();

        if (cash.AvailableBalance <= 0 && cash.RentCollectedTotal > 0)
            alerts.Add(new FinanceAlertItem
            {
                Title = "Trésorerie épuisée",
                Message = "Tous les loyers encaissés sont déjà affectés aux dépenses.",
                Severity = "Error",
                IconKind = "CashRemove",
                AccentColor = "#DC2626",
                Background = "#FEE2E2"
            });
        else if (cash.AvailableBalance < cash.RentCollectedTotal * 0.1m && cash.RentCollectedTotal > 0)
            alerts.Add(new FinanceAlertItem
            {
                Title = "Trésorerie faible",
                Message = $"Il reste {Fc(cash.AvailableBalance)} sur {Fc(cash.RentCollectedTotal)} de loyers encaissés.",
                Severity = "Warning",
                IconKind = "Alert",
                AccentColor = "#EA580C",
                Background = "#FFEDD5"
            });

        if (rentPlanned > 0 && rentCollected < rentPlanned)
        {
            var gap = rentPlanned - rentCollected;
            alerts.Add(new FinanceAlertItem
            {
                Title = "Loyers à encaisser",
                Message = $"{Fc(gap)} restant(s) sur {Fc(rentPlanned)} prévus ce mois",
                Severity = "Info",
                IconKind = "CashRegister",
                AccentColor = "#2563EB",
                Background = "#DBEAFE"
            });
        }

        if (rentLate > 0)
            alerts.Add(new FinanceAlertItem
            {
                Title = "Loyer en retard",
                Message = $"{Fc(rentLate)} à recouvrer ce mois",
                Severity = "Warning",
                IconKind = "AlertCircleOutline",
                AccentColor = "#EA580C",
                Background = "#FFEDD5"
            });

        if (prevExpenses > 0 && expenses > prevExpenses * 1.15m)
            alerts.Add(new FinanceAlertItem
            {
                Title = "Dépenses élevées",
                Message = "Les dépenses dépassent de plus de 15 % le mois précédent",
                Severity = "Error",
                IconKind = "TrendingUp",
                AccentColor = "#DC2626",
                Background = "#FEE2E2"
            });

        if (pending > 0)
            alerts.Add(new FinanceAlertItem
            {
                Title = "Factures en attente",
                Message = $"{pending} facture(s) à valider",
                Severity = "Info",
                IconKind = "FileDocumentOutline",
                AccentColor = "#2563EB",
                Background = "#DBEAFE"
            });

        if (lateCount > 0)
            alerts.Add(new FinanceAlertItem
            {
                Title = "Locataires en défaut",
                Message = $"{lateCount} local(aux) avec paiement incomplet",
                Severity = "Warning",
                IconKind = "HomeAlert",
                AccentColor = "#EA580C",
                Background = "#FFEDD5"
            });

        if (alerts.Count == 0)
            alerts.Add(new FinanceAlertItem
            {
                Title = "Trésorerie saine",
                Message = "Aucune alerte financière critique",
                Severity = "Success",
                IconKind = "CheckCircleOutline",
                AccentColor = "#2D6A4F",
                Background = "#E8F5EE"
            });

        return alerts;
    }

    private static List<FinanceTreasuryLine> BuildTreasury(
        FinanceCashPosition cash,
        DateTime monthStart,
        decimal monthExpenses,
        decimal guaranteeIn,
        decimal rentRevenueMonth)
    {
        return
        [
            new() { Label = "Loyers encaissés (total)", AmountDisplay = Fc(cash.RentCollectedTotal), Color = "#16A34A" },
            new() { Label = "Dépenses engagées (total)", AmountDisplay = $"-{Fc(cash.TotalExpenses)}", Color = "#DC2626" },
            new() { Label = "Disponible pour dépenser", AmountDisplay = Fc(cash.AvailableBalance), Color = "#1B3D3B", IsBold = true },
            new() { Label = "Loyers du mois", AmountDisplay = $"+{Fc(rentRevenueMonth)}", Color = "#64748B" },
            new() { Label = "Dépenses du mois", AmountDisplay = $"-{Fc(monthExpenses)}", Color = "#64748B" },
            new() { Label = "Cautions du mois", AmountDisplay = $"+{Fc(guaranteeIn)}", Color = "#0EA5E9" }
        ];
    }

    private static bool IsRentCategory(string category) =>
        category.Equals(FinanceConstants.CategoryRent, StringComparison.OrdinalIgnoreCase) ||
        category.Contains("Loyer", StringComparison.OrdinalIgnoreCase);

    private static bool IsGuaranteeCategory(string category) =>
        category.Equals(FinanceConstants.CategoryGuarantee, StringComparison.OrdinalIgnoreCase) ||
        (category.Contains("Caution", StringComparison.OrdinalIgnoreCase) &&
         !category.Contains("Remboursement", StringComparison.OrdinalIgnoreCase));

    private static string ResolveSource(string category) => category switch
    {
        var c when IsRentCategory(c) => FinanceConstants.SourceLocations,
        var c when IsGuaranteeCategory(c) => FinanceConstants.SourceLocations,
        var c when c.Contains("Salaire", StringComparison.OrdinalIgnoreCase) => "Personnel",
        var c when c.Contains("Maintenance", StringComparison.OrdinalIgnoreCase) => "Technique",
        var c when c.Contains("Énergie", StringComparison.OrdinalIgnoreCase) || c.Contains("Energie", StringComparison.OrdinalIgnoreCase) => "Consommations",
        var c when c.Contains("Facture", StringComparison.OrdinalIgnoreCase) => "Fournisseurs",
        _ => "Général"
    };

    private static string StatusBg(string status) => status switch
    {
        "En attente" => "#FEF3C7",
        "En attente validation PDG" => "#FEF3C7",
        "En retard" => "#FEE2E2",
        _ => "#DCFCE7"
    };

    private static string StatusFg(string status) => status switch
    {
        "En attente" => "#B45309",
        "En attente validation PDG" => "#B45309",
        "En retard" => "#DC2626",
        _ => "#166534"
    };

    private static string Fc(decimal amount) => MoneyFormatter.Format(amount);

    private static string Trend(decimal current, decimal previous)
    {
        if (previous == 0)
            return current >= 0 ? "+0%" : "0%";
        var pct = (current - previous) / previous * 100;
        return $"{(pct >= 0 ? "+" : "")}{pct:F1}%";
    }
}
