using CommunityToolkit.Mvvm.ComponentModel;
using SmartBuilding.Desktop.WPF.Services;

namespace SmartBuilding.Desktop.WPF.Models;

public class ConsumptionPageData
{
    public decimal RentCollectedTotal { get; init; }
    public decimal AvailableBalance { get; init; }
    public decimal TotalExpenses { get; init; }
    public string ElectricityDisplay { get; init; } = "0 kWh";
    public string WaterBillDisplay { get; init; } = MoneyFormatter.ZeroDisplay;
    public string FuelCostDisplay { get; init; } = MoneyFormatter.ZeroDisplay;
    public string InternetCostDisplay { get; init; } = MoneyFormatter.ZeroDisplay;
    public string TotalEnergyCostDisplay { get; init; } = MoneyFormatter.ZeroDisplay;
    public string MonthlyVariationDisplay { get; init; } = "0%";
    public string MonthlyVariationTrend { get; init; } = "—";
    public decimal TotalEnergyCost { get; init; }
    public decimal MonthlyVariationPercent { get; init; }
    public string TopConsumer { get; init; } = "—";
    public string AverageMonthlyCostDisplay { get; init; } = "—";
    public string ConsumptionTrendLabel { get; init; } = "—";
    public string FutureEstimateDisplay { get; init; } = "—";
    public string SavingsDisplay { get; init; } = "—";
    public IReadOnlyList<ConsumptionListItem> Records { get; init; } = [];
    public IReadOnlyList<ConsumptionAlertItem> Alerts { get; init; } = [];
    public IReadOnlyList<ConsumptionMonthPoint> MonthlyTrend { get; init; } = [];
    public IReadOnlyList<ConsumptionTypeSlice> EnergyDistribution { get; init; } = [];
    public IReadOnlyList<ConsumptionCostBar> CostByType { get; init; } = [];
    public IReadOnlyList<ConsumptionComparePoint> MonthComparison { get; init; } = [];
}

public partial class ConsumptionListItem : ObservableObject
{
    public Guid Id { get; init; }
    public string DateDisplay { get; init; } = string.Empty;
    public string TypeLabel { get; init; } = string.Empty;
    public string TypeIconColor { get; init; } = "#2563EB";
    public string EquipmentSource { get; init; } = string.Empty;
    public string QuantityDisplay { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public string CostDisplay { get; init; } = string.Empty;
    public string VariationDisplay { get; init; } = "—";
    public string VariationColor { get; init; } = "#64748B";
    public string Responsible { get; init; } = "—";
    public string StatusLabel { get; init; } = "Normal";
    public string StatusBadgeBackground { get; init; } = "#DCFCE7";
    public string StatusBadgeForeground { get; init; } = "#166534";
    public string Building { get; init; } = "—";
    public string PeriodType { get; init; } = "Mensuel";
    public string MeterReference { get; init; } = "—";
    public string Notes { get; init; } = "—";
    public string Currency { get; init; } = "FC";
    public decimal Cost { get; init; }
    public decimal Quantity { get; init; }
    public decimal VariationPercent { get; init; }
    public bool IsAnomaly { get; init; }
    public IReadOnlyList<ConsumptionHistoryPoint> MonthlyHistory { get; init; } = [];
}

public class ConsumptionHistoryPoint
{
    public string Label { get; init; } = string.Empty;
    public string CostDisplay { get; init; } = string.Empty;
    public string QuantityDisplay { get; init; } = string.Empty;
}

public class ConsumptionAlertItem
{
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string AccentColor { get; init; } = "#EA580C";
    public string Background { get; init; } = "#FFEDD5";
}

public class ConsumptionMonthPoint
{
    public string Label { get; init; } = string.Empty;
    public decimal TotalCost { get; init; }
    public decimal TotalQuantity { get; init; }
}

public class ConsumptionTypeSlice
{
    public string Type { get; init; } = string.Empty;
    public decimal Cost { get; init; }
}

public class ConsumptionCostBar
{
    public string Type { get; init; } = string.Empty;
    public decimal Cost { get; init; }
}

public class ConsumptionComparePoint
{
    public string Label { get; init; } = string.Empty;
    public decimal CurrentCost { get; init; }
    public decimal PreviousCost { get; init; }
}

public class ConsumptionInsightLine
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Accent { get; init; } = "#1B3D3B";
}
