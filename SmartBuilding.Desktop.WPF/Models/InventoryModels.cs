using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartBuilding.Desktop.WPF.Models;

public class InventoryPageData
{
    public decimal RentCollectedTotal { get; init; }
    public decimal AvailableBalance { get; init; }
    public decimal TotalExpenses { get; init; }
    public int TotalItems { get; init; }
    public int OperationalCount { get; init; }
    public int MaintenanceCount { get; init; }
    public int OutOfServiceCount { get; init; }
    public int CriticalCount { get; init; }
    public string OperationalPercent { get; init; } = "0%";
    public decimal TotalValue { get; init; }
    public int InterventionsThisMonth { get; init; }
    public IReadOnlyList<InventoryListItem> Items { get; init; } = [];
    public IReadOnlyList<InventoryAlertItem> Alerts { get; init; } = [];
    public IReadOnlyList<InventoryCategorySlice> CategoryDistribution { get; init; } = [];
    public IReadOnlyList<InventoryMonthPoint> MaintenanceCostTrend { get; init; } = [];
    public IReadOnlyList<InventoryStatusSlice> CriticalByStatus { get; init; } = [];
    public IReadOnlyList<InventoryInterventionPoint> InterventionHistory { get; init; } = [];
}

public partial class InventoryListItem : ObservableObject
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Initials { get; init; } = "EQ";
    public string LogoBackground { get; init; } = "#E8F5EE";
    public string LogoForeground { get; init; } = "#2D6A4F";
    public string Category { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string Building { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string StatusBadgeBackground { get; init; } = "#DCFCE7";
    public string StatusBadgeForeground { get; init; } = "#166534";
    public string Responsible { get; init; } = "—";
    public string LastMaintenanceDisplay { get; init; } = "—";
    public string NextMaintenanceDisplay { get; init; } = "—";
    public string EstimatedValueDisplay { get; init; } = "—";
    public string SerialNumber { get; init; } = "—";
    public string Brand { get; init; } = "—";
    public string Model { get; init; } = "—";
    public string UsageDuration { get; init; } = "—";
    public string Notes { get; init; } = "—";
    public string YearMaintenanceCostDisplay { get; init; } = "—";
    public int Quantity { get; init; }
    public IReadOnlyList<InventoryMaintenanceRow> Maintenances { get; init; } = [];
    public IReadOnlyList<InventoryMaintenanceRow> Interventions { get; init; } = [];

    [ObservableProperty] private bool _isSelected;
}

public class InventoryMaintenanceRow
{
    public string DateDisplay { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string CostDisplay { get; init; } = string.Empty;
    public string Technician { get; init; } = "—";
    public string RecordType { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
}

public class InventoryAlertItem
{
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string AccentColor { get; init; } = "#EA580C";
    public string Background { get; init; } = "#FFEDD5";
}

public class InventoryCategorySlice
{
    public string Category { get; init; } = string.Empty;
    public int Count { get; init; }
}

public class InventoryStatusSlice
{
    public string Status { get; init; } = string.Empty;
    public int Count { get; init; }
}

public class InventoryMonthPoint
{
    public string Label { get; init; } = string.Empty;
    public decimal Cost { get; init; }
}

public class InventoryInterventionPoint
{
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
}
