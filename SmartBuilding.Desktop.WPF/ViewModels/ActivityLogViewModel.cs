using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class ActivityLogViewModel : BaseViewModel
{
    private const int DefaultPageSize = 10;
    private readonly ActivityLogModuleService _activityLogService;
    private List<ActivityLogListItem> _allActivities = [];

    public const string AllTypes = "Tous les types";
    public const string AllModules = "Tous les modules";
    public const string AllUsers = "Tous les utilisateurs";
    public const string AllStatuses = "Tous les statuts";

    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _userInitials = "AD";
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _filterType = AllTypes;
    [ObservableProperty] private string _filterModule = AllModules;
    [ObservableProperty] private string _filterUser = AllUsers;
    [ObservableProperty] private string _filterStatus = AllStatuses;
    [ObservableProperty] private ActivityLogListItem? _selectedActivity;
    [ObservableProperty] private int _selectedDetailTab;
    [ObservableProperty] private int _pageSize = DefaultPageSize;
    [ObservableProperty] private int _notificationCount;

    [ObservableProperty] private int _activitiesToday;
    [ObservableProperty] private int _loginsCount;
    [ObservableProperty] private int _modificationsCount;
    [ObservableProperty] private int _securityAlertsCount;
    [ObservableProperty] private int _systemErrorsCount;
    [ObservableProperty] private int _syncCount;
    [ObservableProperty] private string _activitiesTodayTrend = "—";
    [ObservableProperty] private string _loginsTrend = "—";
    [ObservableProperty] private string _modificationsTrend = "—";
    [ObservableProperty] private string _securityAlertsTrend = "—";
    [ObservableProperty] private string _systemErrorsTrend = "—";
    [ObservableProperty] private string _syncTrend = "—";
    [ObservableProperty] private string _dateRangeDisplay = string.Empty;

    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private int _filteredTotal;
    [ObservableProperty] private string _paginationDisplay = string.Empty;

    [ObservableProperty] private ISeries[] _activitiesSparkline = [];
    [ObservableProperty] private ISeries[] _loginsSparkline = [];
    [ObservableProperty] private ISeries[] _modificationsSparkline = [];
    [ObservableProperty] private ISeries[] _securitySparkline = [];
    [ObservableProperty] private ISeries[] _errorsSparkline = [];
    [ObservableProperty] private ISeries[] _syncSparkline = [];

    public ObservableCollection<ActivityLogListItem> PagedActivities { get; } = [];
    public ObservableCollection<string> TypeFilters { get; } = [AllTypes];
    public ObservableCollection<string> ModuleFilters { get; } = [AllModules];
    public ObservableCollection<string> UserFilters { get; } = [AllUsers];
    public ObservableCollection<string> StatusFilters { get; } = [AllStatuses];
    public ObservableCollection<int> PageSizeOptions { get; } = [10, 25, 50];
    public ObservableCollection<int> PageNumbers { get; } = [];
    public ObservableCollection<ActivityLogRelatedItem> RelatedActivities { get; } = [];

    public ActivityLogViewModel(ActivityLogModuleService activityLogService, SessionService session)
    {
        _activityLogService = activityLogService;
        UserName = session.CurrentUser?.FullName ?? "Admin SBMS";
        UserInitials = GetInitials(UserName);
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var data = await _activityLogService.LoadAsync();
            _allActivities = data.Activities.ToList();

            ActivitiesToday = data.ActivitiesToday;
            LoginsCount = data.LoginsCount;
            ModificationsCount = data.ModificationsCount;
            SecurityAlertsCount = data.SecurityAlertsCount;
            SystemErrorsCount = data.SystemErrorsCount;
            SyncCount = data.SyncCount;
            ActivitiesTodayTrend = data.ActivitiesTodayTrend;
            LoginsTrend = data.LoginsTrend;
            ModificationsTrend = data.ModificationsTrend;
            SecurityAlertsTrend = data.SecurityAlertsTrend;
            SystemErrorsTrend = data.SystemErrorsTrend;
            SyncTrend = data.SyncTrend;
            NotificationCount = data.SecurityAlertsCount + data.SystemErrorsCount;

            DateRangeDisplay = $"{data.DateRangeStart:dd MMM yyyy} - {data.DateRangeEnd:dd MMM yyyy}";

            TypeFilters.Clear();
            foreach (var t in data.TypeFilters) TypeFilters.Add(t);
            ModuleFilters.Clear();
            foreach (var m in data.ModuleFilters) ModuleFilters.Add(m);
            UserFilters.Clear();
            foreach (var u in data.UserFilters) UserFilters.Add(u);
            StatusFilters.Clear();
            foreach (var s in data.StatusFilters) StatusFilters.Add(s);

            FilterType = AllTypes;
            FilterModule = AllModules;
            FilterUser = AllUsers;
            FilterStatus = AllStatuses;

            BuildSparklines(data);
            ApplyFilters();
            SelectedActivity ??= PagedActivities.FirstOrDefault();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private void ResetFilters()
    {
        SearchQuery = string.Empty;
        FilterType = AllTypes;
        FilterModule = AllModules;
        FilterUser = AllUsers;
        FilterStatus = AllStatuses;
        CurrentPage = 1;
        ApplyFilters();
    }

    [RelayCommand]
    private void ClearSelection() => SelectedActivity = null;

    [RelayCommand]
    private void SelectActivity(ActivityLogListItem? item)
    {
        foreach (var a in _allActivities) a.IsSelected = false;
        if (item is not null) item.IsSelected = true;
        SelectedActivity = item;
    }

    [RelayCommand]
    private void SetDetailTab(object? parameter)
    {
        if (parameter is int i) SelectedDetailTab = i;
        else if (int.TryParse(parameter?.ToString(), out var p)) SelectedDetailTab = p;
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

    partial void OnSelectedActivityChanged(ActivityLogListItem? value) => LoadRelated(value);

    partial void OnSearchQueryChanged(string value) { CurrentPage = 1; ApplyFilters(); }
    partial void OnFilterTypeChanged(string value) { CurrentPage = 1; ApplyFilters(); }
    partial void OnFilterModuleChanged(string value) { CurrentPage = 1; ApplyFilters(); }
    partial void OnFilterUserChanged(string value) { CurrentPage = 1; ApplyFilters(); }
    partial void OnFilterStatusChanged(string value) { CurrentPage = 1; ApplyFilters(); }
    partial void OnPageSizeChanged(int value) { CurrentPage = 1; ApplyFilters(); }

    private void LoadRelated(ActivityLogListItem? item)
    {
        RelatedActivities.Clear();
        if (item is null) return;
        foreach (var r in _activityLogService.BuildRelatedActivities(item, _allActivities))
            RelatedActivities.Add(r);
    }

    private void ApplyFilters(bool skipResetPage = false)
    {
        var query = SearchQuery.Trim().ToLowerInvariant();
        IEnumerable<ActivityLogListItem> filtered = _allActivities;

        if (FilterType != AllTypes)
            filtered = filtered.Where(a => a.ActivityType.Equals(FilterType, StringComparison.OrdinalIgnoreCase));
        if (FilterModule != AllModules)
            filtered = filtered.Where(a => a.Module.Equals(FilterModule, StringComparison.OrdinalIgnoreCase));
        if (FilterUser != AllUsers)
            filtered = filtered.Where(a => a.UserName.Equals(FilterUser, StringComparison.OrdinalIgnoreCase));
        if (FilterStatus != AllStatuses)
            filtered = filtered.Where(a => a.StatusLabel.Equals(FilterStatus, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(a =>
                a.ActionTitle.ToLowerInvariant().Contains(query) ||
                a.ActionDescription.ToLowerInvariant().Contains(query) ||
                a.Details.ToLowerInvariant().Contains(query) ||
                a.UserName.ToLowerInvariant().Contains(query) ||
                a.Module.ToLowerInvariant().Contains(query));
        }

        var list = filtered.OrderByDescending(a => a.OccurredAt).ToList();
        FilteredTotal = list.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling(FilteredTotal / (double)PageSize));
        if (!skipResetPage && CurrentPage > TotalPages) CurrentPage = 1;

        PagedActivities.Clear();
        foreach (var a in list.Skip((CurrentPage - 1) * PageSize).Take(PageSize))
            PagedActivities.Add(a);

        var from = FilteredTotal == 0 ? 0 : (CurrentPage - 1) * PageSize + 1;
        var to = Math.Min(CurrentPage * PageSize, FilteredTotal);
        PaginationDisplay = FilteredTotal == 0
            ? "Aucune activité"
            : $"Affichage de {from} à {to} sur {FilteredTotal:N0} activités";

        PageNumbers.Clear();
        for (var i = 1; i <= Math.Min(TotalPages, 7); i++)
            PageNumbers.Add(i);

        if (SelectedActivity is not null && !list.Any(x => x.Id == SelectedActivity.Id))
        {
            SelectedActivity = PagedActivities.FirstOrDefault();
            LoadRelated(SelectedActivity);
        }
    }

    private void BuildSparklines(ActivityLogPageData data)
    {
        ActivitiesSparkline = BuildSparkline(data.ActivitiesSparkline, "#2563EB");
        LoginsSparkline = BuildSparkline(data.LoginsSparkline, "#2D6A4F");
        ModificationsSparkline = BuildSparkline(data.ModificationsSparkline, "#EA580C");
        SecuritySparkline = BuildSparkline(data.SecuritySparkline, "#DC2626");
        ErrorsSparkline = BuildSparkline(data.ErrorsSparkline, "#6D28D9");
        SyncSparkline = BuildSparkline(data.SyncSparkline, "#0EA5E9");
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
