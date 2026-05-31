using SmartBuilding.Shared.Money;

namespace SmartBuilding.Shared.DTOs.Dashboard;

public class DashboardSummaryDto
{
    public decimal MonthlyRevenue { get; set; }
    public decimal RentRevenue { get; set; }
    public decimal MonthlyExpenses { get; set; }
    public decimal NetBalance { get; set; }
    public decimal TreasuryBalance { get; set; }
    public decimal RentCollectedTotal { get; set; }
    public decimal TotalExpensesAllTime { get; set; }
    public decimal AvailableBalance { get; set; }
    /// <summary>Loyers du mois − dépenses du mois (aligné barre latérale).</summary>
    public decimal AvailableThisMonth { get; set; }
    public decimal RentCollected { get; set; }
    public decimal RentPlanned { get; set; }
    public decimal RentLateAmount { get; set; }
    public decimal GuaranteeDeposits { get; set; }
    public int OpenIncidents { get; set; }
    public decimal TotalConsumptionCost { get; set; }
    public double OccupancyRate { get; set; }
    public int LatePayments { get; set; }

    public int TotalPremises { get; set; }
    public int OccupiedPremises { get; set; }
    public int TotalEmployees { get; set; }
    public int ActiveLeases { get; set; }
    public int VisitorsToday { get; set; }
    public int PendingMaintenance { get; set; }
    public int TotalSuppliers { get; set; }
    public int InventoryItemCount { get; set; }

    public List<ChartPointDto> RevenueChart { get; set; } = [];
    public List<ChartPointDto> ExpenseChart { get; set; } = [];
    public List<ChartPointDto> FinanceTrendChart { get; set; } = [];
    public List<ChartPointDto> TopExpenseCategories { get; set; } = [];

    public List<DashboardAlertDto> Alerts { get; set; } = [];
    public List<RecentMovementDto> RecentMovements { get; set; } = [];
    public List<ActivityItemDto> RecentActivity { get; set; } = [];
    public List<QuickStatDto> QuickStats { get; set; } = [];
}

public class ChartPointDto
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public class DashboardAlertDto
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info";
    public DateTime Timestamp { get; set; }
}

public class RecentMovementDto
{
    public DateTime Date { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Reference { get; set; } = string.Empty;

    public string AmountDisplay => BuildingMoneyFormat.Format(Amount, "USD");
}

public class ActivityItemDto
{
    public string Text { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class QuickStatDto
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
