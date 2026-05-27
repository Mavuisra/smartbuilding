using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class UsersViewModel : BaseViewModel
{
    private readonly UsersModuleService _usersService;
    private readonly SessionService _session;
    private List<UserListItem> _allUsers = [];
    private string _locationLabel = "—";

    public const string AllRoles = "Tous les rôles";

    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _userRole = string.Empty;
    [ObservableProperty] private string _userInitials = "AD";
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _filterRole = AllRoles;
    [ObservableProperty] private UserListItem? _selectedUser;
    [ObservableProperty] private int _selectedDetailTab;
    [ObservableProperty] private int _pageSize = 8;
    [ObservableProperty] private int _notificationCount;

    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _administratorsCount;
    [ObservableProperty] private int _activeCount;
    [ObservableProperty] private int _suspendedCount;
    [ObservableProperty] private int _loginsTodayCount;
    [ObservableProperty] private int _activeSessionsCount;
    [ObservableProperty] private string _totalTrend = "—";
    [ObservableProperty] private string _administratorsTrend = "—";
    [ObservableProperty] private string _activeTrend = "—";
    [ObservableProperty] private string _suspendedTrend = "—";
    [ObservableProperty] private string _loginsTodayTrend = "—";
    [ObservableProperty] private string _activeSessionsTrend = "Temps réel";

    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private int _filteredTotal;
    [ObservableProperty] private string _paginationDisplay = string.Empty;
    [ObservableProperty] private double _activeUsersPercent;

    [ObservableProperty] private ISeries[] _totalSparkline = [];
    [ObservableProperty] private ISeries[] _administratorsSparkline = [];
    [ObservableProperty] private ISeries[] _activeSparkline = [];
    [ObservableProperty] private ISeries[] _suspendedSparkline = [];
    [ObservableProperty] private ISeries[] _loginsSparkline = [];
    [ObservableProperty] private ISeries[] _sessionsSparkline = [];
    [ObservableProperty] private ISeries[] _loginTrendSeries = [];
    [ObservableProperty] private ISeries[] _rolePieSeries = [];
    [ObservableProperty] private ISeries[] _statusPieSeries = [];

    public ObservableCollection<UserListItem> PagedUsers { get; } = [];
    public ObservableCollection<string> RoleFilters { get; } = [AllRoles];
    public ObservableCollection<int> PageSizeOptions { get; } = [8, 10, 25];
    public ObservableCollection<int> PageNumbers { get; } = [];
    public ObservableCollection<UserActivityItem> UserActivities { get; } = [];
    public ObservableCollection<UserSessionItem> UserSessions { get; } = [];
    public ObservableCollection<UserPermissionItem> UserPermissions { get; } = [];
    public ObservableCollection<UserRecentSignupItem> RecentSignups { get; } = [];

    public UsersViewModel(UsersModuleService usersService, SessionService session)
    {
        _usersService = usersService;
        _session = session;
        UserName = session.CurrentUser?.FullName ?? "Admin SBMS";
        UserRole = session.CurrentUser?.Role ?? "Administrateur";
        UserInitials = GetInitials(UserName);
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var data = await _usersService.LoadAsync(_session.CurrentUser?.UserId);
            _allUsers = data.Users.ToList();

            TotalCount = data.TotalCount;
            AdministratorsCount = data.AdministratorsCount;
            ActiveCount = data.ActiveCount;
            SuspendedCount = data.SuspendedCount;
            LoginsTodayCount = data.LoginsTodayCount;
            ActiveSessionsCount = data.ActiveSessionsCount;
            TotalTrend = data.TotalTrend;
            AdministratorsTrend = data.AdministratorsTrend;
            ActiveTrend = data.ActiveTrend;
            SuspendedTrend = data.SuspendedTrend;
            LoginsTodayTrend = data.LoginsTodayTrend;
            ActiveSessionsTrend = data.ActiveSessionsTrend;
            NotificationCount = data.SuspendedCount;
            ActiveUsersPercent = data.TotalCount == 0 ? 0 : Math.Round(data.ActiveCount * 100.0 / data.TotalCount, 0);

            RoleFilters.Clear();
            foreach (var r in data.RoleFilters) RoleFilters.Add(r);

            RecentSignups.Clear();
            foreach (var s in data.RecentSignups) RecentSignups.Add(s);

            _locationLabel = data.DefaultLocation;

            BuildSparklines(data);
            BuildCharts(data);

            ApplyFilters();
            SelectedUser ??= PagedUsers.FirstOrDefault();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private void SelectUser(UserListItem? user)
    {
        foreach (var u in _allUsers) u.IsSelected = false;
        if (user is not null) user.IsSelected = true;
        SelectedUser = user;
    }

    [RelayCommand]
    private void SetDetailTab(object? parameter)
    {
        if (parameter is int i)
            SelectedDetailTab = i;
        else if (int.TryParse(parameter?.ToString(), out var p))
            SelectedDetailTab = p;
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage < TotalPages) { CurrentPage++; ApplyFilters(skipResetPage: true); }
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CurrentPage > 1) { CurrentPage--; ApplyFilters(skipResetPage: true); }
    }

    [RelayCommand]
    private void GoToPage(int page)
    {
        if (page < 1 || page > TotalPages) return;
        CurrentPage = page;
        ApplyFilters(skipResetPage: true);
    }

    partial void OnSelectedUserChanged(UserListItem? value) => _ = LoadUserDetailAsync(value);

    partial void OnSearchQueryChanged(string value) { CurrentPage = 1; ApplyFilters(); }
    partial void OnFilterRoleChanged(string value) { CurrentPage = 1; ApplyFilters(); }
    partial void OnPageSizeChanged(int value) { CurrentPage = 1; ApplyFilters(); }

    private async Task LoadUserDetailAsync(UserListItem? user)
    {
        UserActivities.Clear();
        UserSessions.Clear();
        UserPermissions.Clear();
        if (user is null) return;

        var activities = await _usersService.LoadActivitiesAsync(user);
        foreach (var a in activities) UserActivities.Add(a);

        var sessions = await _usersService.LoadSessionsAsync(user, _locationLabel);
        foreach (var s in sessions) UserSessions.Add(s);

        var perms = await _usersService.LoadPermissionsAsync(user.Id);
        foreach (var p in perms) UserPermissions.Add(p);
    }

    private void ApplyFilters(bool skipResetPage = false)
    {
        var query = SearchQuery.Trim().ToLowerInvariant();
        IEnumerable<UserListItem> filtered = _allUsers;

        if (FilterRole != AllRoles)
            filtered = filtered.Where(u => u.RoleLabel.Equals(FilterRole, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(u =>
                u.FullName.ToLowerInvariant().Contains(query) ||
                u.Email.ToLowerInvariant().Contains(query) ||
                u.Username.ToLowerInvariant().Contains(query) ||
                u.Department.ToLowerInvariant().Contains(query));
        }

        var list = filtered.OrderByDescending(u => u.IsOnline).ThenBy(u => u.FullName).ToList();
        FilteredTotal = list.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling(FilteredTotal / (double)PageSize));
        if (!skipResetPage && CurrentPage > TotalPages) CurrentPage = 1;

        PagedUsers.Clear();
        foreach (var u in list.Skip((CurrentPage - 1) * PageSize).Take(PageSize))
            PagedUsers.Add(u);

        var from = FilteredTotal == 0 ? 0 : (CurrentPage - 1) * PageSize + 1;
        var to = Math.Min(CurrentPage * PageSize, FilteredTotal);
        PaginationDisplay = FilteredTotal == 0
            ? "Aucun utilisateur"
            : $"Affichage {from} à {to} sur {FilteredTotal:N0} utilisateurs";

        PageNumbers.Clear();
        for (var i = 1; i <= Math.Min(TotalPages, 7); i++)
            PageNumbers.Add(i);

        if (SelectedUser is not null && !list.Any(x => x.Id == SelectedUser.Id))
            SelectedUser = PagedUsers.FirstOrDefault();
    }

    private void BuildSparklines(UsersPageData data)
    {
        TotalSparkline = BuildSparkline(data.TotalSparkline, "#2563EB");
        AdministratorsSparkline = BuildSparkline(data.AdministratorsSparkline, "#6D28D9");
        ActiveSparkline = BuildSparkline(data.ActiveSparkline, "#2D6A4F");
        SuspendedSparkline = BuildSparkline(data.SuspendedSparkline, "#EA580C");
        LoginsSparkline = BuildSparkline(data.LoginsSparkline, "#2563EB");
        SessionsSparkline = BuildSparkline(data.SessionsSparkline, "#DC2626");
    }

    private void BuildCharts(UsersPageData data)
    {
        var palette = new[] { "#DC2626", "#2563EB", "#6D28D9", "#EA580C", "#2D6A4F", "#64748B" };

        LoginTrendSeries =
        [
            new LineSeries<int>
            {
                Name = "Connexions",
                Values = data.LoginTrend.Select(p => p.Count).ToArray(),
                Stroke = new SolidColorPaint(SKColor.Parse("#2563EB")) { StrokeThickness = 2 },
                Fill = new SolidColorPaint(SKColor.Parse("#2563EB").WithAlpha(50)),
                GeometrySize = 4
            }
        ];

        RolePieSeries = data.RoleDistribution.Select((s, i) => new PieSeries<int>
        {
            Name = s.Role,
            Values = [s.Count],
            Fill = new SolidColorPaint(SKColor.Parse(palette[i % palette.Length]))
        }).Cast<ISeries>().ToArray();

        var statusColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Actif"] = "#22C55E",
            ["Suspendu"] = "#EF4444",
            ["Inactif"] = "#94A3B8"
        };
        StatusPieSeries = data.StatusDistribution.Select(s => new PieSeries<int>
        {
            Name = s.Status,
            Values = [s.Count],
            Fill = new SolidColorPaint(SKColor.Parse(statusColors.GetValueOrDefault(s.Status, "#64748B")))
        }).Cast<ISeries>().ToArray();
    }

    private static ISeries[] BuildSparkline(IReadOnlyList<int> values, string color) =>
    [
        new LineSeries<int>
        {
            Values = values.Count == 0 ? [0] : values.ToArray(),
            Stroke = new SolidColorPaint(SKColor.Parse(color)) { StrokeThickness = 2 },
            Fill = new SolidColorPaint(SKColor.Parse(color).WithAlpha(40)),
            GeometrySize = 0,
            LineSmoothness = 0.6
        }
    ];

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
            : name.Length >= 2 ? name[..2].ToUpperInvariant() : "AD";
    }
}
