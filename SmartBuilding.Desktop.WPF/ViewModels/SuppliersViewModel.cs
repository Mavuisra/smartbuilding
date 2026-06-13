using System.Collections.ObjectModel;
using System.Globalization;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Domain.Entities.Suppliers;
using SmartBuilding.Desktop.WPF.Helpers;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class SuppliersViewModel : BaseViewModel
{
    private readonly SuppliersService _suppliersService;
    private readonly ISyncService _syncService;
    private List<SupplierListItem> _allSuppliers = [];

    public const string AllCategories = "Toutes catégories";
    public const string AllStatuses = "Tous statuts";
    public const string AllServiceTypes = "Tous types service";
    public const string AllContracts = "Tous contrats";
    public const string AllBuildings = "Tous bâtiments";

    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _userRole = string.Empty;
    [ObservableProperty] private string _userInitials = "AD";
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _tableSearchQuery = string.Empty;
    [ObservableProperty] private string _filterCategory = AllCategories;
    [ObservableProperty] private string _filterStatus = AllStatuses;
    [ObservableProperty] private string _filterServiceType = AllServiceTypes;
    [ObservableProperty] private string _filterContract = AllContracts;
    [ObservableProperty] private string _filterBuilding = AllBuildings;
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private string _paginationText = string.Empty;
    [ObservableProperty] private int _notificationCount = 6;
    [ObservableProperty] private int _pageSize = 10;
    [ObservableProperty] private int _filteredTotal;
    [ObservableProperty] private bool _isDetailPanelOpen;
    [ObservableProperty] private int _selectedDetailTab;
    [ObservableProperty] private bool _isAddFormOpen;
    [ObservableProperty] private SupplierListItem? _selectedSupplier;

    [ObservableProperty] private int _totalSuppliers;
    [ObservableProperty] private int _activeSuppliers;
    [ObservableProperty] private string _activePercent = "0%";
    [ObservableProperty] private int _unpaidInvoices;
    [ObservableProperty] private string _monthlyExpensesDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _availableBalanceDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private int _contractsExpiringSoon;
    [ObservableProperty] private int _interventionsThisMonth;

    [ObservableProperty] private string _formCode = string.Empty;
    [ObservableProperty] private string _formName = string.Empty;
    [ObservableProperty] private string _formCategory = "Maintenance";
    [ObservableProperty] private string _formPhone = string.Empty;
    [ObservableProperty] private string _formEmail = string.Empty;
    [ObservableProperty] private string _formContact = string.Empty;
    [ObservableProperty] private string? _formError;

    [ObservableProperty] private ISeries[] _expensePieSeries = [];
    [ObservableProperty] private ISeries[] _expenseTrendSeries = [];
    [ObservableProperty] private ISeries[] _topSuppliersSeries = [];

    public ObservableCollection<SupplierListItem> Suppliers { get; } = [];
    public ObservableCollection<SupplierAlertItem> Alerts { get; } = [];
    public ObservableCollection<string> CategoryFilters { get; } = [AllCategories];
    public ObservableCollection<string> StatusFilters { get; } = [AllStatuses, "Actif", "Expiré", "En attente"];
    public ObservableCollection<string> ServiceTypeFilters { get; } = [AllServiceTypes];
    public ObservableCollection<string> ContractFilters { get; } = [AllContracts, "Contrat actif", "Sans contrat", "Expire bientôt"];
    public ObservableCollection<string> BuildingFilters { get; } = [AllBuildings];
    public ObservableCollection<int> PageSizeOptions { get; } = [10, 20, 50];
    public ObservableCollection<string> SupplierCategories { get; } =
        ["Maintenance", "Sécurité", "Nettoyage", "Énergie", "Plomberie", "Électricité", "Services", "Autre"];

    public SuppliersViewModel(
        SuppliersService suppliersService,
        ISyncService syncService,
        AppConfigurationService appConfiguration,
        SessionService session)
    {
        _suppliersService = suppliersService;
        _syncService = syncService;
        appConfiguration.ConfigurationChanged += (_, _) => _ = LoadAsync();
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
            var data = await _suppliersService.LoadAsync();
            _allSuppliers = data.Suppliers.ToList();

            TotalSuppliers = data.TotalSuppliers;
            ActiveSuppliers = data.ActiveSuppliers;
            ActivePercent = data.ActivePercent;
            UnpaidInvoices = data.UnpaidInvoices;
            MonthlyExpensesDisplay = Fc(data.MonthlyExpenses);
            AvailableBalanceDisplay = Fc(data.AvailableBalance);
            ContractsExpiringSoon = data.ContractsExpiringSoon;
            InterventionsThisMonth = data.InterventionsThisMonth;
            NotificationCount = data.Alerts.Count(a => a.Title != "Aucune alerte");

            Alerts.Clear();
            foreach (var a in data.Alerts) Alerts.Add(a);

            CategoryFilters.Clear();
            CategoryFilters.Add(AllCategories);
            foreach (var c in _allSuppliers.Select(s => s.Category).Where(x => x != "—").Distinct().OrderBy(x => x))
                CategoryFilters.Add(c);

            ServiceTypeFilters.Clear();
            ServiceTypeFilters.Add(AllServiceTypes);
            foreach (var t in _allSuppliers.Select(s => s.ServiceType).Where(x => x != "—").Distinct().OrderBy(x => x))
                ServiceTypeFilters.Add(t);

            BuildingFilters.Clear();
            BuildingFilters.Add(AllBuildings);
            foreach (var b in _allSuppliers.Select(s => s.Building).Where(x => x != "—").Distinct().OrderBy(x => x))
                BuildingFilters.Add(b);

            FilterCategory = PageFilterHelper.RestoreSelection(FilterCategory, CategoryFilters, AllCategories);
            FilterServiceType = PageFilterHelper.RestoreSelection(FilterServiceType, ServiceTypeFilters, AllServiceTypes);
            FilterBuilding = PageFilterHelper.RestoreSelection(FilterBuilding, BuildingFilters, AllBuildings);
            FilterStatus = PageFilterHelper.RestoreSelection(FilterStatus, StatusFilters, AllStatuses);
            FilterContract = PageFilterHelper.RestoreSelection(FilterContract, ContractFilters, AllContracts);

            BuildCharts(data);
            CurrentPage = 1;
            ApplyFilters();
            if (SelectedSupplier is null || !_allSuppliers.Any(s => s.Id == SelectedSupplier.Id))
                SelectedSupplier = Suppliers.FirstOrDefault();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CloseDetailPanel()
    {
        IsDetailPanelOpen = false;
        SelectedSupplier = null;
    }

    [RelayCommand]
    private void SetDetailTab(object? parameter) => SelectedDetailTab = TabNavigationHelper.ParseIndex(parameter);

    [RelayCommand]
    private void OpenAddForm()
    {
        FormCode = $"FRN-{DateTime.Today:yyyyMM}-{_allSuppliers.Count + 1:D3}";
        FormName = string.Empty;
        FormCategory = "Maintenance";
        FormPhone = string.Empty;
        FormEmail = string.Empty;
        FormContact = string.Empty;
        FormError = null;
        IsAddFormOpen = true;
    }

    [RelayCommand]
    private void CloseAddForm() => IsAddFormOpen = false;

    [RelayCommand]
    private async Task SaveSupplierAsync()
    {
        FormError = null;
        IsBusy = true;
        try
        {
            var error = await _suppliersService.CreateSupplierAsync(new Supplier
            {
                Code = FormCode,
                Name = FormName,
                Category = FormCategory,
                Phone = FormPhone,
                Email = FormEmail,
                ContactName = FormContact,
                Status = "Actif",
                ServiceType = "Prestation",
                Building = "Tour SBMS"
            });

            if (!string.IsNullOrEmpty(error))
            {
                FormError = error;
                return;
            }

            IsAddFormOpen = false;
            StatusMessage = "Fournisseur enregistré.";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private void ExportCsv()
    {
        if (_allSuppliers.Count == 0)
        {
            ErrorMessage = "Aucune donnée à exporter.";
            return;
        }

        var path = SuppliersExportService.ExportCsv(_allSuppliers);
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
        StatusMessage = $"Export : {path}";
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task SyncAsync()
    {
        IsBusy = true;
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
    private void PreviousPage()
    {
        if (CurrentPage > 1) { CurrentPage--; ApplyFilters(); }
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage < TotalPages) { CurrentPage++; ApplyFilters(); }
    }

    partial void OnSelectedSupplierChanged(SupplierListItem? value) => IsDetailPanelOpen = value is not null;
    partial void OnSearchQueryChanged(string value) => ResetPageAndFilter();
    partial void OnTableSearchQueryChanged(string value) => ResetPageAndFilter();
    partial void OnFilterCategoryChanged(string value) => ResetPageAndFilter();
    partial void OnFilterStatusChanged(string value) => ResetPageAndFilter();
    partial void OnFilterServiceTypeChanged(string value) => ResetPageAndFilter();
    partial void OnFilterContractChanged(string value) => ResetPageAndFilter();
    partial void OnFilterBuildingChanged(string value) => ResetPageAndFilter();
    partial void OnPageSizeChanged(int value) => ResetPageAndFilter();

    private void ResetPageAndFilter()
    {
        CurrentPage = 1;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var today = DateTime.Today;
        var query = $"{SearchQuery} {TableSearchQuery}".Trim();

        var filtered = _allSuppliers.Where(s =>
            PageFilterHelper.Matches(FilterCategory, AllCategories, s.Category) &&
            PageFilterHelper.Matches(FilterStatus, AllStatuses, s.StatusLabel) &&
            PageFilterHelper.Matches(FilterServiceType, AllServiceTypes, s.ServiceType) &&
            PageFilterHelper.Matches(FilterBuilding, AllBuildings, s.Building) &&
            (PageFilterHelper.IsAll(FilterContract, AllContracts)
                || (FilterContract == "Contrat actif" && s.ContractDisplay != "—")
                || (FilterContract == "Sans contrat" && s.ContractDisplay == "—")
                || (FilterContract == "Expire bientôt" && s.ContractStatus == "Actif"
                    && DateTime.TryParseExact(s.ContractEndDisplay, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end)
                    && end <= today.AddDays(30))) &&
            (string.IsNullOrWhiteSpace(query) ||
             s.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             s.Code.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             s.Email.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             s.Category.Contains(query, StringComparison.OrdinalIgnoreCase)));

        var list = filtered.ToList();
        FilteredTotal = list.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling(list.Count / (double)PageSize));
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;

        var skip = (CurrentPage - 1) * PageSize;
        var page = list.Skip(skip).Take(PageSize).ToList();

        Suppliers.Clear();
        foreach (var s in page) Suppliers.Add(s);

        var start = list.Count == 0 ? 0 : skip + 1;
        var end = skip + page.Count;
        PaginationText = $"Affichage de {start} à {end} sur {list.Count} fournisseur(s)";
    }

    private void BuildCharts(SuppliersPageData data)
    {
        var palette = new[] { "#2D6A4F", "#2563EB", "#EA580C", "#6D28D9", "#DC2626", "#64748B" };
        ExpensePieSeries = data.ExpenseByCategory.Select((s, i) => new PieSeries<decimal>
        {
            Name = s.Category,
            Values = [s.Amount],
            Fill = new SolidColorPaint(SKColor.Parse(palette[i % palette.Length]))
        }).Cast<ISeries>().ToArray();

        ExpenseTrendSeries =
        [
            new LineSeries<decimal>
            {
                Name = "Dépenses",
                Values = data.ExpenseTrend.Select(p => p.Amount).ToArray(),
                Stroke = new SolidColorPaint(SKColor.Parse("#2563EB")) { StrokeThickness = 2 },
                Fill = null,
                GeometrySize = 5
            }
        ];

        TopSuppliersSeries =
        [
            new ColumnSeries<decimal>
            {
                Name = "Top fournisseurs",
                Values = data.TopExpensive.Select(t => t.Amount).ToArray(),
                Fill = new SolidColorPaint(SKColor.Parse("#2D6A4F"))
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
