using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Desktop.WPF.Services;
using SmartBuilding.Shared.DTOs.Dashboard;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class DashboardViewModel : BaseViewModel
{
    private static readonly string[] ChartPalette =
        ["#2D6A4F", "#40916C", "#52B788", "#EA580C", "#DC2626", "#7B2CBF", "#0077B6", "#F77F00"];

    private readonly IDashboardService _dashboardService;
    private readonly ISyncService _syncService;
    private readonly SessionService _session;
    private readonly AppConfigurationService _appConfiguration;

    [ObservableProperty] private DashboardSummaryDto _summary = new();
    [ObservableProperty] private string _monthlyRevenueDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _rentCollectedDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _rentSubtitleDisplay = "—";
    [ObservableProperty] private string _rentLateDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _netBalanceDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _availableBalanceDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _rentCollectedTotalDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _monthlyExpensesDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _expensesSubtitleDisplay = "—";
    [ObservableProperty] private string _balanceSubtitleDisplay = "—";
    [ObservableProperty] private string _rentCollectionRateDisplay = "0 %";
    [ObservableProperty] private double _rentCollectionRate;
    [ObservableProperty] private ISeries[] _financeTrendSeries = [];
    [ObservableProperty] private ISeries[] _revenueExpenseSeries = [];
    [ObservableProperty] private ISeries[] _topExpenseSeries = [];
    [ObservableProperty] private ISeries[] _expensePieSeries = [];
    [ObservableProperty] private ISeries[] _rentCollectionSeries = [];
    [ObservableProperty] private ISeries[] _occupancySeries = [];
    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _userRole = string.Empty;
    [ObservableProperty] private string _userInitials = "AD";
    [ObservableProperty] private int _notificationCount;
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
    [ObservableProperty] private bool _isNotificationsOpen;

    private List<RecentMovementDto> _allRecentMovements = [];
    private List<ActivityItemDto> _allRecentActivity = [];
    private List<DashboardAlertDto> _allAlerts = [];

    public ObservableCollection<DashboardAlertDto> Alerts { get; } = [];
    public ObservableCollection<RecentMovementDto> RecentMovements { get; } = [];
    public ObservableCollection<ActivityItemDto> RecentActivity { get; } = [];
    public ObservableCollection<QuickStatDto> QuickStats { get; } = [];

    public DashboardViewModel(
        IDashboardService dashboardService,
        ISyncService syncService,
        AppConfigurationService appConfiguration,
        SessionService session)
    {
        _dashboardService = dashboardService;
        _syncService = syncService;
        _appConfiguration = appConfiguration;
        _appConfiguration.ConfigurationChanged += (_, _) => _ = LoadAsync();
        _session = session;
        UserName = session.CurrentUser?.FullName ?? "Utilisateur";
        UserRole = session.CurrentUser?.Role ?? "";
        UserInitials = GetInitials(UserName);
        NotificationCount = 0;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            Summary = await _dashboardService.GetSummaryAsync();
            MonthlyRevenueDisplay = Fc(Summary.RentCollectedTotal);
            RentCollectedDisplay = Fc(Summary.RentCollected);
            RentSubtitleDisplay = Summary.RentPlanned > 0
                ? $"Ce mois : {Fc(Summary.RentCollected)} / {Fc(Summary.RentPlanned)} prévus"
                : Summary.RentCollected > 0
                    ? $"Ce mois : {Fc(Summary.RentCollected)}"
                    : "Aucun loyer prévu ce mois";
            RentLateDisplay = Summary.RentLateAmount > 0 ? Fc(Summary.RentLateAmount) : Summary.LatePayments.ToString();
            AvailableBalanceDisplay = Fc(Summary.AvailableBalance);
            NetBalanceDisplay = Fc(Summary.AvailableBalance);
            BalanceSubtitleDisplay = Summary.AvailableThisMonth != Summary.AvailableBalance
                ? $"Ce mois : {Fc(Summary.AvailableThisMonth)} · {Fc(Summary.RentCollectedTotal)} − {Fc(Summary.TotalExpensesAllTime)}"
                : $"{Fc(Summary.RentCollectedTotal)} − {Fc(Summary.TotalExpensesAllTime)}";
            RentCollectedTotalDisplay = Fc(Summary.RentCollectedTotal);
            MonthlyExpensesDisplay = Fc(Summary.TotalExpensesAllTime);
            ExpensesSubtitleDisplay = Summary.MonthlyExpenses > 0
                ? $"Ce mois : {Fc(Summary.MonthlyExpenses)}"
                : "Aucune dépense ce mois";
            RentCollectionRate = Summary.RentPlanned > 0
                ? Math.Min(100, (double)(Summary.RentCollected / Summary.RentPlanned) * 100)
                : Summary.RentCollected > 0 ? 100 : 0;
            RentCollectionRateDisplay = $"{RentCollectionRate:F0} %";
            _allAlerts = Summary.Alerts.ToList();
            _allRecentMovements = Summary.RecentMovements.ToList();
            _allRecentActivity = Summary.RecentActivity.ToList();
            NotificationCount = _allAlerts.Count(a => a.Severity is "Warning" or "Error");

            QuickStats.Clear();
            foreach (var s in Summary.QuickStats) QuickStats.Add(s);

            BuildCharts();
            ApplyFilters();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void BuildCharts()
    {
        var trendValues = Summary.FinanceTrendChart.Select(c => c.Value).ToArray();
        FinanceTrendSeries =
        [
            new LineSeries<decimal>
            {
                Name = "Solde net (7 j.)",
                Values = trendValues,
                Fill = new SolidColorPaint(SKColor.Parse("#2D6A4F").WithAlpha(48)),
                Stroke = new SolidColorPaint(SKColor.Parse("#1B4332")) { StrokeThickness = 3 },
                GeometryFill = new SolidColorPaint(SKColor.Parse("#2D6A4F")),
                GeometryStroke = new SolidColorPaint(SKColor.Parse("#1B4332")),
                GeometrySize = 8,
                LineSmoothness = 0.35
            }
        ];

        var revenueValues = Summary.RevenueChart.Select(c => c.Value).ToArray();
        var expenseValues = Summary.ExpenseChart.Select(c => c.Value).ToArray();
        RevenueExpenseSeries =
        [
            new ColumnSeries<decimal>
            {
                Name = "Loyers encaissés",
                Values = revenueValues,
                Fill = new SolidColorPaint(SKColor.Parse("#2D6A4F")),
                MaxBarWidth = 28
            },
            new ColumnSeries<decimal>
            {
                Name = "Dépenses",
                Values = expenseValues,
                Fill = new SolidColorPaint(SKColor.Parse("#DC2626")),
                MaxBarWidth = 28
            }
        ];

        var topCategories = Summary.TopExpenseCategories;
        if (topCategories.Count > 0)
        {
            TopExpenseSeries =
            [
                new ColumnSeries<decimal>
                {
                    Name = "Dépenses (mois)",
                    Values = topCategories.Select(c => c.Value).ToArray(),
                    Fill = new SolidColorPaint(SKColor.Parse("#40916C")),
                    MaxBarWidth = 36
                }
            ];
            ExpensePieSeries = topCategories.Select((c, i) => new PieSeries<decimal>
            {
                Name = c.Label,
                Values = [c.Value],
                Fill = new SolidColorPaint(SKColor.Parse(ChartPalette[i % ChartPalette.Length]))
            }).Cast<ISeries>().ToArray();
        }
        else
        {
            TopExpenseSeries =
            [
                new ColumnSeries<decimal>
                {
                    Name = "Dépenses",
                    Values = [0m],
                    Fill = new SolidColorPaint(SKColor.Parse("#CBD5E1"))
                }
            ];
            ExpensePieSeries =
            [
                new PieSeries<decimal>
                {
                    Name = "Aucune dépense",
                    Values = [1m],
                    Fill = new SolidColorPaint(SKColor.Parse("#E2E8F0"))
                }
            ];
        }

        RentCollectionSeries =
        [
            new ColumnSeries<decimal>
            {
                Name = "Encaissé",
                Values = [Summary.RentCollected],
                Fill = new SolidColorPaint(SKColor.Parse("#2D6A4F")),
                MaxBarWidth = 48
            },
            new ColumnSeries<decimal>
            {
                Name = "Prévu",
                Values = [Summary.RentPlanned],
                Fill = new SolidColorPaint(SKColor.Parse("#95D5B2")),
                MaxBarWidth = 48
            },
            new ColumnSeries<decimal>
            {
                Name = "Retard",
                Values = [Summary.RentLateAmount],
                Fill = new SolidColorPaint(SKColor.Parse("#DC2626")),
                MaxBarWidth = 48
            }
        ];

        var free = Summary.TotalPremises - Summary.OccupiedPremises;
        OccupancySeries =
        [
            new PieSeries<double>
            {
                Name = "Occupés",
                Values = [Summary.OccupiedPremises],
                Fill = new SolidColorPaint(SKColor.Parse("#2D6A4F")),
                InnerRadius = 42
            },
            new PieSeries<double>
            {
                Name = "Libres",
                Values = [Math.Max(free, 0)],
                Fill = new SolidColorPaint(SKColor.Parse("#D8F3DC")),
                InnerRadius = 42
            }
        ];
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilters();

    partial void OnSelectedDateChanged(DateTime value) => ApplyFilters();

    [RelayCommand]
    private void OpenNotifications()
    {
        if (Alerts.Count == 0)
        {
            SbmsDialogService.ShowInfo("Notifications", "Aucune alerte pour la date sélectionnée.");
            return;
        }

        IsNotificationsOpen = true;
    }

    [RelayCommand]
    private void CloseNotifications() => IsNotificationsOpen = false;

    private void ApplyFilters()
    {
        var query = SearchQuery.Trim();
        var date = SelectedDate.Date;

        var movements = _allRecentMovements.Where(m => m.Date.Date == date);
        var activity = _allRecentActivity.Where(a => a.Timestamp.Date == date);
        var alerts = _allAlerts.Where(a => a.Timestamp.Date == date);

        if (!string.IsNullOrEmpty(query))
        {
            movements = movements.Where(m => MatchesMovement(m, query));
            activity = activity.Where(a => MatchesActivity(a, query));
            alerts = alerts.Where(a => MatchesAlert(a, query));
        }

        var movementList = movements.ToList();
        var activityList = activity.ToList();
        var alertList = alerts.ToList();

        Alerts.Clear();
        foreach (var a in alertList) Alerts.Add(a);

        RecentMovements.Clear();
        foreach (var m in movementList) RecentMovements.Add(m);

        RecentActivity.Clear();
        foreach (var a in activityList) RecentActivity.Add(a);

        NotificationCount = alertList.Count(a => a.Severity is "Warning" or "Error");

        if (!string.IsNullOrEmpty(query))
        {
            StatusMessage = movementList.Count + activityList.Count + alertList.Count > 0
                ? $"Recherche « {query} » — {movementList.Count} mouvement(s), {activityList.Count} activité(s), {alertList.Count} alerte(s)"
                : $"Aucun résultat pour « {query} » le {date:dd/MM/yyyy}";
        }
        else if (date != DateTime.Today)
        {
            StatusMessage = $"{movementList.Count} mouvement(s), {activityList.Count} activité(s) — {date:dd/MM/yyyy}";
        }
        else
        {
            StatusMessage = null;
        }
    }

    private static bool MatchesMovement(RecentMovementDto m, string query) =>
        m.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        m.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        m.Reference.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        m.Type.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        m.AmountDisplay.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesActivity(ActivityItemDto a, string query) =>
        a.Text.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesAlert(DashboardAlertDto a, string query) =>
        a.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        a.Message.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        a.Severity.Contains(query, StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        IsBusy = true;
        StatusMessage = "Synchronisation en cours...";
        try
        {
            var result = await _syncService.SyncAsync(manual: true);
            StatusMessage = result.Success
                ? $"Synchronisation OK — {result.Pushed} envoyés, {result.Pulled} reçus"
                : $"Échec : {result.Error}";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string Fc(decimal amount) => MoneyFormatter.Format(amount);

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[^1][0]}".ToUpper()
            : name.Length >= 2 ? name[..2].ToUpper() : "SB";
    }
}
