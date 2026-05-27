using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartBuilding.Desktop.WPF.Models;

public class VisitsPageData
{
    public int VisitorsToday { get; init; }
    public int ActiveVisits { get; init; }
    public int AccessGranted { get; init; }
    public int AccessDenied { get; init; }
    public int ScheduledAppointments { get; init; }
    public int PendingCheckouts { get; init; }
    public string SecurityStatusLabel { get; init; } = "Accès normal";
    public string SecurityStatusColor { get; init; } = "#166534";
    public string BusiestZone { get; init; } = "—";
    public string PeakHourLabel { get; init; } = "—";
    public string AverageDurationDisplay { get; init; } = "—";
    public IReadOnlyList<VisitListItem> Visits { get; init; } = [];
    public IReadOnlyList<VisitAppointmentItem> Appointments { get; init; } = [];
    public IReadOnlyList<VisitAlertItem> Alerts { get; init; } = [];
    public IReadOnlyList<AccessZoneItem> AccessZones { get; init; } = [];
    public IReadOnlyList<VisitInsightLine> Insights { get; init; } = [];
    public IReadOnlyList<VisitDayPoint> DailyTrend { get; init; } = [];
    public IReadOnlyList<VisitTypeSlice> TypeDistribution { get; init; } = [];
    public IReadOnlyList<VisitAccessSlice> AccessDistribution { get; init; } = [];
    public IReadOnlyList<VisitHourPoint> HourlyTraffic { get; init; } = [];
}

public partial class VisitListItem : ObservableObject
{
    public Guid Id { get; init; }
    public string VisitCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Initials { get; init; } = "??";
    public string LogoBackground { get; init; } = "#DBEAFE";
    public string LogoForeground { get; init; } = "#1D4ED8";
    public string Phone { get; init; } = "—";
    public string HostName { get; init; } = "—";
    public string Purpose { get; init; } = "—";
    public string VisitType { get; init; } = "—";
    public string CheckInDisplay { get; init; } = "—";
    public string CheckOutDisplay { get; init; } = "En cours";
    public string AccessStatus { get; init; } = "Actif";
    public string StatusBadgeBackground { get; init; } = "#DCFCE7";
    public string StatusBadgeForeground { get; init; } = "#166534";
    public string BadgeNumber { get; init; } = "—";
    public string Building { get; init; } = "—";
    public string Zone { get; init; } = "—";
    public string Company { get; init; } = "—";
    public string Email { get; init; } = "—";
    public string IdDocument { get; init; } = "—";
    public string IdDocumentType { get; init; } = "CNI";
    public string AllowedZones { get; init; } = "—";
    public string PresenceDurationDisplay { get; init; } = "—";
    public string Notes { get; init; } = "—";
    public IReadOnlyList<string> VisitHistory { get; init; } = [];
}

public class VisitAppointmentItem
{
    public Guid Id { get; init; }
    public string VisitorName { get; init; } = string.Empty;
    public string HostName { get; init; } = string.Empty;
    public string ScheduledDisplay { get; init; } = string.Empty;
    public string Room { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string StatusBadgeBackground { get; init; } = "#DBEAFE";
    public string StatusBadgeForeground { get; init; } = "#1D4ED8";
    public string DurationDisplay { get; init; } = "—";
    public string Purpose { get; init; } = "—";
}

public class VisitAlertItem
{
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string AccentColor { get; init; } = "#DC2626";
    public string Background { get; init; } = "#FEE2E2";
}

public class AccessZoneItem
{
    public string ZoneName { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = "Ouvert";
    public string StatusColor { get; init; } = "#166534";
    public string StatusBackground { get; init; } = "#DCFCE7";
    public int ActiveCount { get; init; }
    public string Detail { get; init; } = string.Empty;
}

public class VisitInsightLine
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Accent { get; init; } = "#2563EB";
}

public class VisitDayPoint
{
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
}

public class VisitTypeSlice
{
    public string Type { get; init; } = string.Empty;
    public int Count { get; init; }
}

public class VisitAccessSlice
{
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
}

public class VisitHourPoint
{
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
}
