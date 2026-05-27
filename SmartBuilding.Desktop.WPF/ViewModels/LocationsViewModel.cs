using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Shared.Constants;
using SmartBuilding.Desktop.WPF.Helpers;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class LocationsViewModel : BaseViewModel
{
    private readonly LocationsService _locationsService;
    private readonly ShellNavigationService _shellNavigation;
    private readonly ISyncService _syncService;
    private readonly SessionService _session;
    private List<LocationsPremiseItem> _allPremises = [];

    public const string AllBuildings = "Tous bâtiments";
    public const string AllFloors = "Tous étages";
    public const string AllTypes = "Tous types";
    public const string AllStatuses = "Tous statuts";
    public const string AllPayments = "Tous paiements";

    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _userRole = string.Empty;
    [ObservableProperty] private string _userInitials = "AD";
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _tableSearchQuery = string.Empty;
    [ObservableProperty] private string _filterBuilding = AllBuildings;
    [ObservableProperty] private string _filterFloor = AllFloors;
    [ObservableProperty] private string _filterType = AllTypes;
    [ObservableProperty] private string _filterStatus = AllStatuses;
    [ObservableProperty] private string _filterPayment = AllPayments;
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private string _paginationText = string.Empty;
    [ObservableProperty] private int _notificationCount = 1;
    [ObservableProperty] private bool _canManageLocations = true;
    [ObservableProperty] private bool _isNotificationsOpen;
    [ObservableProperty] private bool _isAddFormOpen;
    [ObservableProperty] private bool _isDetailPanelOpen;
    [ObservableProperty] private int _pageSize = 10;
    [ObservableProperty] private int _filteredTotal;
    [ObservableProperty] private int _selectedMainTab;
    [ObservableProperty] private int _selectedDetailTab;

    [ObservableProperty] private int _totalPremises;
    [ObservableProperty] private int _occupiedPremises;
    [ObservableProperty] private string _occupiedPercent = "0%";
    [ObservableProperty] private int _availablePremises;
    [ObservableProperty] private string _availablePercent = "0%";
    [ObservableProperty] private string _monthlyRentDisplay = "0 FC";
    [ObservableProperty] private string _availableBalanceDisplay = "0 FC";
    [ObservableProperty] private string _rentCollectedTotalDisplay = "0 FC";
    [ObservableProperty] private int _latePaymentsCount;
    [ObservableProperty] private string _latePercent = "0%";
    [ObservableProperty] private int _activeContracts;
    [ObservableProperty] private string _occupancyRateDisplay = "0%";
    [ObservableProperty] private LocationsPremiseItem? _selectedPremise;
    [ObservableProperty] private LocationsTenantItem? _selectedTenant;
    [ObservableProperty] private LocationsContractItem? _selectedContractSummary;
    [ObservableProperty] private DateTime _contractStart = DateTime.Today;
    [ObservableProperty] private DateTime _contractEnd = DateTime.Today.AddYears(1);
    [ObservableProperty] private string _contractType = "Bureau de travail";
    [ObservableProperty] private string _paymentFrequency = "Mensuelle";
    [ObservableProperty] private string _paymentMethod = "Virement bancaire";
    [ObservableProperty] private string _contractRentText = "0";
    [ObservableProperty] private string _contractDepositText = "0";
    [ObservableProperty] private string _contractClauses = "Accès 24/7, entretien inclus, internet inclus";
    [ObservableProperty] private bool _automaticRenewal = true;
    [ObservableProperty] private int _currentStep;
    [ObservableProperty] private string _formContractNumber = string.Empty;

    public bool IsFirstStep => CurrentStep <= 0;
    public bool IsLastStep => CurrentStep >= 4;
    public bool HasSelectedTenant => SelectedTenant is not null;
    public string NextButtonLabel => IsLastStep ? "Créer le contrat" : "Suivant";
    public string AutomaticRenewalDisplay => AutomaticRenewal ? "Oui" : "Non";

    public IReadOnlyList<LocationWizardStepItem> WizardSteps { get; } =
    [
        new() { Index = 0, Number = "1", Title = "Locataire", Subtitle = "Informations personnelles" },
        new() { Index = 1, Number = "2", Title = "Appartement", Subtitle = "Sélection de l'espace" },
        new() { Index = 2, Number = "3", Title = "Contrat", Subtitle = "Détails du contrat" },
        new() { Index = 3, Number = "4", Title = "Garantie & Paiement", Subtitle = "Conditions financières" },
        new() { Index = 4, Number = "5", Title = "Documents", Subtitle = "Résumé et confirmation" }
    ];

    [ObservableProperty] private string _formCode = string.Empty;
    [ObservableProperty] private string _formName = string.Empty;
    [ObservableProperty] private string _formBuilding = string.Empty;
    [ObservableProperty] private string _formFloor = string.Empty;
    [ObservableProperty] private string _formType = "Bureau";
    [ObservableProperty] private string _formAreaText = "0";
    [ObservableProperty] private string _formRentText = "0";
    [ObservableProperty] private string? _formError;

    [ObservableProperty] private string _formQuickDossier = string.Empty;
    [ObservableProperty] private string _formQuickTenantName = string.Empty;
    [ObservableProperty] private string _formQuickTenantPhone = string.Empty;
    [ObservableProperty] private string _formQuickTenantEmail = string.Empty;
    [ObservableProperty] private string _formQuickTenantCompany = string.Empty;

    [ObservableProperty] private bool _isWizardExpanded;

    [ObservableProperty] private ISeries[] _typeDistributionSeries = [];
    [ObservableProperty] private ISeries[] _occupancySeries = [];
    [ObservableProperty] private ISeries[] _rentStatusSeries = [];
    [ObservableProperty] private ISeries[] _rentTrendSeries = [];

    public ObservableCollection<LocationsPremiseItem> Premises { get; } = [];
    public ObservableCollection<LocationsContractItem> Contracts { get; } = [];
    public ObservableCollection<LocationsPaymentItem> Payments { get; } = [];
    public ObservableCollection<LocationsTenantItem> Tenants { get; } = [];
    public ObservableCollection<LocationsPaymentItem> LatePaymentRows { get; } = [];
    public ObservableCollection<LocationsContractItem> TerminatedContracts { get; } = [];
    public ObservableCollection<string> Buildings { get; } = [AllBuildings];
    public ObservableCollection<string> Floors { get; } = [AllFloors];
    public ObservableCollection<string> Types { get; } = [AllTypes];
    public ObservableCollection<string> Statuses { get; } = [AllStatuses, "Occupé", "Disponible"];
    public ObservableCollection<string> PaymentFilters { get; } = [AllPayments, "Payé", "En retard", "En attente"];
    public ObservableCollection<int> PageSizeOptions { get; } = [10, 20, 50];
    public ObservableCollection<string> ContractTypes { get; } = ["Bureau de travail", "Appartement", "Salle de réunion", "Salle conférence", "Commerce", "Entrepôt"];
    public ObservableCollection<string> PaymentFrequencies { get; } = ["Mensuelle", "Trimestrielle", "Semestrielle", "Annuelle"];
    public ObservableCollection<string> PaymentMethods { get; } = ["Virement bancaire", "Mobile money", "Espèces", "Chèque"];
    public ObservableCollection<string> MainTabs { get; } =
    [
        "Locaux", "Bâtiments", "Contrats", "Paiements", "Locataires",
        "Garanties", "Historique", "Retards", "Résiliations"
    ];

    [ObservableProperty] private string _selectedTenantName = "Sélectionnez un locataire";
    [ObservableProperty] private string _selectedTenantPhone = "—";
    [ObservableProperty] private string _selectedTenantEmail = "—";
    [ObservableProperty] private string _selectedTenantCompany = "—";
    [ObservableProperty] private string _selectedPremiseName = "Sélectionnez un espace";
    [ObservableProperty] private string _selectedPremiseSubtitle = "Aucun espace sélectionné";
    [ObservableProperty] private string _selectedPremiseArea = "—";
    [ObservableProperty] private string _selectedPremiseSelectionSummary = "Sélectionnez un espace";
    [ObservableProperty] private string _contractDurationDisplay = "12 mois";
    [ObservableProperty] private string _nextPaymentDateDisplay = string.Empty;
    [ObservableProperty] private string _rentSummaryDisplay = "0 FC";
    [ObservableProperty] private string _depositSummaryDisplay = "0 FC";

    public ObservableCollection<LocationsBuildingItem> BuildingRows { get; } = [];
    public ObservableCollection<LocationsGuaranteeItem> GuaranteeRows { get; } = [];
    public ObservableCollection<LocationsActivityItem> ActivityRows { get; } = [];
    public ObservableCollection<string> NotificationMessages { get; } = [];

    public LocationsViewModel(
        LocationsService locationsService,
        ShellNavigationService shellNavigation,
        ISyncService syncService,
        AppConfigurationService appConfiguration,
        SessionService session)
    {
        _locationsService = locationsService;
        _shellNavigation = shellNavigation;
        _syncService = syncService;
        appConfiguration.ConfigurationChanged += (_, _) => _ = LoadAsync();
        _session = session;
        CanManageLocations = session.HasPermission(PermissionCodes.LocationManage);
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
            var data = await _locationsService.LoadAsync();
            _allPremises = data.Premises.ToList();

            TotalPremises = data.TotalPremises;
            OccupiedPremises = data.OccupiedPremises;
            AvailablePremises = data.AvailablePremises;
            OccupiedPercent = data.OccupiedPercent;
            AvailablePercent = data.AvailablePercent;
            MonthlyRentDisplay = MoneyFormatter.Format(data.MonthlyRentCollected);
            AvailableBalanceDisplay = MoneyFormatter.Format(data.AvailableBalance);
            RentCollectedTotalDisplay = MoneyFormatter.Format(data.RentCollectedTotal);
            LatePaymentsCount = data.LatePayments;
            LatePercent = data.LatePercent;
            ActiveContracts = data.ActiveContracts;
            OccupancyRateDisplay = $"{data.OccupancyRate:F2}%";

            Buildings.Clear();
            Buildings.Add(AllBuildings);
            foreach (var b in _allPremises.Select(p => p.Building).Distinct().OrderBy(x => x))
                Buildings.Add(b);

            Floors.Clear();
            Floors.Add(AllFloors);
            foreach (var f in _allPremises.Select(p => p.Floor).Where(x => x != "—").Distinct().OrderBy(x => x))
                Floors.Add(f);

            Types.Clear();
            Types.Add(AllTypes);
            foreach (var t in _allPremises.Select(p => p.PremiseType).Distinct().OrderBy(x => x))
                Types.Add(t);

            Contracts.Clear();
            foreach (var c in data.Contracts) Contracts.Add(c);

            Payments.Clear();
            foreach (var p in data.Payments) Payments.Add(p);

            Tenants.Clear();
            foreach (var t in data.Tenants) Tenants.Add(t);

            LatePaymentRows.Clear();
            foreach (var p in data.LatePaymentRows) LatePaymentRows.Add(p);

            TerminatedContracts.Clear();
            foreach (var c in data.TerminatedContracts) TerminatedContracts.Add(c);

            BuildingRows.Clear();
            foreach (var b in data.BuildingRows) BuildingRows.Add(b);

            GuaranteeRows.Clear();
            foreach (var g in data.Guarantees) GuaranteeRows.Add(g);

            ActivityRows.Clear();
            foreach (var a in data.RecentActivities) ActivityRows.Add(a);

            BuildCharts(data);
            ApplyLocationAlerts(data);
            ActiveGuarantees = data.ActiveGuarantees;
            CurrentPage = 1;
            ApplyFilters();

            Premises.Clear();
            foreach (var p in _allPremises.Where(p => p.StatusLabel == "Disponible"))
                Premises.Add(p);

            if (SelectedPremise is null || Premises.All(p => p.Id != SelectedPremise.Id))
                SelectedPremise = Premises.FirstOrDefault();
            if (SelectedTenant is null || Tenants.All(t => t.Id != SelectedTenant.Id))
                SelectedTenant = Tenants.FirstOrDefault();
            if (SelectedContractSummary is null || Contracts.All(c => c.Id != SelectedContractSummary.Id))
                SelectedContractSummary = Contracts.FirstOrDefault();

            FormContractNumber = await _locationsService.GenerateNextContractNumberAsync();
            await ResetQuickCreateFieldsAsync();
            CurrentStep = 0;
            RefreshContractSummaryDisplays();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void GoToStep(object? parameter)
    {
        var step = TabNavigationHelper.ParseIndex(parameter, -1);
        if (step < 0 || step > 4) return;
        if (step > CurrentStep && !ValidateStep(CurrentStep))
            return;
        CurrentStep = step;
        NotifyStepChanged();
    }

    [RelayCommand]
    private void NextStep()
    {
        if (!ValidateStep(CurrentStep))
            return;

        if (IsLastStep)
        {
            _ = SaveContractAsync();
            return;
        }

        CurrentStep++;
        NotifyStepChanged();
    }

    [RelayCommand]
    private void PreviousStep()
    {
        if (CurrentStep > 0)
            CurrentStep--;
        NotifyStepChanged();
    }

    [RelayCommand]
    private async Task SaveContractAsync()
    {
        if (!CanManageLocations) { FormError = "Permission refusée."; return; }
        if (SelectedTenant is null || SelectedPremise is null)
        {
            FormError = "Sélectionnez un locataire et un espace.";
            return;
        }
        FormContractNumber = await _locationsService.GenerateNextContractNumberAsync();

        if (!SbmsDialogService.Confirm(
                "Créer le contrat",
                $"Créer le contrat {FormContractNumber} pour {SelectedTenant.Name} ?"))
            return;

        FormError = null;
        if (!decimal.TryParse(ContractRentText.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var rent))
            rent = 0;
        if (!decimal.TryParse(ContractDepositText.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var deposit))
            deposit = 0;

        IsBusy = true;
        try
        {
            var result = await _locationsService.CreateContractAsync(
                SelectedPremise.Id,
                SelectedTenant.Id,
                ContractStart,
                ContractEnd,
                rent,
                deposit,
                ContractType,
                ContractClauses,
                PaymentFrequency,
                PaymentMethod);

            if (!string.IsNullOrEmpty(result.Error))
            {
                FormError = result.Error;
                return;
            }

            StatusMessage = !string.IsNullOrWhiteSpace(result.SummaryPdfPath)
                ? "Contrat créé — récapitulatif PDF généré."
                : "Contrat créé — en attente de validation.";
            await _shellNavigation.OpenLocationListAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool ValidateStep(int step)
    {
        FormError = step switch
        {
            0 when SelectedTenant is null => "Sélectionnez un locataire.",
            1 when SelectedPremise is null => "Sélectionnez un espace disponible.",
            2 when ContractEnd.Date < ContractStart.Date => "La date de fin doit être après la date de début.",
            _ => null
        };
        return FormError is null;
    }

    private void NotifyStepChanged()
    {
        OnPropertyChanged(nameof(IsFirstStep));
        OnPropertyChanged(nameof(IsLastStep));
        OnPropertyChanged(nameof(NextButtonLabel));
    }

    partial void OnCurrentStepChanged(int value) => NotifyStepChanged();

    [RelayCommand]
    private async Task OpenTenantDetailAsync(LocationsTenantItem? tenant)
    {
        if (tenant is null || tenant.Id == Guid.Empty)
            return;

        await _shellNavigation.OpenTenantDetailAsync(tenant.Id);
    }

    [RelayCommand]
    private async Task OpenTenantFromPremiseAsync(LocationsPremiseItem? premise)
    {
        if (premise is null || premise.TenantId == Guid.Empty)
        {
            StatusMessage = "Aucun locataire associé à ce local.";
            return;
        }

        await _shellNavigation.OpenTenantDetailAsync(premise.TenantId);
    }

    [RelayCommand]
    private async Task OpenSelectedPremiseTenantAsync()
    {
        if (SelectedPremise is null)
            return;

        await OpenTenantFromPremiseAsync(SelectedPremise);
    }

    [RelayCommand]
    private void SelectPremise(LocationsPremiseItem? premise)
    {
        if (premise is not null)
        {
            SelectedPremise = premise;
            SelectedDetailTab = 0;
        }
    }

    [RelayCommand]
    private void CloseDetailPanel()
    {
        SelectedPremise = null;
        IsDetailPanelOpen = false;
    }

    [RelayCommand]
    private void SetDetailTab(object? parameter) => SelectedDetailTab = TabNavigationHelper.ParseIndex(parameter);

    [RelayCommand]
    private void SetMainTab(object? parameter)
    {
        var tabIndex = TabNavigationHelper.ParseIndex(parameter, -1);
        if (tabIndex < 0 || tabIndex >= MainTabs.Count)
            return;

        SelectedMainTab = tabIndex;
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
    private async Task SyncAsync()
    {
        IsBusy = true;
        try
        {
            await _syncService.SyncAsync(manual: true);
            await LoadAsync();
            StatusMessage = "Synchronisation terminée";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveQuickCreateAsync()
    {
        if (!CanManageLocations)
        {
            FormError = "Permission refusée.";
            return;
        }

        if (string.IsNullOrWhiteSpace(FormQuickTenantName))
        {
            FormError = "Le nom du locataire est obligatoire.";
            return;
        }

        if (string.IsNullOrWhiteSpace(FormQuickTenantPhone))
        {
            FormError = "Le téléphone du locataire est obligatoire.";
            return;
        }

        if (string.IsNullOrWhiteSpace(FormName))
        {
            FormError = "Le nom du local est obligatoire.";
            return;
        }

        if (string.IsNullOrWhiteSpace(FormCode))
        {
            FormError = "Le code du local est obligatoire.";
            return;
        }

        FormError = null;
        IsBusy = true;
        try
        {
            var tenantError = await _locationsService.CreateTenantAsync(new Tenant
            {
                DossierNumber = FormQuickDossier,
                Name = FormQuickTenantName.Trim(),
                Phone = FormQuickTenantPhone.Trim(),
                Email = FormQuickTenantEmail.Trim(),
                Company = FormQuickTenantCompany.Trim(),
                RentalStatus = LocationConstants.TenantStatus.Active
            });

            if (!string.IsNullOrEmpty(tenantError))
            {
                FormError = tenantError;
                return;
            }

            if (!decimal.TryParse(FormAreaText.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var area))
                area = 0;
            if (!decimal.TryParse(FormRentText.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var rent))
                rent = 0;

            var premiseError = await _locationsService.CreatePremiseAsync(new Premise
            {
                Code = FormCode.Trim(),
                Name = FormName.Trim(),
                Building = string.IsNullOrWhiteSpace(FormBuilding) ? "Tour SBMS" : FormBuilding.Trim(),
                Floor = FormFloor.Trim(),
                PremiseType = string.IsNullOrWhiteSpace(FormType) ? "Bureau" : FormType,
                AreaSqM = area,
                MonthlyRent = rent,
                IsOccupied = false,
                OccupancyStatus = LocationConstants.PremiseOccupancyStatus.Available
            });

            if (!string.IsNullOrEmpty(premiseError))
            {
                FormError = premiseError;
                return;
            }

            StatusMessage = $"Locataire « {FormQuickTenantName.Trim()} » et local « {FormName.Trim()} » enregistrés.";
            await LoadAsync();
            await ResetQuickCreateFieldsAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ResetQuickCreateAsync() => await ResetQuickCreateFieldsAsync();

    private async Task ResetQuickCreateFieldsAsync()
    {
        FormQuickTenantName = string.Empty;
        FormQuickTenantPhone = string.Empty;
        FormQuickTenantEmail = string.Empty;
        FormQuickTenantCompany = string.Empty;
        FormQuickDossier = await _locationsService.GenerateNextDossierNumberAsync();
        FormCode = await _locationsService.GenerateNextCodeAsync();
        FormName = string.Empty;
        FormBuilding = Buildings.Count > 1 ? Buildings[1] : "Tour SBMS";
        FormFloor = string.Empty;
        FormType = "Bureau";
        FormAreaText = "0";
        FormRentText = "0";
        FormError = null;
    }

    [RelayCommand]
    private async Task AddPremiseAsync()
    {
        CloseAllForms();
        FormError = null;
        FormCode = await _locationsService.GenerateNextCodeAsync();
        FormName = string.Empty;
        FormBuilding = Buildings.Count > 1 ? Buildings[1] : string.Empty;
        FormFloor = string.Empty;
        FormType = "Bureau";
        FormAreaText = "0";
        FormRentText = "0";
        IsAddFormOpen = true;
    }

    [RelayCommand]
    private async Task AddTenant() => await _shellNavigation.OpenTenantFormAsync(null);

    [RelayCommand]
    private void AddContract() => NextStep();

    [RelayCommand]
    private async Task CollectRent() => await _shellNavigation.OpenRentFormAsync();

    [RelayCommand]
    private void ExportExcel()
    {
        var rows = GetExportRows();
        if (rows.Count == 0)
        {
            StatusMessage = "Aucune donnée à exporter.";
            return;
        }

        if (LocationsExportService.ExportPremisesCsv(rows))
            StatusMessage = "Export Excel (CSV) enregistré.";
    }

    [RelayCommand]
    private void ExportPdf()
    {
        var rows = GetExportRows();
        if (rows.Count == 0)
        {
            StatusMessage = "Aucune donnée à exporter.";
            return;
        }

        if (LocationsExportService.ExportPremisesHtml(rows, "Rapport Locations — SBMS"))
            StatusMessage = "Rapport HTML enregistré (ouvrez et imprimez en PDF).";
    }

    [RelayCommand]
    private void Print()
    {
        var rows = GetExportRows();
        if (rows.Count == 0)
        {
            StatusMessage = "Aucune donnée à imprimer.";
            return;
        }

        if (LocationsExportService.PrintPremises(rows, "Liste des locaux — SBMS"))
            StatusMessage = "Impression envoyée.";
    }

    [RelayCommand]
    private void CancelAddForm() => CloseAllForms();

    private void CloseAllForms()
    {
        IsAddFormOpen = false;
        IsEditPremiseFormOpen = false;
        FormError = null;
    }

    private List<LocationsPremiseItem> GetExportRows() =>
        _allPremises.Count > 0 ? _allPremises : Premises.ToList();

    [RelayCommand]
    private async Task SavePremiseAsync()
    {
        if (!CanManageLocations) { FormError = "Permission refusée."; return; }
        if (!SbmsDialogService.Confirm("Confirmation", "Confirmer l'enregistrement de ce local ?"))
            return;

        FormError = null;
        IsBusy = true;
        try
        {
            if (!decimal.TryParse(FormAreaText.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var area))
                area = 0;
            if (!decimal.TryParse(FormRentText.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var rent))
                rent = 0;

            var error = await _locationsService.CreatePremiseAsync(new Premise
            {
                Code = FormCode,
                Name = FormName,
                Building = FormBuilding,
                Floor = FormFloor,
                PremiseType = FormType,
                AreaSqM = area,
                MonthlyRent = rent,
                IsOccupied = false
            });

            if (!string.IsNullOrEmpty(error))
            {
                FormError = error;
                return;
            }

            IsAddFormOpen = false;
            StatusMessage = "Local enregistré avec succès.";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedPremiseChanged(LocationsPremiseItem? value)
    {
        IsDetailPanelOpen = value is not null;
        if (value is not null && value.MonthlyRent > 0)
            ContractRentText = value.MonthlyRent.ToString("0");

        RefreshContractSummaryDisplays();
    }

    partial void OnSelectedTenantChanged(LocationsTenantItem? value)
    {
        OnPropertyChanged(nameof(HasSelectedTenant));
        if (value is null)
            SelectedPremise = null;
        RefreshContractSummaryDisplays();
    }

    partial void OnContractStartChanged(DateTime value) => RefreshContractSummaryDisplays();

    partial void OnContractEndChanged(DateTime value) => RefreshContractSummaryDisplays();

    partial void OnContractRentTextChanged(string value) => RefreshContractSummaryDisplays();

    partial void OnContractDepositTextChanged(string value) => RefreshContractSummaryDisplays();

    private void RefreshContractSummaryDisplays()
    {
        SelectedTenantName = SelectedTenant?.Name ?? "Sélectionnez un locataire";
        SelectedTenantPhone = EmptyToDash(SelectedTenant?.Phone);
        SelectedTenantEmail = EmptyToDash(SelectedTenant?.Email);
        SelectedTenantCompany = EmptyToDash(SelectedTenant?.Company);
        SelectedPremiseName = SelectedPremise?.Name ?? "Sélectionnez un espace";
        SelectedPremiseSubtitle = SelectedPremise is null
            ? "Aucun espace sélectionné"
            : $"{SelectedPremise.Building} · {SelectedPremise.Floor}";
        SelectedPremiseArea = SelectedPremise?.AreaDisplay ?? "—";
        ContractDurationDisplay =
            $"{Math.Max(1, ((ContractEnd.Year - ContractStart.Year) * 12) + ContractEnd.Month - ContractStart.Month)} mois";
        NextPaymentDateDisplay = ContractStart.AddMonths(1).ToString("dd/MM/yyyy");
        RentSummaryDisplay = FormatMoney(ParseAmount(ContractRentText));
        DepositSummaryDisplay = FormatMoney(ParseAmount(ContractDepositText));
        SelectedPremiseSelectionSummary = SelectedPremise is null
            ? "Espace sélectionné : sélectionnez un espace"
            : $"Espace sélectionné : {SelectedPremiseName} - {RentSummaryDisplay} / mois";
    }

    partial void OnSearchQueryChanged(string value) => ResetPageAndFilter();
    partial void OnTableSearchQueryChanged(string value) => ResetPageAndFilter();
    partial void OnFilterBuildingChanged(string value) => ResetPageAndFilter();
    partial void OnFilterFloorChanged(string value) => ResetPageAndFilter();
    partial void OnFilterTypeChanged(string value) => ResetPageAndFilter();
    partial void OnFilterStatusChanged(string value) => ResetPageAndFilter();
    partial void OnFilterPaymentChanged(string value) => ResetPageAndFilter();
    partial void OnPageSizeChanged(int value) => ResetPageAndFilter();

    private void ResetPageAndFilter()
    {
        CurrentPage = 1;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var query = $"{SearchQuery} {TableSearchQuery}".Trim();
        var filtered = _allPremises.Where(p =>
            (FilterBuilding == AllBuildings || p.Building == FilterBuilding) &&
            (FilterFloor == AllFloors || p.Floor == FilterFloor) &&
            (FilterType == AllTypes || p.PremiseType == FilterType) &&
            (FilterStatus == AllStatuses || p.StatusLabel == FilterStatus) &&
            (string.IsNullOrWhiteSpace(query) ||
             p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             p.Code.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             p.TenantName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             p.Building.Contains(query, StringComparison.OrdinalIgnoreCase)));

        var list = filtered.ToList();
        FilteredTotal = list.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling(list.Count / (double)PageSize));
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;

        var skip = (CurrentPage - 1) * PageSize;
        var page = list.Skip(skip).Take(PageSize).ToList();

        Premises.Clear();
        foreach (var p in page) Premises.Add(p);

        var start = list.Count == 0 ? 0 : skip + 1;
        var end = skip + page.Count;
        PaginationText = $"Affichage de {start} à {end} sur {list.Count} locaux";
    }

    private void BuildCharts(LocationsPageData data)
    {
        var palette = new[] { "#2D6A4F", "#40916C", "#52B788", "#74C69D", "#95D5B2", "#B7E4C7" };
        TypeDistributionSeries = data.TypeDistribution.Select((s, i) => new PieSeries<int>
        {
            Name = s.Type,
            Values = [s.Count],
            Fill = new SolidColorPaint(SKColor.Parse(palette[i % palette.Length]))
        }).Cast<ISeries>().ToArray();

        var free = Math.Max(data.AvailablePremises, 0);
        OccupancySeries =
        [
            new PieSeries<double>
            {
                Name = "Occupés",
                Values = [data.OccupiedPremises],
                Fill = new SolidColorPaint(SKColor.Parse("#2D6A4F"))
            },
            new PieSeries<double>
            {
                Name = "Libres",
                Values = [free],
                Fill = new SolidColorPaint(SKColor.Parse("#95D5B2"))
            }
        ];

        RentStatusSeries =
        [
            new RowSeries<decimal>
            {
                Name = "Occupés",
                Values = [data.RentOccupied],
                Fill = new SolidColorPaint(SKColor.Parse("#2D6A4F"))
            },
            new RowSeries<decimal>
            {
                Name = "En retard",
                Values = [data.RentLate],
                Fill = new SolidColorPaint(SKColor.Parse("#EF4444"))
            },
            new RowSeries<decimal>
            {
                Name = "Disponibles",
                Values = [data.RentAvailable],
                Fill = new SolidColorPaint(SKColor.Parse("#60A5FA"))
            }
        ];

        RentTrendSeries =
        [
            new LineSeries<decimal>
            {
                Name = "Loyers encaissés",
                Values = data.RentTrend.ToArray(),
                Fill = null,
                Stroke = new SolidColorPaint(SKColor.Parse("#2D6A4F")) { StrokeThickness = 3 },
                GeometryFill = new SolidColorPaint(SKColor.Parse("#2D6A4F")),
                GeometryStroke = new SolidColorPaint(SKColor.Parse("#2D6A4F"))
            }
        ];
    }

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0][0]}{parts[^1][0]}".ToUpper() : name.Length >= 2 ? name[..2].ToUpper() : "AD";
    }

    private static decimal ParseAmount(string? value)
    {
        if (decimal.TryParse(value?.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var amount))
            return amount;

        return 0;
    }

    private static string FormatMoney(decimal amount) =>
        MoneyFormatter.Format(amount);

    private static string EmptyToDash(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;
}
