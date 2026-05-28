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
    [ObservableProperty] private ISeries[] _financeTrendSeries = [];
    [ObservableProperty] private ISeries[] _topExpenseSeries = [];
    [ObservableProperty] private ISeries[] _occupancySeries = [];
    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _userRole = string.Empty;
    [ObservableProperty] private string _userInitials = "AD";
    [ObservableProperty] private int _notificationCount;
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;

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
            MonthlyRevenueDisplay = Fc(Summary.RentCollected);
            RentCollectedDisplay = Fc(Summary.RentCollected);
            RentSubtitleDisplay = Summary.RentPlanned > 0
                ? $"{Fc(Summary.RentCollected)} / {Fc(Summary.RentPlanned)} prévus"
                : "Aucun loyer prévu ce mois";
            RentLateDisplay = Summary.RentLateAmount > 0 ? Fc(Summary.RentLateAmount) : Summary.LatePayments.ToString();
            NetBalanceDisplay = Fc(Summary.NetBalance);
            AvailableBalanceDisplay = Fc(Summary.AvailableThisMonth);
            RentCollectedTotalDisplay = Fc(Summary.RentCollectedTotal);
            NotificationCount = Summary.Alerts.Count(a => a.Severity is "Warning" or "Error");

            Alerts.Clear();
            foreach (var a in Summary.Alerts) Alerts.Add(a);

            RecentMovements.Clear();
            foreach (var m in Summary.RecentMovements) RecentMovements.Add(m);

            RecentActivity.Clear();
            foreach (var a in Summary.RecentActivity) RecentActivity.Add(a);

            QuickStats.Clear();
            foreach (var s in Summary.QuickStats) QuickStats.Add(s);

            FinanceTrendSeries =
            [
                new LineSeries<decimal>
                {
                    Name = "Solde net",
                    Values = Summary.FinanceTrendChart.Select(c => c.Value).ToArray(),
                    Fill = null,
                    Stroke = new SolidColorPaint(SKColor.Parse("#2D6A4F")) { StrokeThickness = 3 },
                    GeometryFill = new SolidColorPaint(SKColor.Parse("#2D6A4F")),
                    GeometryStroke = new SolidColorPaint(SKColor.Parse("#2D6A4F"))
                }
            ];

            var expenseValues = Summary.TopExpenseCategories.Count > 0
                ? Summary.TopExpenseCategories.Select(c => c.Value).ToArray()
                : new decimal[] { 0 };

            TopExpenseSeries =
            [
                new RowSeries<decimal>
                {
                    Name = "Dépenses",
                    Values = expenseValues,
                    Fill = new SolidColorPaint(SKColor.Parse("#40916C"))
                }
            ];

            var free = Summary.TotalPremises - Summary.OccupiedPremises;
            OccupancySeries =
            [
                new PieSeries<double> { Name = "Occupés", Values = [Summary.OccupiedPremises], Fill = new SolidColorPaint(SKColor.Parse("#2D6A4F")) },
                new PieSeries<double> { Name = "Libres", Values = [Math.Max(free, 0)], Fill = new SolidColorPaint(SKColor.Parse("#95D5B2")) }
            ];
        }
        finally
        {
            IsBusy = false;
        }
    }

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
