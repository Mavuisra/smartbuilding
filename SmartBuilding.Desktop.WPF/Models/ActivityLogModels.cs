using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartBuilding.Desktop.WPF.Models;

public class ActivityLogPageData
{
    public int ActivitiesToday { get; init; }
    public int LoginsCount { get; init; }
    public int ModificationsCount { get; init; }
    public int SecurityAlertsCount { get; init; }
    public int SystemErrorsCount { get; init; }
    public int SyncCount { get; init; }
    public string ActivitiesTodayTrend { get; init; } = "—";
    public string LoginsTrend { get; init; } = "—";
    public string ModificationsTrend { get; init; } = "—";
    public string SecurityAlertsTrend { get; init; } = "—";
    public string SystemErrorsTrend { get; init; } = "—";
    public string SyncTrend { get; init; } = "—";
    public IReadOnlyList<int> ActivitiesSparkline { get; init; } = [];
    public IReadOnlyList<int> LoginsSparkline { get; init; } = [];
    public IReadOnlyList<int> ModificationsSparkline { get; init; } = [];
    public IReadOnlyList<int> SecuritySparkline { get; init; } = [];
    public IReadOnlyList<int> ErrorsSparkline { get; init; } = [];
    public IReadOnlyList<int> SyncSparkline { get; init; } = [];
    public IReadOnlyList<ActivityLogListItem> Activities { get; init; } = [];
    public IReadOnlyList<string> TypeFilters { get; init; } = [];
    public IReadOnlyList<string> ModuleFilters { get; init; } = [];
    public IReadOnlyList<string> UserFilters { get; init; } = [];
    public IReadOnlyList<string> StatusFilters { get; init; } = [];
    public DateTime DateRangeStart { get; init; }
    public DateTime DateRangeEnd { get; init; }
}

public partial class ActivityLogListItem : ObservableObject
{
    public Guid Id { get; init; }
    public string ActivityCode { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; }
    public string TimeDisplay { get; init; } = string.Empty;
    public string DateDisplay { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string UserRole { get; init; } = string.Empty;
    public string UserInitials { get; init; } = "??";
    public string AvatarBackground { get; init; } = "#DBEAFE";
    public string AvatarForeground { get; init; } = "#2563EB";
    public string ActionTitle { get; init; } = string.Empty;
    public string ActionDescription { get; init; } = string.Empty;
    public string ActivityType { get; init; } = string.Empty;
    public string Module { get; init; } = string.Empty;
    public string ModuleBadgeBackground { get; init; } = "#F1F5F9";
    public string ModuleBadgeForeground { get; init; } = "#475569";
    public string Details { get; init; } = string.Empty;
    public string DeviceInfo { get; init; } = "SBMS Desktop";
    public string IpAddress { get; init; } = "—";
    public string StatusLabel { get; init; } = "Succès";
    public string StatusDotColor { get; init; } = "#22C55E";
    public string IconKind { get; init; } = "InformationOutline";
    public string IconColor { get; init; } = "#2563EB";
    public string TitleForeground { get; init; } = "#1B3D3B";
    public string FileName { get; init; } = "—";
    public string FilePath { get; init; } = "—";
    public string Browser { get; init; } = "SBMS Desktop";
    public string Location { get; init; } = "—";
    public string OldValues { get; init; } = "—";
    public string NewValues { get; init; } = "—";
    [ObservableProperty] private bool _isSelected;
}

public class ActivityLogRelatedItem
{
    public string Title { get; init; } = string.Empty;
    public string TimeDisplay { get; init; } = string.Empty;
    public string IconKind { get; init; } = "InformationOutline";
}
