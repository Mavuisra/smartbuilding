namespace SmartBuilding.Desktop.WPF.Models;

public class FinancePageData
{
    public decimal MonthlyRevenue { get; init; }
    /// <summary>Recettes loyers (source Locations).</summary>
    public decimal RentRevenue { get; init; }
    /// <summary>Cautions encaissées sur le mois.</summary>
    public decimal GuaranteeDeposits { get; init; }
    public decimal MonthlyExpenses { get; init; }
    public decimal NetProfit { get; init; }
    public string RevenueTrend { get; init; } = "0%";
    public string ExpenseTrend { get; init; } = "0%";
    public string ProfitTrend { get; init; } = "0%";
    public decimal RentCollected { get; init; }
    public decimal RentPlanned { get; init; }
    public decimal RentLate { get; init; }
    public string RentCollectedPercent { get; init; } = "0%";
    public string RentLatePercent { get; init; } = "0%";
    public decimal TreasuryBalance { get; init; }
    public decimal RentCollectedTotal { get; init; }
    public decimal TotalExpensesAllTime { get; init; }
    public decimal AvailableBalance { get; init; }
    public int PendingInvoices { get; init; }
    public decimal PendingInvoicesAmount { get; init; }
    public decimal MaintenanceCost { get; init; }
    public IReadOnlyList<FinanceTransactionItem> Transactions { get; init; } = [];
    public IReadOnlyList<FinanceAlertItem> Alerts { get; init; } = [];
    public IReadOnlyList<FinanceTreasuryLine> TreasuryLines { get; init; } = [];
    public IReadOnlyList<FinanceLateRentItem> LateRents { get; init; } = [];
    public IReadOnlyList<FinanceMonthPoint> RevenueVsExpenseTrend { get; init; } = [];
    public IReadOnlyList<FinanceCategorySlice> ExpenseBreakdown { get; init; } = [];
    public decimal RentBarPlanned { get; init; }
    public decimal RentBarCollected { get; init; }
    public decimal RentBarLate { get; init; }
    public IReadOnlyList<string> Categories { get; init; } = [];
    public IReadOnlyList<string> Sources { get; init; } = [];
}

public class FinanceTransactionItem
{
    public Guid Id { get; init; }
    public string Reference { get; init; } = string.Empty;
    public DateTime TransactionDate { get; init; }
    public string DateDisplay { get; init; } = string.Empty;
    public string TypeLabel { get; init; } = string.Empty;
    public string TypeBadgeBackground { get; init; } = "#DCFCE7";
    public string TypeBadgeForeground { get; init; } = "#166534";
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string PaymentMethod { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string AmountDisplay { get; init; } = string.Empty;
    public string AmountColor { get; init; } = "#166534";
    public string StatusLabel { get; init; } = "Payé";
    public string StatusBadgeBackground { get; init; } = "#DCFCE7";
    public string StatusBadgeForeground { get; init; } = "#166534";
    public string RecordedBy { get; init; } = string.Empty;
    public bool IsRevenue { get; init; }
    public bool IsRent { get; init; }
    public bool IsGuarantee { get; init; }
    public bool IsRefund { get; init; }
    public bool IsFromLocations { get; init; }
}

public class FinanceAlertItem
{
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Severity { get; init; } = "Info";
    public string IconKind { get; init; } = "AlertCircleOutline";
    public string AccentColor { get; init; } = "#EA580C";
    public string Background { get; init; } = "#FFEDD5";
}

public class FinanceTreasuryLine
{
    public string Label { get; init; } = string.Empty;
    public string AmountDisplay { get; init; } = string.Empty;
    public string Color { get; init; } = "#1B3D3B";
    public bool IsBold { get; init; }
}

public class FinanceLateRentItem
{
    public string PremiseLabel { get; init; } = string.Empty;
    public string TenantName { get; init; } = string.Empty;
    public string AmountDisplay { get; init; } = string.Empty;
}

public class FinanceMonthPoint
{
    public string Label { get; init; } = string.Empty;
    public decimal Revenue { get; init; }
    public decimal Expense { get; init; }
}

public class FinanceCategorySlice
{
    public string Category { get; init; } = string.Empty;
    public decimal Amount { get; init; }
}
