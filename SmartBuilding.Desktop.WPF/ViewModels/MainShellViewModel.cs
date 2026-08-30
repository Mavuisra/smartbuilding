using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;
using SmartBuilding.Infrastructure.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class MainShellViewModel : BaseViewModel
{
    private readonly IServiceProvider _services;
    private readonly SessionService _session;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly PersonnelViewModel _personnelViewModel;
    private readonly LocationsViewModel _locationsViewModel;
    private readonly LocationsListViewModel _locationsListViewModel;
    private readonly LocationsTenantsViewModel _locationsTenantsViewModel;
    private readonly LocationsPatrimoineViewModel _locationsPatrimoineViewModel;
    private readonly FinancesViewModel _financesViewModel;
    private readonly RapportsViewModel _rapportsViewModel;
    private readonly TechnicalViewModel _technicalViewModel;
    private readonly SuppliersViewModel _suppliersViewModel;
    private readonly InventoryViewModel _inventoryViewModel;
    private readonly ConsumptionsViewModel _consumptionsViewModel;
    private readonly VisitsViewModel _visitsViewModel;
    private readonly EmailsViewModel _emailsViewModel;
    private readonly DocumentsViewModel _documentsViewModel;
    private readonly UsersViewModel _usersViewModel;
    private readonly ActivityLogViewModel _activityLogViewModel;
    private readonly SynchronizationViewModel _synchronizationViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly ShellNavigationService _shellNavigation;
    private readonly ISyncService _syncService;
    private readonly INetworkService _network;
    private readonly IConfiguration _configuration;
    private readonly IDbContextFactory<SmartBuildingDbContext> _dbContextFactory;
    private readonly AppConfigurationService _appConfiguration;
    private readonly Action _onLogout;
    private BaseViewModel? _trackedViewModel;
    private bool _shellInitialized;
    private Task? _shellInitTask;

    [ObservableProperty]
    private object? _currentViewModel;

    [ObservableProperty]
    private string _selectedModuleId = "dashboard";

    [ObservableProperty]
    private string _lastSyncDisplay = "Dernière sync : —";

    [ObservableProperty]
    private string _syncStatusLabel = "Hors ligne";

    [ObservableProperty] private string _shellUserName = "Admin SBMS";
    [ObservableProperty] private string _shellUserRole = "Administrateur";
    [ObservableProperty] private string _shellUserInitials = "AD";
    [ObservableProperty] private string _internetStatusLabel = "Déconnecté";
    [ObservableProperty] private string _cloudStatusLabel = "Hors ligne";
    [ObservableProperty] private string _localDbStatusLabel = "OK";
    [ObservableProperty] private string _pingDisplay = "—";
    [ObservableProperty] private bool _isInternetConnected;
    [ObservableProperty] private bool _isCloudConnected;
    [ObservableProperty] private bool _isCurrentViewBusy;
    [ObservableProperty] private string _treasuryAvailableDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _treasuryDetailDisplay = "Loyers encaissés : 0 USD";
    [ObservableProperty] private bool _isTreasuryDepleted;
    [ObservableProperty] private string _shellBrandName = BuildingInfoDefaults.CompanyName;
    [ObservableProperty] private string _shellBrandSubtitle = AppBrandingState.DefaultSubtitle;
    [ObservableProperty] private string? _shellLogoPath;
    [ObservableProperty] private bool _hasShellLogo;
    [ObservableProperty] private string _windowTitle = BuildingInfoDefaults.CompanyName;
    [ObservableProperty] private bool _isReceptionOnly;

    public ObservableCollection<ShellNavEntry> NavigationItems { get; } = [];

    public MainShellViewModel(
        IServiceProvider services,
        SessionService session,
        DashboardViewModel dashboardViewModel,
        PersonnelViewModel personnelViewModel,
        LocationsViewModel locationsViewModel,
        LocationsListViewModel locationsListViewModel,
        LocationsTenantsViewModel locationsTenantsViewModel,
        LocationsPatrimoineViewModel locationsPatrimoineViewModel,
        FinancesViewModel financesViewModel,
        RapportsViewModel rapportsViewModel,
        TechnicalViewModel technicalViewModel,
        SuppliersViewModel suppliersViewModel,
        InventoryViewModel inventoryViewModel,
        ConsumptionsViewModel consumptionsViewModel,
        VisitsViewModel visitsViewModel,
        EmailsViewModel emailsViewModel,
        DocumentsViewModel documentsViewModel,
        UsersViewModel usersViewModel,
        ActivityLogViewModel activityLogViewModel,
        SynchronizationViewModel synchronizationViewModel,
        SettingsViewModel settingsViewModel,
        ShellNavigationService shellNavigation,
        ISyncService syncService,
        INetworkService network,
        IConfiguration configuration,
        IDbContextFactory<SmartBuildingDbContext> dbContextFactory,
        AppConfigurationService appConfiguration,
        Action onLogout)
    {
        _dbContextFactory = dbContextFactory;
        _appConfiguration = appConfiguration;
        _appConfiguration.ConfigurationChanged += (_, _) => ApplyBrandingFromConfiguration();
        ApplyBrandingFromConfiguration();
        _session = session;
        ShellUserName = session.CurrentUser?.FullName ?? "Administrateur";
        ShellUserRole = session.CurrentUser?.Role ?? "Administrateur";
        ShellUserInitials = GetInitials(ShellUserName);
        _services = services;
        _dashboardViewModel = dashboardViewModel;
        _personnelViewModel = personnelViewModel;
        _locationsViewModel = locationsViewModel;
        _locationsListViewModel = locationsListViewModel;
        _locationsTenantsViewModel = locationsTenantsViewModel;
        _locationsPatrimoineViewModel = locationsPatrimoineViewModel;
        _financesViewModel = financesViewModel;
        _rapportsViewModel = rapportsViewModel;
        _technicalViewModel = technicalViewModel;
        _suppliersViewModel = suppliersViewModel;
        _inventoryViewModel = inventoryViewModel;
        _consumptionsViewModel = consumptionsViewModel;
        _visitsViewModel = visitsViewModel;
        _emailsViewModel = emailsViewModel;
        _documentsViewModel = documentsViewModel;
        _usersViewModel = usersViewModel;
        _activityLogViewModel = activityLogViewModel;
        _synchronizationViewModel = synchronizationViewModel;
        _settingsViewModel = settingsViewModel;
        _settingsViewModel.NavigateToModuleRequested += id => _ = NavigateAsync(id);
        _shellNavigation = shellNavigation;
        _syncService = syncService;
        _network = network;
        _configuration = configuration;
        _onLogout = onLogout;
        _shellNavigation.Register(
            OpenTenantDetailAsync,
            BackToLocationsFromTenantAsync,
            OpenBuildingFormAsync,
            OpenTenantFormAsync,
            OpenContractFormAsync,
            OpenRentFormAsync,
            OpenLocationCreateAsync,
            OpenLocationListAsync,
            OpenTenantsListAsync,
            OpenPatrimoineForTabAsync,
            ResumeContractFormAsync);
        IsReceptionOnly = _session.IsReceptionOnly();
        RebuildNavigation();
        CurrentViewModel = IsReceptionOnly ? _visitsViewModel : _dashboardViewModel;
        SelectedModuleId = IsReceptionOnly ? "visites" : "dashboard";
    }

    public async Task NavigateToDefaultModuleAsync()
    {
        await EnsureShellInitializedAsync();

        if (_session.PendingCompanyProfileSetup)
        {
            _settingsViewModel.SetCompanyProfileSetupMode(true);
            await NavigateAsync("parametres");
            _settingsViewModel.FocusCategory("general");
            return;
        }

        if (_session.IsReceptionOnly())
            await NavigateAsync("visites");
        else
            await NavigateAsync("dashboard");
    }

    private Task EnsureShellInitializedAsync()
    {
        if (_shellInitialized)
            return Task.CompletedTask;

        return _shellInitTask ??= InitializeShellCoreAsync();
    }

    private async Task InitializeShellCoreAsync()
    {
        try
        {
            await InitializeShellAsync();
            _shellInitialized = true;
        }
        finally
        {
            _shellInitTask = null;
        }
    }

    private async Task InitializeShellAsync()
    {
        await _appConfiguration.LoadAndApplyAsync();
        ApplyBrandingFromConfiguration();
        await _syncService.EnsureMetadataLoadedAsync();
        await RefreshShellStatusAsync();
    }

    private void ApplyBrandingFromConfiguration()
    {
        var c = _appConfiguration.Current;
        ShellBrandName = c.CompanyName;
        ShellBrandSubtitle = c.AppSubtitle;
        ShellLogoPath = c.LogoPath;
        HasShellLogo = !string.IsNullOrWhiteSpace(c.LogoPath) && File.Exists(c.LogoPath);
        WindowTitle = c.CompanyName;
    }

    private void RebuildNavigation()
    {
        NavigationItems.Clear();
        foreach (var entry in ModuleRegistry.BuildNavigation(_session))
            NavigationItems.Add(entry);
    }

    partial void OnCurrentViewModelChanged(object? value)
    {
        if (_trackedViewModel is not null)
            _trackedViewModel.PropertyChanged -= OnTrackedViewModelPropertyChanged;

        _trackedViewModel = value as BaseViewModel;
        if (_trackedViewModel is not null)
            _trackedViewModel.PropertyChanged += OnTrackedViewModelPropertyChanged;

        IsCurrentViewBusy = _trackedViewModel?.IsBusy ?? false;
    }

    private void OnTrackedViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BaseViewModel.IsBusy) && sender is BaseViewModel vm)
            IsCurrentViewBusy = vm.IsBusy;
    }

    public async Task OpenTenantDetailAsync(Guid tenantId)
    {
        var vm = ActivatorUtilities.CreateInstance<TenantDetailViewModel>(_services);
        vm.Initialize(tenantId);
        CurrentViewModel = vm;
        SelectedModuleId = "locations-tenants";
        await vm.LoadCommand.ExecuteAsync(null);
        await RefreshShellStatusAsync();
    }

    private async Task BackToLocationsFromTenantAsync() => await OpenTenantsListAsync();

    private async Task OpenLocationCreateAsync()
    {
        var vm = ActivatorUtilities.CreateInstance<LocationContractFormViewModel>(_services);
        CurrentViewModel = vm;
        SelectedModuleId = "locations-create";
        await vm.LoadCommand.ExecuteAsync(null);
        await RefreshShellStatusAsync();
    }

    private async Task OpenLocationListAsync()
    {
        CurrentViewModel = _locationsListViewModel;
        SelectedModuleId = "locations-list";
        await _locationsListViewModel.LoadCommand.ExecuteAsync(null);
        await RefreshShellStatusAsync();
    }

    private async Task OpenBuildingFormAsync(Guid? buildingId)
    {
        var vm = ActivatorUtilities.CreateInstance<LocationBuildingFormViewModel>(_services);
        vm.Initialize(buildingId);
        CurrentViewModel = vm;
        SelectedModuleId = "locations-list";
        await vm.LoadCommand.ExecuteAsync(null);
        await RefreshShellStatusAsync();
    }

    private async Task OpenTenantFormAsync(Guid? tenantId)
    {
        var vm = ActivatorUtilities.CreateInstance<LocationTenantFormViewModel>(_services);
        vm.Initialize(tenantId);
        CurrentViewModel = vm;
        SelectedModuleId = "locations-tenants";
        await vm.LoadCommand.ExecuteAsync(null);
        await RefreshShellStatusAsync();
    }

    private async Task OpenTenantsListAsync()
    {
        CurrentViewModel = _locationsTenantsViewModel;
        SelectedModuleId = "locations-tenants";
        await _locationsTenantsViewModel.LoadCommand.ExecuteAsync(null);
        await RefreshShellStatusAsync();
    }

    private async Task OpenPatrimoineAsync(int tabIndex, string moduleId)
    {
        _locationsPatrimoineViewModel.Initialize(tabIndex);
        CurrentViewModel = _locationsPatrimoineViewModel;
        SelectedModuleId = moduleId;
        await _locationsPatrimoineViewModel.LoadCommand.ExecuteAsync(null);
        await RefreshShellStatusAsync();
    }

    private Task OpenPatrimoineForTabAsync(int tabIndex) => tabIndex switch
    {
        0 => OpenPatrimoineAsync(0, "locations-landlord"),
        1 => OpenPatrimoineAsync(1, "locations-building"),
        2 => OpenPatrimoineAsync(2, "locations-gestion"),
        // Compat : ancien index « Appartements »
        3 => OpenPatrimoineAsync(2, "locations-gestion"),
        _ => OpenPatrimoineAsync(1, "locations-building")
    };

    private async Task ResumeContractFormAsync(LocationContractFormViewModel vm, Guid? selectTenantId)
    {
        CurrentViewModel = vm;
        SelectedModuleId = "locations-create";
        await vm.LoadAsync();
        if (selectTenantId.HasValue)
            vm.ApplyTenantSelection(selectTenantId.Value);
        await RefreshShellStatusAsync();
    }

    private async Task OpenContractFormAsync() => await OpenLocationCreateAsync();

    private async Task OpenRentFormAsync()
    {
        var vm = ActivatorUtilities.CreateInstance<LocationRentFormViewModel>(_services);
        CurrentViewModel = vm;
        SelectedModuleId = "locations-rent-pay";
        await vm.LoadCommand.ExecuteAsync(null);
        await RefreshShellStatusAsync();
    }

    [RelayCommand]
    private async Task NavigateAsync(string? moduleId)
    {
        if (CurrentViewModel is SynchronizationViewModel leavingSync)
            leavingSync.Deactivate();

        if (string.IsNullOrWhiteSpace(moduleId))
            return;

        await EnsureShellInitializedAsync();

        var permissionModuleId = moduleId switch
        {
            "incidents" => "technique",
            _ when moduleId.StartsWith("locations", StringComparison.OrdinalIgnoreCase) => "locations",
            _ => moduleId
        };
        var module = ModuleRegistry.Get(permissionModuleId);
        if (!ModuleRegistry.CanAccess(_session, module))
        {
            ErrorMessage = "Accès refusé à ce module.";
            return;
        }

        ErrorMessage = null;
        SelectedModuleId = moduleId;

        if (moduleId == "dashboard")
        {
            await ShowDashboardAsync();
            return;
        }

        if (moduleId == "personnel")
        {
            CurrentViewModel = _personnelViewModel;
            await _personnelViewModel.LoadCommand.ExecuteAsync(null);
            await RefreshShellStatusAsync();
            return;
        }

        if (moduleId == "locations")
        {
            await OpenLocationListAsync();
            return;
        }

        if (moduleId == "locations-create")
        {
            await OpenLocationCreateAsync();
            return;
        }

        if (moduleId == "locations-list")
        {
            await OpenLocationListAsync();
            return;
        }

        if (moduleId == "locations-contract")
        {
            await OpenContractFormAsync();
            return;
        }

        if (moduleId == "locations-rent-pay")
        {
            await OpenRentFormAsync();
            return;
        }

        if (moduleId == "locations-tenants")
        {
            await OpenTenantsListAsync();
            return;
        }

        if (moduleId == "locations-landlord")
        {
            await OpenPatrimoineAsync(0, moduleId);
            return;
        }

        if (moduleId == "locations-building")
        {
            await OpenPatrimoineAsync(1, moduleId);
            return;
        }

        if (moduleId == "locations-apartments")
        {
            await OpenPatrimoineAsync(1, "locations-building");
            return;
        }

        if (moduleId == "locations-gestion")
        {
            await OpenPatrimoineAsync(2, moduleId);
            return;
        }

        if (moduleId == "finances")
        {
            CurrentViewModel = _financesViewModel;
            await _financesViewModel.LoadCommand.ExecuteAsync(null);
            await RefreshShellStatusAsync();
            return;
        }

        if (moduleId == "rapports")
        {
            CurrentViewModel = _rapportsViewModel;
            await _rapportsViewModel.LoadCommand.ExecuteAsync(null);
            await RefreshShellStatusAsync();
            return;
        }

        if (moduleId is "technique" or "incidents")
        {
            SelectedModuleId = "technique";
            CurrentViewModel = _technicalViewModel;
            _technicalViewModel.NavigateToSection(moduleId == "incidents" ? 1 : 0);
            await _technicalViewModel.LoadCommand.ExecuteAsync(null);
            await RefreshShellStatusAsync();
            return;
        }

        if (moduleId == "fournisseurs")
        {
            CurrentViewModel = _suppliersViewModel;
            await _suppliersViewModel.LoadCommand.ExecuteAsync(null);
            await RefreshShellStatusAsync();
            return;
        }

        if (moduleId == "inventaire")
        {
            CurrentViewModel = _inventoryViewModel;
            await _inventoryViewModel.LoadCommand.ExecuteAsync(null);
            await RefreshShellStatusAsync();
            return;
        }

        if (moduleId == "consommations")
        {
            CurrentViewModel = _consumptionsViewModel;
            await _consumptionsViewModel.LoadCommand.ExecuteAsync(null);
            await RefreshShellStatusAsync();
            return;
        }

        if (moduleId == "visites")
        {
            CurrentViewModel = _visitsViewModel;
            await _visitsViewModel.LoadCommand.ExecuteAsync(null);
            await RefreshShellStatusAsync();
            return;
        }

        if (moduleId == "emails")
        {
            CurrentViewModel = _emailsViewModel;
            await _emailsViewModel.LoadCommand.ExecuteAsync(null);
            await RefreshShellStatusAsync();
            return;
        }

        if (moduleId == "documents")
        {
            CurrentViewModel = _documentsViewModel;
            await _documentsViewModel.LoadCommand.ExecuteAsync(null);
            await RefreshShellStatusAsync();
            return;
        }

        if (moduleId == "utilisateurs")
        {
            CurrentViewModel = _usersViewModel;
            await _usersViewModel.LoadCommand.ExecuteAsync(null);
            await RefreshShellStatusAsync();
            return;
        }

        if (moduleId == "synchronisation")
        {
            CurrentViewModel = _synchronizationViewModel;
            _synchronizationViewModel.Activate();
            await _synchronizationViewModel.LoadCommand.ExecuteAsync(null);
            await RefreshShellStatusAsync();
            return;
        }

        if (moduleId == "parametres")
        {
            CurrentViewModel = _settingsViewModel;
            await _settingsViewModel.LoadCommand.ExecuteAsync(null);
            await RefreshShellStatusAsync();
            return;
        }

        if (moduleId == "journal")
        {
            CurrentViewModel = _activityLogViewModel;
            await _activityLogViewModel.LoadCommand.ExecuteAsync(null);
            await RefreshShellStatusAsync();
            return;
        }

        StatusMessage = $"Module inconnu : {moduleId}";
    }

    [RelayCommand]
    private async Task ShowDashboardAsync()
    {
        SelectedModuleId = "dashboard";
        CurrentViewModel = _dashboardViewModel;
        await _dashboardViewModel.LoadCommand.ExecuteAsync(null);
        await RefreshShellStatusAsync();
    }

    [RelayCommand]
    private void Logout() => _onLogout();

    private async Task UpdateConnectionStatusAsync()
    {
        IsInternetConnected = _network.IsConnected();
        InternetStatusLabel = IsInternetConnected ? "Connecté" : "Déconnecté";

        if (IsInternetConnected)
        {
            var apiUrl = _configuration["Api:BaseUrl"] ?? "https://localhost:7001/";
            var sw = Stopwatch.StartNew();
            IsCloudConnected = await _network.CanReachApiAsync(apiUrl);
            sw.Stop();
            PingDisplay = IsCloudConnected ? $"{sw.ElapsedMilliseconds} ms" : "—";
            CloudStatusLabel = IsCloudConnected ? "Connecté" : "Hors ligne";
        }
        else
        {
            IsCloudConnected = false;
            CloudStatusLabel = "Hors ligne";
            PingDisplay = "—";
        }

        LocalDbStatusLabel = "OK";
    }

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "AD";
        if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
        return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
    }

    private async Task RefreshShellStatusAsync()
    {
        await _syncService.EnsureMetadataLoadedAsync();
        UpdateSyncStatus();
        await UpdateConnectionStatusAsync();
        await RefreshTreasuryAsync();
    }

    private async Task RefreshTreasuryAsync()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var cash = await TreasuryLoader.LoadAsync(new FinanceLedgerService(db));
        TreasuryAvailableDisplay = FormatFc(cash.AvailableThisMonth);
        TreasuryDetailDisplay =
            $"Loyers ce mois {FormatFc(cash.RentCollectedThisMonth)} · Dépenses ce mois {FormatFc(cash.TotalExpensesThisMonth)} · Total encaissé {FormatFc(cash.RentCollectedTotal)}";
        IsTreasuryDepleted = cash.AvailableThisMonth <= 0 && cash.RentCollectedThisMonth > 0;
    }

    private static string FormatFc(decimal amount) => MoneyFormatter.Format(amount);

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
}
