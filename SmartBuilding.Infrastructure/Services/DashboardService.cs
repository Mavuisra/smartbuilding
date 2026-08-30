using Microsoft.EntityFrameworkCore;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Domain.Entities.Finance;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Shared.DTOs.Dashboard;

namespace SmartBuilding.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly IDbContextFactory<SmartBuildingDbContext> _contextFactory;
    private readonly OrganizationConnectionResolver? _connectionResolver;

    public DashboardService(
        IDbContextFactory<SmartBuildingDbContext> contextFactory,
        OrganizationConnectionResolver? connectionResolver = null)
    {
        _contextFactory = contextFactory;
        _connectionResolver = connectionResolver;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var financeLedger = new FinanceLedgerService(context);
        await financeLedger.ReconcileAllAsync(cancellationToken);

        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var chartStart = monthStart.AddMonths(-5);
        var trendStart = today.AddDays(-6);

        var transactions = await context.FinancialTransactions
            .Where(t => t.TransactionDate >= chartStart)
            .Select(t => new { t.TransactionDate, t.Type, t.Amount, t.Category, t.Description, t.Reference })
            .ToListAsync(cancellationToken);

        var monthTransactions = transactions.Where(t => t.TransactionDate.Date >= monthStart).ToList();

        var guaranteeIn = monthTransactions
            .Where(t => t.Type == TransactionType.Recette && FinanceMetrics.IsGuaranteeCategory(t.Category))
            .Sum(t => t.Amount);
        var expenses = monthTransactions.Where(t => t.Type == TransactionType.Depense).Sum(t => t.Amount);

        // Source unique des loyers : table RentPayments (pas le ledger, qui peut être désaligné).
        var allRentPayments = await context.RentPayments
            .Select(p => new { p.Year, p.Month, p.AmountDue, p.AmountPaid, p.IsLate, p.PaidDate })
            .ToListAsync(cancellationToken);

        var rentPayments = allRentPayments
            .Where(p => p.Year == today.Year && p.Month == today.Month)
            .ToList();

        var rentCollected = rentPayments.Sum(p => p.AmountPaid);
        var rentPlanned = rentPayments.Sum(p => p.AmountDue);
        var rentRevenue = rentCollected;
        var revenue = rentRevenue;
        var rentLateAmount = rentPayments
            .Where(p => p.IsLate || p.AmountPaid < p.AmountDue)
            .Sum(p => p.AmountDue - p.AmountPaid);

        var latePayments = rentPayments.Count(p => p.IsLate || (p.AmountPaid < p.AmountDue && p.PaidDate == null));

        var cashPosition = await financeLedger.GetCashPositionAsync(cancellationToken);
        rentRevenue = cashPosition.RentCollectedTotal;
        revenue = rentRevenue;

        var totalPremises = await context.Premises.CountAsync(cancellationToken);
        var occupied = await context.Premises.CountAsync(p => p.IsOccupied, cancellationToken);
        var occupancy = totalPremises > 0 ? (double)occupied / totalPremises * 100 : 0;

        var consumptionCost = (await context.ConsumptionRecords
            .Where(c => c.PeriodEnd >= monthStart)
            .Select(c => c.Cost)
            .ToListAsync(cancellationToken)).Sum();

        var openIncidents = await context.Incidents
            .CountAsync(i => i.Status != IncidentStatus.Cloture && i.Status != IncidentStatus.Resolu, cancellationToken);

        var totalEmployees = await context.Employees.CountAsync(e => e.IsActive, cancellationToken);
        var activeLeases = await context.LeaseContracts.CountAsync(l => l.Status == LeaseStatus.Actif, cancellationToken);
        var visitorsToday = await context.Visitors
            .CountAsync(v => v.CheckInAt.Date == today, cancellationToken);
        var pendingMaintenance = await context.MaintenanceRecords
            .CountAsync(m => m.CompletedDate == null, cancellationToken);
        var totalSuppliers = await context.Suppliers.CountAsync(cancellationToken);
        var inventoryCount = await context.InventoryItems.CountAsync(cancellationToken);

        var last6Months = Enumerable.Range(0, 6).Select(i => monthStart.AddMonths(-i)).OrderBy(d => d).ToList();
        var revenueChart = new List<ChartPointDto>();
        var expenseChart = new List<ChartPointDto>();

        foreach (var month in last6Months)
        {
            var end = month.AddMonths(1);
            var label = month.ToString("MMM yyyy");
            revenueChart.Add(new ChartPointDto
            {
                Label = label,
                Value = allRentPayments
                    .Where(p => p.Year == month.Year && p.Month == month.Month)
                    .Sum(p => p.AmountPaid)
            });
            var monthData = transactions.Where(t => t.TransactionDate >= month && t.TransactionDate < end);
            expenseChart.Add(new ChartPointDto
            {
                Label = label,
                Value = monthData.Where(t => t.Type == TransactionType.Depense).Sum(t => t.Amount)
            });
        }

        var financeTrend = Enumerable.Range(0, 7)
            .Select(i => trendStart.AddDays(i))
            .Select(day =>
            {
                var dayEnd = day.AddDays(1);
                var dayTx = transactions.Where(t => t.TransactionDate.Date >= day && t.TransactionDate.Date < dayEnd);
                var rentIn = allRentPayments
                    .Where(p => p.AmountPaid > 0)
                    .Where(p =>
                    {
                        var paidOn = p.PaidDate?.Date ?? new DateTime(p.Year, p.Month, 1);
                        return paidOn >= day && paidOn < dayEnd;
                    })
                    .Sum(p => p.AmountPaid);
                return new ChartPointDto
                {
                    Label = day.ToString("dd/MM"),
                    Value = rentIn - dayTx.Where(t => t.Type == TransactionType.Depense).Sum(t => t.Amount)
                };
            }).ToList();

        var topExpenses = monthTransactions
            .Where(t => t.Type == TransactionType.Depense)
            .GroupBy(t => string.IsNullOrWhiteSpace(t.Category) ? "Autre" : t.Category)
            .OrderByDescending(g => g.Sum(x => x.Amount))
            .Take(5)
            .Select(g => new ChartPointDto { Label = g.Key, Value = g.Sum(x => x.Amount) })
            .ToList();

        var recentMovements = transactions
            .OrderByDescending(t => t.TransactionDate)
            .Take(8)
            .Select(t => new RecentMovementDto
            {
                Date = t.TransactionDate,
                Type = t.Type == TransactionType.Recette ? "IN" : "OUT",
                Category = t.Category,
                Description = t.Description,
                Amount = t.Amount,
                Reference = t.Reference ?? "—"
            }).ToList();

        var alerts = new List<DashboardAlertDto>();
        if (cashPosition.AvailableBalance <= 0 && cashPosition.RentCollectedTotal > 0)
            alerts.Add(new DashboardAlertDto
            {
                Title = "Trésorerie épuisée",
                Message = "Les dépenses ont atteint le total des loyers encaissés.",
                Severity = "Error",
                Timestamp = DateTime.UtcNow
            });
        else if (cashPosition.AvailableBalance < cashPosition.RentCollectedTotal * 0.15m && cashPosition.RentCollectedTotal > 0)
            alerts.Add(new DashboardAlertDto
            {
                Title = "Trésorerie faible",
                Message = $"Disponible : {FinanceMetrics.Fc(cashPosition.AvailableBalance)}",
                Severity = "Warning",
                Timestamp = DateTime.UtcNow
            });

        if (rentLateAmount > 0)
            alerts.Add(new DashboardAlertDto
            {
                Title = "Loyers en retard",
                Message = $"{FinanceMetrics.Fc(rentLateAmount)} à recouvrer ({latePayments} paiement(s))",
                Severity = "Warning",
                Timestamp = DateTime.UtcNow
            });
        else if (rentPlanned > 0 && rentCollected < rentPlanned)
            alerts.Add(new DashboardAlertDto
            {
                Title = "Loyers à encaisser",
                Message = $"{FinanceMetrics.Fc(rentPlanned - rentCollected)} restant(s) ce mois",
                Severity = "Info",
                Timestamp = DateTime.UtcNow
            });
        if (openIncidents > 0)
            alerts.Add(new DashboardAlertDto
            {
                Title = "Incidents ouverts",
                Message = $"{openIncidents} incident(s) à traiter",
                Severity = "Error",
                Timestamp = DateTime.UtcNow
            });
        if (pendingMaintenance > 0)
            alerts.Add(new DashboardAlertDto
            {
                Title = "Maintenance",
                Message = $"{pendingMaintenance} intervention(s) planifiée(s)",
                Severity = "Info",
                Timestamp = DateTime.UtcNow
            });
        if (alerts.Count == 0)
            alerts.Add(new DashboardAlertDto
            {
                Title = "Système",
                Message = "Aucune alerte critique",
                Severity = "Success",
                Timestamp = DateTime.UtcNow
            });

        var syncLogs = await context.SyncLogs.OrderByDescending(s => s.StartedAt).Take(5).ToListAsync(cancellationToken);
        var recentActivity = syncLogs.Select(s => new ActivityItemDto
        {
            Text = s.Success
                ? $"Synchronisation réussie — {s.RecordsPushed} envoyés, {s.RecordsPulled} reçus"
                : $"Échec synchronisation : {s.ErrorMessage}",
            Timestamp = s.StartedAt
        }).ToList();

        if (recentActivity.Count == 0)
            recentActivity.Add(new ActivityItemDto { Text = "Application démarrée — mode hors ligne actif", Timestamp = DateTime.UtcNow });

        var quickStats = new List<QuickStatDto>
        {
            new() { Label = "Loyers encaissés (total)", Value = FinanceMetrics.Fc(cashPosition.RentCollectedTotal) },
            new() { Label = "Disponible ce mois", Value = FinanceMetrics.Fc(cashPosition.AvailableThisMonth) },
            new() { Label = "Disponible (total)", Value = FinanceMetrics.Fc(cashPosition.AvailableBalance) },
            new() { Label = "Loyers du mois", Value = FinanceMetrics.Fc(rentCollected) },
            new() { Label = "Dépenses engagées", Value = FinanceMetrics.Fc(cashPosition.TotalExpenses) },
            new() { Label = "Dépenses (mois)", Value = FinanceMetrics.Fc(expenses) },
            new() { Label = "Taux d'occupation", Value = $"{occupancy:F1} %" },
            new() { Label = "Contrats actifs", Value = activeLeases.ToString() }
        };

        return new DashboardSummaryDto
        {
            MonthlyRevenue = revenue,
            RentRevenue = rentRevenue,
            MonthlyExpenses = expenses,
            NetBalance = cashPosition.AvailableBalance,
            TreasuryBalance = cashPosition.AvailableBalance,
            RentCollectedTotal = cashPosition.RentCollectedTotal,
            TotalExpensesAllTime = cashPosition.TotalExpenses,
            AvailableBalance = cashPosition.AvailableBalance,
            AvailableThisMonth = cashPosition.AvailableThisMonth,
            RentCollected = rentCollected,
            RentPlanned = rentPlanned,
            RentLateAmount = rentLateAmount,
            GuaranteeDeposits = guaranteeIn,
            OpenIncidents = openIncidents,
            TotalConsumptionCost = consumptionCost,
            OccupancyRate = Math.Round(occupancy, 1),
            LatePayments = latePayments,
            TotalPremises = totalPremises,
            OccupiedPremises = occupied,
            TotalEmployees = totalEmployees,
            ActiveLeases = activeLeases,
            VisitorsToday = visitorsToday,
            PendingMaintenance = pendingMaintenance,
            TotalSuppliers = totalSuppliers,
            InventoryItemCount = inventoryCount,
            RevenueChart = revenueChart,
            ExpenseChart = expenseChart,
            FinanceTrendChart = financeTrend,
            TopExpenseCategories = topExpenses,
            Alerts = alerts,
            RecentMovements = recentMovements,
            RecentActivity = recentActivity,
            QuickStats = quickStats
        };
    }

    private async Task<SmartBuildingDbContext> CreateContextAsync(CancellationToken cancellationToken)
    {
        if (_connectionResolver?.ActiveOrganizationId is Guid orgId)
        {
            var connectionString = _connectionResolver.BuildConnectionString(orgId);
            var serverVersion = ServerVersion.Parse("8.0.36-mysql");
            var options = new DbContextOptionsBuilder<SmartBuildingDbContext>()
                .UseMySql(connectionString, serverVersion, mySql => mySql.EnableStringComparisonTranslations())
                .Options;
            return new SmartBuildingDbContext(options);
        }

        return await _contextFactory.CreateDbContextAsync(cancellationToken);
    }
}

/// <summary>Helpers partagés pour les agrégats financiers (Dashboard, Finances).</summary>
public static class FinanceMetrics
{
    public static bool IsRentCategory(string category) =>
        category.Equals(FinanceConstants.CategoryRent, StringComparison.OrdinalIgnoreCase) ||
        category.Contains("Loyer", StringComparison.OrdinalIgnoreCase);

    public static bool IsGuaranteeCategory(string category) =>
        category.Equals(FinanceConstants.CategoryGuarantee, StringComparison.OrdinalIgnoreCase) ||
        (category.Contains("Caution", StringComparison.OrdinalIgnoreCase) &&
         !category.Contains("Remboursement", StringComparison.OrdinalIgnoreCase));

    public static string Fc(decimal amount) =>
        SmartBuilding.Shared.Money.BuildingMoneyFormat.Format(amount, "USD");
}
