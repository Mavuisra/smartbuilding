using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartBuilding.Desktop.WPF.Models;

public class SuppliersPageData
{
    public decimal RentCollectedTotal { get; init; }
    public decimal AvailableBalance { get; init; }
    public decimal TotalExpenses { get; init; }
    public int TotalSuppliers { get; init; }
    public int ActiveSuppliers { get; init; }
    public int UnpaidInvoices { get; init; }
    public decimal MonthlyExpenses { get; init; }
    public int ContractsExpiringSoon { get; init; }
    public int InterventionsThisMonth { get; init; }
    public string ActivePercent { get; init; } = "0%";
    public IReadOnlyList<SupplierListItem> Suppliers { get; init; } = [];
    public IReadOnlyList<SupplierAlertItem> Alerts { get; init; } = [];
    public IReadOnlyList<SupplierCategorySlice> ExpenseByCategory { get; init; } = [];
    public IReadOnlyList<SupplierMonthPoint> ExpenseTrend { get; init; } = [];
    public IReadOnlyList<SupplierTopItem> TopExpensive { get; init; } = [];
}

public partial class SupplierListItem : ObservableObject
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Initials { get; init; } = "FR";
    public string LogoBackground { get; init; } = "#E8F5EE";
    public string LogoForeground { get; init; } = "#2D6A4F";
    public string Category { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string ContractDisplay { get; init; } = "—";
    public string TotalExpensesDisplay { get; init; } = "—";
    public string LastInterventionDisplay { get; init; } = "—";
    public string StatusLabel { get; init; } = "Actif";
    public string StatusBadgeBackground { get; init; } = "#DCFCE7";
    public string StatusBadgeForeground { get; init; } = "#166534";
    public string ServiceType { get; init; } = string.Empty;
    public string Building { get; init; } = string.Empty;
    public string ContactName { get; init; } = "—";
    public string Address { get; init; } = "—";
    public string TaxId { get; init; } = "—";
    public string Notes { get; init; } = "—";
    public string ContractStatus { get; init; } = "—";
    public string ContractEndDisplay { get; init; } = "—";
    public decimal TotalExpenses { get; init; }
    public IReadOnlyList<SupplierInvoiceItem> Invoices { get; init; } = [];
    public IReadOnlyList<SupplierInterventionItem> Interventions { get; init; } = [];

    [ObservableProperty] private bool _isSelected;
}

public class SupplierInvoiceItem
{
    public string Reference { get; init; } = string.Empty;
    public string DateDisplay { get; init; } = string.Empty;
    public string AmountDisplay { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string StatusBadgeBackground { get; init; } = "#DCFCE7";
    public string StatusBadgeForeground { get; init; } = "#166534";
}

public class SupplierInterventionItem
{
    public string DateDisplay { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string AmountDisplay { get; init; } = string.Empty;
}

public class SupplierAlertItem
{
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string AccentColor { get; init; } = "#EA580C";
    public string Background { get; init; } = "#FFEDD5";
}

public class SupplierCategorySlice
{
    public string Category { get; init; } = string.Empty;
    public decimal Amount { get; init; }
}

public class SupplierMonthPoint
{
    public string Label { get; init; } = string.Empty;
    public decimal Amount { get; init; }
}

public class SupplierTopItem
{
    public string Name { get; init; } = string.Empty;
    public decimal Amount { get; init; }
}
