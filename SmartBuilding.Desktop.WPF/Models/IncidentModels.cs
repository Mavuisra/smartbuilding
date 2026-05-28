using CommunityToolkit.Mvvm.ComponentModel;
using SmartBuilding.Desktop.WPF.Services;

namespace SmartBuilding.Desktop.WPF.Models;

public class IncidentPageData
{
    public decimal RentCollectedTotal { get; init; }
    public decimal AvailableBalance { get; init; }
    public decimal TotalExpenses { get; init; }
    public int TotalIncidents { get; init; }
    public int OpenIncidentsCount { get; init; }
    public int CriticalCount { get; init; }
    public int ResolvedCount { get; init; }
    public int ActiveSecurityAlerts { get; init; }
    public decimal TotalIncidentCost { get; init; }
    public string TotalCostDisplay { get; init; } = MoneyFormatter.ZeroDisplay;
    public int InterventionsToday { get; init; }
    public string SecurityStatusLabel { get; init; } = "Normal";
    public string SecurityStatusColor { get; init; } = "#166534";
    public string RiskiestZone { get; init; } = "—";
    public string ProblematicEquipment { get; init; } = "—";
    public string MonthlyCostDisplay { get; init; } = "—";
    public string SecurityTrendLabel { get; init; } = "—";
    public string RecurringIncidents { get; init; } = "—";
    public IReadOnlyList<IncidentListItem> Incidents { get; init; } = [];
    public IReadOnlyList<IncidentInterventionItem> Interventions { get; init; } = [];
    public IReadOnlyList<IncidentAlertItem> Alerts { get; init; } = [];
    public IReadOnlyList<SecurityMonitorItem> Monitoring { get; init; } = [];
    public IReadOnlyList<IncidentInsightLine> Insights { get; init; } = [];
    public IReadOnlyList<IncidentMonthPoint> MonthlyTrend { get; init; } = [];
    public IReadOnlyList<IncidentTypeSlice> TypeDistribution { get; init; } = [];
    public IReadOnlyList<IncidentSeveritySlice> SeverityDistribution { get; init; } = [];
    public IReadOnlyList<IncidentResolutionPoint> ResolutionTrend { get; init; } = [];
    public IReadOnlyList<IncidentEquipmentOption> EquipmentOptions { get; init; } = [];
    public IReadOnlyList<IncidentTechnicianOption> TechnicianOptions { get; init; } = [];
}

public partial class IncidentListItem : ObservableObject
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string DateDisplay { get; init; } = string.Empty;
    public string TypeLabel { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string Building { get; init; } = "—";
    public string SeverityLabel { get; init; } = string.Empty;
    public string SeverityBadgeBackground { get; init; } = "#DCFCE7";
    public string SeverityBadgeForeground { get; init; } = "#166534";
    public string Responsible { get; init; } = "—";
    public string StatusLabel { get; init; } = string.Empty;
    public string StatusBadgeBackground { get; init; } = "#FFEDD5";
    public string StatusBadgeForeground { get; init; } = "#EA580C";
    public string CostDisplay { get; init; } = "—";
    public string InterventionSummary { get; init; } = "—";
    public string Description { get; init; } = string.Empty;
    public string RiskLevel { get; init; } = "—";
    public string ResolutionDurationDisplay { get; init; } = "—";
    public bool HasPhoto { get; init; }
    public string ResolutionNotes { get; init; } = "—";
    public IReadOnlyList<IncidentInterventionItem> Interventions { get; init; } = [];
}

public class IncidentInterventionItem
{
    public Guid Id { get; init; }
    public Guid IncidentId { get; init; }
    public string IncidentCode { get; init; } = string.Empty;
    public string Technician { get; init; } = string.Empty;
    public string InterventionType { get; init; } = string.Empty;
    public string StartDisplay { get; init; } = string.Empty;
    public string EndDisplay { get; init; } = "—";
    public string CostDisplay { get; init; } = "—";
    public string StatusLabel { get; init; } = string.Empty;
    public string Result { get; init; } = string.Empty;
}

public class IncidentAlertItem
{
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string AccentColor { get; init; } = "#DC2626";
    public string Background { get; init; } = "#FEE2E2";
}

public class SecurityMonitorItem
{
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = "OK";
    public string StatusColor { get; init; } = "#166534";
    public string Detail { get; init; } = string.Empty;
}

public class IncidentInsightLine
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Accent { get; init; } = "#1B3D3B";
}

public class IncidentMonthPoint
{
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
}

public class IncidentTypeSlice
{
    public string Type { get; init; } = string.Empty;
    public int Count { get; init; }
}

public class IncidentSeveritySlice
{
    public string Severity { get; init; } = string.Empty;
    public int Count { get; init; }
}

public class IncidentResolutionPoint
{
    public string Label { get; init; } = string.Empty;
    public double AverageHours { get; init; }
}

public class IncidentEquipmentOption
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
}

public class IncidentTechnicianOption
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Matricule { get; init; } = string.Empty;
    public string Department { get; init; } = string.Empty;
    public string Position { get; init; } = string.Empty;
    public string DisplayLabel => $"{FullName} ({Position})";
}
