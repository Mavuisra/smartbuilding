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
using SmartBuilding.Domain.Entities.Consumption;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Desktop.WPF.Helpers;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class ConsumptionsViewModel : BaseViewModel
{
    private readonly ConsumptionsService _consumptionsService;
    private readonly ISyncService _syncService;
    private readonly ConsumptionsReportPdfService _consumptionsPdf = new();
    private List<ConsumptionListItem> _allRecords = [];

    public const string AllPeriods = "Ce mois";
    public const string AllTypes = "Tous types";
    public const string AllBuildings = "Tous bâtiments";
    public const string AllEquipment = "Tous équipements";
    public const string AllAnomalies = "Toutes";
    public const string AllStatuses = "Tous statuts";

    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _userRole = string.Empty;
    [ObservableProperty] private string _userInitials = "AD";
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _tableSearchQuery = string.Empty;
    [ObservableProperty] private string _filterPeriod = AllPeriods;
    [ObservableProperty] private string _filterType = AllTypes;
    [ObservableProperty] private string _filterBuilding = AllBuildings;
    [ObservableProperty] private string _filterEquipment = AllEquipment;
    [ObservableProperty] private string _filterAnomaly = AllAnomalies;
    [ObservableProperty] private string _filterStatus = AllStatuses;
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private string _paginationText = string.Empty;
    [ObservableProperty] private int _notificationCount = 5;
    [ObservableProperty] private int _pageSize = 10;
    [ObservableProperty] private int _filteredTotal;
    [ObservableProperty] private bool _isDetailPanelOpen;
    [ObservableProperty] private int _selectedDetailTab;
    [ObservableProperty] private bool _isAddFormOpen;
    [ObservableProperty] private bool _isRecordDetailsOpen;
    [ObservableProperty] private ConsumptionListItem? _selectedRecord;
    [ObservableProperty] private string _syncStatusLabel = "Hors ligne";
    [ObservableProperty] private string _lastSyncDisplay = "Dernière sync : —";

    [ObservableProperty] private string _electricityDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _waterBillDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _fuelCostDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _internetCostDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _totalEnergyCostDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _availableBalanceDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _monthlyVariationDisplay = "0%";
    [ObservableProperty] private string _monthlyVariationTrend = "—";
    [ObservableProperty] private string _topConsumer = "—";
    [ObservableProperty] private string _averageMonthlyCostDisplay = "—";
    [ObservableProperty] private string _consumptionTrendLabel = "—";
    [ObservableProperty] private string _futureEstimateDisplay = "—";
    [ObservableProperty] private string _savingsDisplay = "—";

    [ObservableProperty] private string _formType = "Électricité";
    [ObservableProperty] private string _formEquipment = string.Empty;
    [ObservableProperty] private string _formCostText = "0";
    [ObservableProperty] private string _formPeriodType = "Mensuel";
    [ObservableProperty] private string? _formError;

    [ObservableProperty] private ISeries[] _monthlyTrendSeries = [];
    [ObservableProperty] private ISeries[] _energyPieSeries = [];
    [ObservableProperty] private ISeries[] _costBarSeries = [];
    [ObservableProperty] private ISeries[] _compareSeries = [];

    public ObservableCollection<ConsumptionListItem> Records { get; } = [];
    public ObservableCollection<ConsumptionAlertItem> Alerts { get; } = [];
    public ObservableCollection<ConsumptionInsightLine> Insights { get; } = [];
    public ObservableCollection<string> PeriodFilters { get; } = [AllPeriods, "7 derniers jours", "3 derniers mois", "12 derniers mois", "Année en cours"];
    public ObservableCollection<string> TypeFilters { get; } = [AllTypes];
    public ObservableCollection<string> BuildingFilters { get; } = [AllBuildings];
    public ObservableCollection<string> EquipmentFilters { get; } = [AllEquipment];
    public ObservableCollection<string> AnomalyFilters { get; } = [AllAnomalies, "Anomalies uniquement", "Sans anomalie"];
    public ObservableCollection<string> StatusFilters { get; } = [AllStatuses, "Normal", "Élevé", "Critique", "Économie"];
    public ObservableCollection<int> PageSizeOptions { get; } = [10, 20, 50];
    public ObservableCollection<string> ConsumptionTypes { get; } =
    [
        "Électricité", "Eau", "Carburant générateur", "Internet", "Climatisation",
        "Éclairage", "Groupe électrogène", "Réseau technique", "Énergie"
    ];
    public ObservableCollection<string> PeriodTypes { get; } = ["Journalier", "Hebdomadaire", "Mensuel", "Annuel"];

    public ConsumptionsViewModel(
        ConsumptionsService consumptionsService,
        ISyncService syncService,
        AppConfigurationService appConfiguration,
        SessionService session)
    {
        _consumptionsService = consumptionsService;
        _syncService = syncService;
        appConfiguration.ConfigurationChanged += (_, _) => _ = LoadAsync();
        UserName = session.CurrentUser?.FullName ?? "Admin Principal";
        UserRole = session.CurrentUser?.Role ?? "Administrateur";
        UserInitials = GetInitials(UserName);
        UpdateSyncStatus();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var data = await _consumptionsService.LoadAsync();
            _allRecords = data.Records.ToList();

            ElectricityDisplay = data.ElectricityDisplay;
            WaterBillDisplay = data.WaterBillDisplay;
            FuelCostDisplay = data.FuelCostDisplay;
            InternetCostDisplay = data.InternetCostDisplay;
            TotalEnergyCostDisplay = data.TotalEnergyCostDisplay;
            AvailableBalanceDisplay = MoneyFormatter.Format(data.AvailableBalance);
            MonthlyVariationDisplay = data.MonthlyVariationDisplay;
            MonthlyVariationTrend = data.MonthlyVariationTrend;
            TopConsumer = data.TopConsumer;
            AverageMonthlyCostDisplay = data.AverageMonthlyCostDisplay;
            ConsumptionTrendLabel = data.ConsumptionTrendLabel;
            FutureEstimateDisplay = data.FutureEstimateDisplay;
            SavingsDisplay = data.SavingsDisplay;
            NotificationCount = data.Alerts.Count(a => a.Title != "Consommations sous contrôle");

            Alerts.Clear();
            foreach (var a in data.Alerts) Alerts.Add(a);

            Insights.Clear();
            Insights.Add(new ConsumptionInsightLine { Label = "Plus énergivore", Value = data.TopConsumer, Accent = "#DC2626" });
            Insights.Add(new ConsumptionInsightLine { Label = "Coût moyen / mois", Value = data.AverageMonthlyCostDisplay, Accent = "#2563EB" });
            Insights.Add(new ConsumptionInsightLine { Label = "Tendance", Value = data.ConsumptionTrendLabel, Accent = "#EA580C" });
            Insights.Add(new ConsumptionInsightLine { Label = "Estimation mois prochain", Value = data.FutureEstimateDisplay, Accent = "#6D28D9" });
            Insights.Add(new ConsumptionInsightLine { Label = "Économie réalisée", Value = data.SavingsDisplay, Accent = "#166534" });

            TypeFilters.Clear();
            TypeFilters.Add(AllTypes);
            foreach (var t in _allRecords.Select(r => r.TypeLabel).Distinct().OrderBy(x => x)) TypeFilters.Add(t);

            BuildingFilters.Clear();
            BuildingFilters.Add(AllBuildings);
            foreach (var b in _allRecords.Select(r => r.Building).Where(x => x != "—").Distinct().OrderBy(x => x)) BuildingFilters.Add(b);

            EquipmentFilters.Clear();
            EquipmentFilters.Add(AllEquipment);
            foreach (var e in _allRecords.Select(r => r.EquipmentSource).Distinct().OrderBy(x => x)) EquipmentFilters.Add(e);

            BuildCharts(data);
            UpdateSyncStatus();
            CurrentPage = 1;
            ApplyFilters();
            if (SelectedRecord is null || !_allRecords.Any(r => r.Id == SelectedRecord.Id))
                SelectedRecord = Records.FirstOrDefault();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand] private void CloseDetailPanel() { IsDetailPanelOpen = false; SelectedRecord = null; }
    [RelayCommand]
    private void SetDetailTab(object? parameter) => SelectedDetailTab = TabNavigationHelper.ParseIndex(parameter);

    [RelayCommand]
    private void OpenAddForm()
    {
        FormType = "Électricité";
        FormEquipment = "Compteur principal Tour SBMS";
        FormCostText = "0";
        FormPeriodType = "Mensuel";
        FormError = null;
        IsAddFormOpen = true;
    }

    [RelayCommand] private void CloseAddForm() => IsAddFormOpen = false;

    [RelayCommand]
    private async Task SaveRecordAsync()
    {
        FormError = null;
        if (!decimal.TryParse(FormCostText.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var cost))
        { FormError = "Montant invalide."; return; }
        if (cost <= 0)
        { FormError = "Le montant doit être supérieur à zéro."; return; }

        IsBusy = true;
        try
        {
            var type = ConsumptionsService.ParseType(FormType);
            var error = await _consumptionsService.CreateRecordAsync(new ConsumptionRecord
            {
                Type = type,
                PeriodStart = DateTime.Today.AddDays(-30),
                PeriodEnd = DateTime.Today,
                Quantity = cost,
                Unit = "USD",
                Cost = cost,
                Currency = "USD",
                EquipmentSource = FormEquipment,
                Building = "Tour SBMS",
                Responsible = "Paul Ngoy",
                Status = "Normal",
                PeriodType = FormPeriodType,
                VariationPercent = 0
            });
            if (!string.IsNullOrEmpty(error)) { FormError = error; return; }
            IsAddFormOpen = false;
            StatusMessage = "Consommation enregistrée.";
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private void ExportCsv()
    {
        if (_allRecords.Count == 0)
        {
            ErrorMessage = "Aucune donnée à exporter.";
            return;
        }

        var path = ConsumptionsExportService.ExportCsv(_allRecords);
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
        StatusMessage = $"Export : {path}";
        ErrorMessage = null;
    }

    [RelayCommand]
    private void OpenNewRecordForm() => OpenAddForm();

    [RelayCommand]
    private void OpenRecordHistory()
    {
        if (SelectedRecord is null)
            SelectedRecord = Records.FirstOrDefault() ?? _allRecords.FirstOrDefault();

        if (SelectedRecord is null)
        {
            ErrorMessage = "Aucune consommation disponible.";
            return;
        }

        IsDetailPanelOpen = true;
        SelectedDetailTab = 1;
        ErrorMessage = null;
    }

    [RelayCommand]
    private void OpenRecordDetails(ConsumptionListItem? item)
    {
        var target = item ?? SelectedRecord;
        if (target is null)
        {
            ErrorMessage = "Sélectionnez une consommation.";
            return;
        }

        SelectedRecord = target;
        IsRecordDetailsOpen = true;
        ErrorMessage = null;
    }

    [RelayCommand]
    private void CloseRecordDetails() => IsRecordDetailsOpen = false;

    [RelayCommand]
    private void GenerateReport()
    {
        var target = SelectedRecord ?? Records.FirstOrDefault() ?? _allRecords.FirstOrDefault();
        if (target is null)
        {
            ErrorMessage = "Aucune consommation disponible pour générer le rapport.";
            return;
        }

        var path = _consumptionsPdf.ExportRecordDetails(target);
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
        StatusMessage = $"Rapport généré : {path}";
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task SyncAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _syncService.SyncAsync(manual: true);
            StatusMessage = result.Success ? $"Sync OK — {result.Pushed}/{result.Pulled}" : $"Échec : {result.Error}";
            UpdateSyncStatus();
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand] private void PreviousPage() { if (CurrentPage > 1) { CurrentPage--; ApplyFilters(); } }
    [RelayCommand] private void NextPage() { if (CurrentPage < TotalPages) { CurrentPage++; ApplyFilters(); } }

    partial void OnSelectedRecordChanged(ConsumptionListItem? value) => IsDetailPanelOpen = value is not null;
    partial void OnSearchQueryChanged(string value) => ResetPageAndFilter();
    partial void OnTableSearchQueryChanged(string value) => ResetPageAndFilter();
    partial void OnFilterPeriodChanged(string value) => ResetPageAndFilter();
    partial void OnFilterTypeChanged(string value) => ResetPageAndFilter();
    partial void OnFilterBuildingChanged(string value) => ResetPageAndFilter();
    partial void OnFilterEquipmentChanged(string value) => ResetPageAndFilter();
    partial void OnFilterAnomalyChanged(string value) => ResetPageAndFilter();
    partial void OnFilterStatusChanged(string value) => ResetPageAndFilter();
    partial void OnPageSizeChanged(int value) => ResetPageAndFilter();

    private void ResetPageAndFilter() { CurrentPage = 1; ApplyFilters(); }

    private void ApplyFilters()
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var query = $"{SearchQuery} {TableSearchQuery}".Trim();

        var filtered = _allRecords.Where(r =>
            InPeriod(r.DateDisplay, monthStart, today) &&
            (FilterType == AllTypes || r.TypeLabel == FilterType) &&
            (FilterBuilding == AllBuildings || r.Building == FilterBuilding) &&
            (FilterEquipment == AllEquipment || r.EquipmentSource == FilterEquipment) &&
            (FilterStatus == AllStatuses || r.StatusLabel == FilterStatus) &&
            MatchesAnomaly(r) &&
            (string.IsNullOrWhiteSpace(query) ||
             r.TypeLabel.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             r.EquipmentSource.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             r.Responsible.Contains(query, StringComparison.OrdinalIgnoreCase)));

        var list = filtered.ToList();
        FilteredTotal = list.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling(list.Count / (double)PageSize));
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;

        var skip = (CurrentPage - 1) * PageSize;
        Records.Clear();
        foreach (var r in list.Skip(skip).Take(PageSize)) Records.Add(r);

        var start = list.Count == 0 ? 0 : skip + 1;
        PaginationText = $"Affichage de {start} à {skip + Records.Count} sur {list.Count} relevé(s)";
    }

    private bool InPeriod(string dateDisplay, DateTime monthStart, DateTime today)
    {
        if (!DateTime.TryParseExact(dateDisplay, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return true;

        return FilterPeriod switch
        {
            "7 derniers jours" => d >= today.AddDays(-7),
            "3 derniers mois" => d >= monthStart.AddMonths(-2),
            "12 derniers mois" => d >= monthStart.AddMonths(-11),
            "Année en cours" => d.Year == today.Year,
            AllPeriods => d >= monthStart,
            _ => true
        };
    }

    private bool MatchesAnomaly(ConsumptionListItem r) => FilterAnomaly switch
    {
        "Anomalies uniquement" => r.IsAnomaly || r.StatusLabel is "Critique" or "Élevé",
        "Sans anomalie" => !r.IsAnomaly && r.StatusLabel == "Normal",
        _ => true
    };

    private void BuildCharts(ConsumptionPageData data)
    {
        var palette = new[] { "#2563EB", "#0EA5E9", "#EA580C", "#6D28D9", "#0369A1", "#B45309", "#2D6A4F", "#DC2626" };

        MonthlyTrendSeries =
        [
            new LineSeries<decimal>
            {
                Name = "Coût total",
                Values = data.MonthlyTrend.Select(p => p.TotalCost).ToArray(),
                Stroke = new SolidColorPaint(SKColor.Parse("#2563EB")) { StrokeThickness = 2 },
                Fill = null,
                GeometrySize = 5
            }
        ];

        EnergyPieSeries = data.EnergyDistribution.Select((s, i) => new PieSeries<decimal>
        {
            Name = s.Type,
            Values = [s.Cost],
            Fill = new SolidColorPaint(SKColor.Parse(palette[i % palette.Length]))
        }).Cast<ISeries>().ToArray();

        CostBarSeries = data.CostByType.Select((s, i) => new ColumnSeries<decimal>
        {
            Name = s.Type,
            Values = [s.Cost],
            Fill = new SolidColorPaint(SKColor.Parse(palette[i % palette.Length]))
        }).Cast<ISeries>().ToArray();

        CompareSeries =
        [
            new ColumnSeries<decimal>
            {
                Name = "Mois N",
                Values = data.MonthComparison.Select(p => p.CurrentCost).ToArray(),
                Fill = new SolidColorPaint(SKColor.Parse("#2563EB"))
            },
            new ColumnSeries<decimal>
            {
                Name = "Mois N-1",
                Values = data.MonthComparison.Select(p => p.PreviousCost).ToArray(),
                Fill = new SolidColorPaint(SKColor.Parse("#94A3B8"))
            }
        ];
    }

    private void UpdateSyncStatus()
    {
        if (_syncService.LastSyncAt.HasValue)
        {
            LastSyncDisplay = $"Dernière sync : {_syncService.LastSyncAt.Value:dd/MM/yyyy HH:mm}";
            SyncStatusLabel = "À jour";
        }
        else
        {
            LastSyncDisplay = "Dernière sync : jamais";
            SyncStatusLabel = "Hors ligne";
        }
    }

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant() : name.Length >= 2 ? name[..2].ToUpperInvariant() : "AD";
    }
}
