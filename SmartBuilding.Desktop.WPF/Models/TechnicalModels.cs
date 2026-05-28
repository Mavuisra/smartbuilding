using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartBuilding.Desktop.WPF.Models;

public class TechnicalPageData
{
    public decimal RentCollectedTotal { get; init; }
    public decimal AvailableBalance { get; init; }
    public decimal TotalExpenses { get; init; }
    public int TotalEquipment { get; init; }
    public int OperationalCount { get; init; }
    public int MaintenanceCount { get; init; }
    public int BrokenCount { get; init; }
    public string OperationalPercent { get; init; } = "0%";
    public string MaintenancePercent { get; init; } = "0%";
    public string BrokenPercent { get; init; } = "0%";
    public decimal MonthlyMaintenanceCost { get; init; }
    public int PlannedThisWeek { get; init; }
    public IReadOnlyList<TechnicalEquipmentItem> Equipment { get; init; } = [];
    public IReadOnlyList<TechnicalCategorySlice> CategoryDistribution { get; init; } = [];
    public IReadOnlyList<TechnicalStatusSlice> StatusDistribution { get; init; } = [];
    public IReadOnlyList<TechnicalMonthPoint> MaintenanceCostTrend { get; init; } = [];
}

public partial class TechnicalEquipmentItem : ObservableObject
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string StatusBadgeBackground { get; init; } = "#DCFCE7";
    public string StatusBadgeForeground { get; init; } = "#166534";
    public string LastMaintenanceDisplay { get; init; } = "—";
    public string NextMaintenanceDisplay { get; init; } = "—";
    public string MaintenanceCostDisplay { get; init; } = "—";
    public string Brand { get; init; } = "—";
    public string Model { get; init; } = "—";
    public string SerialNumber { get; init; } = "—";
    public string InstallationDisplay { get; init; } = "—";
    public string PurchaseValueDisplay { get; init; } = "—";
    public string WarrantyDisplay { get; init; } = "—";
    public string PowerSpec { get; init; } = "—";
    public string VoltageSpec { get; init; } = "—";
    public string FrequencySpec { get; init; } = "—";
    public string FuelType { get; init; } = "—";
    public string OperatingHours { get; init; } = "—";
    public string YearMaintenanceCostDisplay { get; init; } = "—";
    public IReadOnlyList<TechnicalInterventionItem> Interventions { get; init; } = [];

    [ObservableProperty] private bool _isSelected;
}

public class TechnicalInterventionItem
{
    public string DateDisplay { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string CostDisplay { get; init; } = string.Empty;
    public string Technician { get; init; } = "—";
    public string StatusLabel { get; init; } = string.Empty;
}

public class TechnicalCategorySlice
{
    public string Category { get; init; } = string.Empty;
    public int Count { get; init; }
}

public class TechnicalStatusSlice
{
    public string Status { get; init; } = string.Empty;
    public int Count { get; init; }
}

public class TechnicalMonthPoint
{
    public string Label { get; init; } = string.Empty;
    public decimal Cost { get; init; }
}

public class TechnicalInterventionHistoryRow
{
    public Guid MaintenanceId { get; init; }
    public Guid EquipmentId { get; init; }
    public string EquipmentCode { get; init; } = string.Empty;
    public string EquipmentName { get; init; } = string.Empty;
    public string DateDisplay { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Technician { get; init; } = "—";
    public string CostDisplay { get; init; } = "—";
    public string StatusLabel { get; init; } = string.Empty;
    public string StatusBadgeBackground { get; init; } = "#F1F5F9";
    public string StatusBadgeForeground { get; init; } = "#64748B";
    public bool IsPlanned { get; init; }
}
