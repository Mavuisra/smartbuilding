using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Desktop.WPF.Helpers;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class FinancesViewModel : BaseViewModel
{
    private readonly FinancesService _financesService;
    private readonly FinancesReportPdfService _financesPdf;
    private readonly ISyncService _syncService;
    private readonly ShellNavigationService _shellNavigation;
    private readonly AppConfigurationService _appConfiguration;
    private List<FinanceTransactionItem> _allTransactions = [];
    private FinancePageData? _pageData;

    public const string AllPeriods = "Ce mois";
    public const string AllTypes = "Tous types";
    public const string AllCategories = "Toutes catégories";
    public const string AllSources = "Toutes sources";
    public const string AllStatuses = "Tous statuts";

    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _userRole = string.Empty;
    [ObservableProperty] private string _userInitials = "AD";
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _tableSearchQuery = string.Empty;
    [ObservableProperty] private string _filterPeriod = AllPeriods;
    [ObservableProperty] private string _filterType = AllTypes;
    [ObservableProperty] private string _filterCategory = AllCategories;
    [ObservableProperty] private string _filterSource = AllSources;
    [ObservableProperty] private string _filterStatus = AllStatuses;
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private string _paginationText = string.Empty;
    [ObservableProperty] private int _notificationCount = 6;
    [ObservableProperty] private int _pageSize = 10;
    [ObservableProperty] private int _filteredTotal;
    [ObservableProperty] private int _selectedMainTab;

    [ObservableProperty] private string _monthlyRevenueDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _monthlyExpensesDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _netProfitDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _revenueTrend = "+0%";
    [ObservableProperty] private string _expenseTrend = "+0%";
    [ObservableProperty] private string _profitTrend = "+0%";
    [ObservableProperty] private string _rentCollectedDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _rentCollectedPercent = "0%";
    [ObservableProperty] private string _rentLateDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _rentLatePercent = "0%";
    [ObservableProperty] private string _treasuryDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _rentCollectedTotalDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _availableBalanceDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _pendingInvoicesDisplay = "0";
    [ObservableProperty] private string _pendingAmountDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _maintenanceDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _lateRentTotalDisplay = MoneyFormatter.ZeroDisplay;

    [ObservableProperty] private ISeries[] _revenueExpenseSeries = [];
    [ObservableProperty] private ISeries[] _expensePieSeries = [];
    [ObservableProperty] private ISeries[] _rentBarSeries = [];

    public ObservableCollection<FinanceTransactionItem> Transactions { get; } = [];
    public ObservableCollection<FinanceTransactionItem> PendingInvoices { get; } = [];
    public ObservableCollection<FinanceAlertItem> Alerts { get; } = [];
    public ObservableCollection<FinanceTreasuryLine> TreasuryLines { get; } = [];
    public ObservableCollection<FinanceLateRentItem> LateRents { get; } = [];
    public ObservableCollection<string> PeriodFilters { get; } = [AllPeriods, "3 derniers mois", "12 derniers mois", "Toute la période"];
    public ObservableCollection<string> TypeFilters { get; } = [AllTypes, "Revenu", "Dépense"];
    public ObservableCollection<string> CategoryFilters { get; } = [AllCategories];
    public ObservableCollection<string> SourceFilters { get; } = [AllSources];
    public ObservableCollection<string> StatusFilters { get; } =
        [AllStatuses, "Payé", "En attente", "En retard", "Impayé", "En attente validation PDG"];
    public ObservableCollection<int> PageSizeOptions { get; } = [10, 20, 50];
    public ObservableCollection<string> MainTabs { get; } =
        ["Toutes", "Revenus", "Dépenses", "Loyers", "Factures", "Remboursements"];
    public FinancesViewModel(
        FinancesService financesService,
        FinancesReportPdfService financesPdf,
        ISyncService syncService,
        ShellNavigationService shellNavigation,
        AppConfigurationService appConfiguration,
        SessionService session)
    {
        _financesService = financesService;
        _financesPdf = financesPdf;
        _syncService = syncService;
        _shellNavigation = shellNavigation;
        _appConfiguration = appConfiguration;
        _appConfiguration.ConfigurationChanged += (_, _) => _ = LoadAsync();
        UserName = session.CurrentUser?.FullName ?? "Admin Principal";
        UserRole = session.CurrentUser?.Role ?? "Administrateur";
        UserInitials = GetInitials(UserName);
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var data = await _financesService.LoadAsync();
            _pageData = data;
            _allTransactions = data.Transactions.ToList();

            MonthlyRevenueDisplay = Fc(data.RentCollected);
            MonthlyExpensesDisplay = Fc(data.MonthlyExpenses);
            NetProfitDisplay = Fc(data.NetProfit);
            RevenueTrend = data.RevenueTrend;
            ExpenseTrend = data.ExpenseTrend;
            ProfitTrend = data.ProfitTrend;
            RentCollectedDisplay = Fc(data.RentCollected);
            RentCollectedPercent = data.RentCollectedPercent;
            RentLateDisplay = Fc(data.RentLate);
            RentLatePercent = data.RentLatePercent;
            TreasuryDisplay = Fc(data.TreasuryBalance);
            RentCollectedTotalDisplay = Fc(data.RentCollectedTotal);
            AvailableBalanceDisplay = Fc(data.AvailableBalance);
            PendingInvoicesDisplay = data.PendingInvoices.ToString();
            PendingAmountDisplay = Fc(data.PendingInvoicesAmount);
            MaintenanceDisplay = Fc(data.MaintenanceCost);
            LateRentTotalDisplay = Fc(data.RentLate);
            NotificationCount = data.Alerts.Count(a => a.Severity is "Warning" or "Error");

            Alerts.Clear();
            foreach (var a in data.Alerts) Alerts.Add(a);

            TreasuryLines.Clear();
            foreach (var t in data.TreasuryLines) TreasuryLines.Add(t);

            LateRents.Clear();
            foreach (var r in data.LateRents) LateRents.Add(r);

            PendingInvoices.Clear();
            foreach (var invoice in data.Transactions
                         .Where(t => t.Category.Contains("Facture", StringComparison.OrdinalIgnoreCase) &&
                                     t.StatusLabel is "En attente" or "En retard" or "Impayé" or "En attente validation PDG")
                         .OrderByDescending(t => t.TransactionDate)
                         .Take(8))
            {
                PendingInvoices.Add(invoice);
            }

            CategoryFilters.Clear();
            CategoryFilters.Add(AllCategories);
            foreach (var c in data.Categories) CategoryFilters.Add(c);

            SourceFilters.Clear();
            SourceFilters.Add(AllSources);
            foreach (var s in data.Sources) SourceFilters.Add(s);

            BuildCharts(data);
            CurrentPage = 1;
            ApplyFilters();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SetMainTab(object? parameter)
    {
        SelectedMainTab = TabNavigationHelper.ParseIndex(parameter);
        CurrentPage = 1;
        ApplyFilters();
    }

    [RelayCommand]
    private async Task CollectRent() => await _shellNavigation.OpenRentFormAsync();

    [RelayCommand]
    private async Task SyncAsync()
    {
        IsBusy = true;
        StatusMessage = "Synchronisation...";
        try
        {
            var result = await _syncService.SyncAsync(manual: true);
            StatusMessage = result.Success
                ? $"Sync OK — {result.Pushed} envoyés, {result.Pulled} reçus"
                : $"Échec : {result.Error}";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ExportCsv()
    {
        var list = GetFilteredTransactions();
        if (list.Count == 0)
        {
            StatusMessage = "Aucune transaction à exporter avec les filtres actuels.";
            return;
        }

        var path = FinancesExportService.ExportCsv(list);
        TryOpenFile(path);
        StatusMessage = $"Export Excel enregistré ({list.Count} lignes).";
    }

    [RelayCommand]
    private void ExportPdf()
    {
        var list = GetFilteredTransactions();
        if (list.Count == 0)
        {
            StatusMessage = "Aucune transaction à exporter avec les filtres actuels.";
            return;
        }

        if (_pageData is null)
        {
            StatusMessage = "Données financières non chargées.";
            return;
        }

        var path = _financesPdf.ExportTransactionsReport(_pageData, list, "Rapport financier SBMS");
        TryOpenFile(path);
        StatusMessage = $"Export PDF généré ({list.Count} transactions).";
    }

    [RelayCommand]
    private void PrintReport()
    {
        var list = GetFilteredTransactions();
        if (list.Count == 0)
        {
            StatusMessage = "Aucune transaction à imprimer avec les filtres actuels.";
            return;
        }

        StatusMessage = FinancesExportService.PrintTransactionsList(list, "SBMS — Finances")
            ? $"Impression envoyée ({list.Count} lignes)."
            : "Impression annulée.";
    }

    private static void TryOpenFile(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch
        {
            // Fichier créé ; ouverture OS optionnelle
        }
    }

    [RelayCommand]
    private void ViewPendingInvoice(FinanceTransactionItem? invoice)
    {
        if (invoice is null)
            return;

        SelectedMainTab = 4;
        FilterStatus = AllStatuses;
        TableSearchQuery = invoice.Reference;
        StatusMessage = $"Facture {invoice.Reference} affichée pour contrôle.";
    }

    [RelayCommand]
    private async Task ValidatePendingInvoiceAsync(FinanceTransactionItem? invoice)
    {
        if (invoice is null)
            return;

        IsBusy = true;
        try
        {
            var error = await _financesService.ApprovePendingInvoiceAsync(invoice.Id, UserName);
            if (!string.IsNullOrWhiteSpace(error))
            {
                StatusMessage = error;
                return;
            }

            StatusMessage = $"Facture {invoice.Reference} validée.";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            ApplyFilters();
        }
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            ApplyFilters();
        }
    }

    partial void OnSearchQueryChanged(string value) => ResetPageAndFilter();
    partial void OnTableSearchQueryChanged(string value) => ResetPageAndFilter();
    partial void OnFilterPeriodChanged(string value) => ResetPageAndFilter();
    partial void OnFilterTypeChanged(string value) => ResetPageAndFilter();
    partial void OnFilterCategoryChanged(string value) => ResetPageAndFilter();
    partial void OnFilterSourceChanged(string value) => ResetPageAndFilter();
    partial void OnFilterStatusChanged(string value) => ResetPageAndFilter();
    partial void OnPageSizeChanged(int value) => ResetPageAndFilter();
    partial void OnSelectedMainTabChanged(int value) => ResetPageAndFilter();

    private void ResetPageAndFilter()
    {
        CurrentPage = 1;
        ApplyFilters();
    }

    private List<FinanceTransactionItem> GetFilteredTransactions()
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var query = $"{SearchQuery} {TableSearchQuery}".Trim();

        IEnumerable<FinanceTransactionItem> filtered = _allTransactions;

        filtered = SelectedMainTab == 4
            ? filtered
            : ApplyPeriodFilter(filtered, monthStart);
        filtered = filtered.Where(MatchesTab);
        filtered = filtered.Where(t =>
            (FilterType == AllTypes || t.TypeLabel == FilterType) &&
            (FilterCategory == AllCategories || t.Category == FilterCategory) &&
            (FilterSource == AllSources || t.Source == FilterSource) &&
            (FilterStatus == AllStatuses || t.StatusLabel == FilterStatus) &&
            (string.IsNullOrWhiteSpace(query) ||
             t.Reference.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             t.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             t.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             t.Source.Contains(query, StringComparison.OrdinalIgnoreCase)));

        return filtered.OrderByDescending(t => t.TransactionDate).ToList();
    }

    private void ApplyFilters()
    {
        var list = GetFilteredTransactions();
        FilteredTotal = list.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling(list.Count / (double)PageSize));
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;

        var skip = (CurrentPage - 1) * PageSize;
        var page = list.Skip(skip).Take(PageSize).ToList();

        Transactions.Clear();
        foreach (var t in page) Transactions.Add(t);

        var start = list.Count == 0 ? 0 : skip + 1;
        var end = skip + page.Count;
        PaginationText = $"Affichage de {start} à {end} sur {list.Count} transaction(s)";
    }

    private IEnumerable<FinanceTransactionItem> ApplyPeriodFilter(IEnumerable<FinanceTransactionItem> items, DateTime monthStart)
    {
        var today = DateTime.Today;
        return FilterPeriod switch
        {
            "3 derniers mois" => items.Where(t => t.TransactionDate >= monthStart.AddMonths(-2)),
            "12 derniers mois" => items.Where(t => t.TransactionDate >= monthStart.AddMonths(-11)),
            "Toute la période" => items,
            _ => items.Where(t => t.TransactionDate >= monthStart && t.TransactionDate <= today)
        };
    }

    private bool MatchesTab(FinanceTransactionItem t) => SelectedMainTab switch
    {
        1 => t.IsRevenue,
        2 => !t.IsRevenue,
        3 => t.IsRent || t.IsGuarantee,
        4 => t.Category.Contains("Facture", StringComparison.OrdinalIgnoreCase),
        5 => t.IsRefund || t.Category.Contains("Remboursement", StringComparison.OrdinalIgnoreCase),
        _ => true
    };

    private void BuildCharts(FinancePageData data)
    {
        RevenueExpenseSeries =
        [
            new LineSeries<decimal>
            {
                Name = "Revenus",
                Values = data.RevenueVsExpenseTrend.Select(p => p.Revenue).ToArray(),
                Stroke = new SolidColorPaint(SKColor.Parse("#2D6A4F")) { StrokeThickness = 2 },
                Fill = null,
                GeometrySize = 4
            },
            new LineSeries<decimal>
            {
                Name = "Dépenses",
                Values = data.RevenueVsExpenseTrend.Select(p => p.Expense).ToArray(),
                Stroke = new SolidColorPaint(SKColor.Parse("#DC2626")) { StrokeThickness = 2 },
                Fill = null,
                GeometrySize = 4
            }
        ];

        var palette = new[] { "#2D6A4F", "#40916C", "#52B788", "#74C69D", "#EA580C", "#DC2626" };
        ExpensePieSeries = data.ExpenseBreakdown.Select((s, i) => new PieSeries<decimal>
        {
            Name = s.Category,
            Values = [s.Amount],
            Fill = new SolidColorPaint(SKColor.Parse(palette[i % palette.Length]))
        }).Cast<ISeries>().ToArray();

        RentBarSeries =
        [
            new ColumnSeries<decimal>
            {
                Name = "Prévus",
                Values = [data.RentBarPlanned],
                Fill = new SolidColorPaint(SKColor.Parse("#95D5B2"))
            },
            new ColumnSeries<decimal>
            {
                Name = "Collectés",
                Values = [data.RentBarCollected],
                Fill = new SolidColorPaint(SKColor.Parse("#2D6A4F"))
            },
            new ColumnSeries<decimal>
            {
                Name = "En retard",
                Values = [data.RentBarLate],
                Fill = new SolidColorPaint(SKColor.Parse("#DC2626"))
            }
        ];
    }

    private static string Fc(decimal amount) => MoneyFormatter.Format(amount);

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
            : name.Length >= 2 ? name[..2].ToUpperInvariant() : "AD";
    }
}
