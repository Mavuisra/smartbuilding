using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartBuilding.Desktop.WPF.Models;

public class UsersPageData
{
    public int TotalCount { get; init; }
    public int AdministratorsCount { get; init; }
    public int ActiveCount { get; init; }
    public int SuspendedCount { get; init; }
    public int LoginsTodayCount { get; init; }
    public int ActiveSessionsCount { get; init; }
    public string TotalTrend { get; init; } = "—";
    public string AdministratorsTrend { get; init; } = "—";
    public string ActiveTrend { get; init; } = "—";
    public string SuspendedTrend { get; init; } = "—";
    public string LoginsTodayTrend { get; init; } = "—";
    public string ActiveSessionsTrend { get; init; } = "Temps réel";
    public IReadOnlyList<int> TotalSparkline { get; init; } = [];
    public IReadOnlyList<int> AdministratorsSparkline { get; init; } = [];
    public IReadOnlyList<int> ActiveSparkline { get; init; } = [];
    public IReadOnlyList<int> SuspendedSparkline { get; init; } = [];
    public IReadOnlyList<int> LoginsSparkline { get; init; } = [];
    public IReadOnlyList<int> SessionsSparkline { get; init; } = [];
    public IReadOnlyList<UserListItem> Users { get; init; } = [];
    public IReadOnlyList<UserRoleSlice> RoleDistribution { get; init; } = [];
    public IReadOnlyList<UserStatusSlice> StatusDistribution { get; init; } = [];
    public IReadOnlyList<UserDayPoint> LoginTrend { get; init; } = [];
    public IReadOnlyList<UserRecentSignupItem> RecentSignups { get; init; } = [];
    public IReadOnlyList<string> RoleFilters { get; init; } = [];
    public string DefaultLocation { get; init; } = "—";
}

public partial class UserListItem : ObservableObject
{
    public Guid Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string JobTitle { get; init; } = string.Empty;
    public string RoleLabel { get; init; } = string.Empty;
    public string RoleBadgeBackground { get; init; } = "#F1F5F9";
    public string RoleBadgeForeground { get; init; } = "#475569";
    public string Department { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = "Actif";
    public string StatusDotColor { get; init; } = "#22C55E";
    public bool IsActive { get; init; }
    public bool IsOnline { get; init; }
    public string LastLoginDisplay { get; init; } = "Jamais";
    public string CreatedAtDisplay { get; init; } = string.Empty;
    public string Phone { get; init; } = "—";
    public string Initials { get; init; } = "??";
    public string AvatarBackground { get; init; } = "#DBEAFE";
    public string AvatarForeground { get; init; } = "#2563EB";
    public string OnlineStatusLabel { get; init; } = "Hors ligne";
    public string OnlineStatusColor { get; init; } = "#94A3B8";
    [ObservableProperty] private bool _isSelected;
}

public class UserActivityItem
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string TimeDisplay { get; init; } = string.Empty;
    public string IconKind { get; init; } = "InformationOutline";
    public string IconColor { get; init; } = "#2563EB";
}

public class UserSessionItem
{
    public string DeviceLabel { get; init; } = string.Empty;
    public string ClientInfo { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = "Actif";
    public string IconKind { get; init; } = "Monitor";
}

public class UserPermissionItem
{
    public string Name { get; init; } = string.Empty;
    public string Module { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
}

public class UserRoleSlice
{
    public string Role { get; init; } = string.Empty;
    public int Count { get; init; }
}

public class UserStatusSlice
{
    public string Status { get; init; } = string.Empty;
    public int Count { get; init; }
}

public class UserDayPoint
{
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
}

public class UserRecentSignupItem
{
    public string FullName { get; init; } = string.Empty;
    public string RoleLabel { get; init; } = string.Empty;
    public string DateDisplay { get; init; } = string.Empty;
    public string Initials { get; init; } = "??";
    public string AvatarBackground { get; init; } = "#DBEAFE";
    public string AvatarForeground { get; init; } = "#2563EB";
}
