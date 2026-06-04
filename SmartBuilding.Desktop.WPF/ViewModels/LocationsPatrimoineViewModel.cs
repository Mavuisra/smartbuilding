using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;
using SmartBuilding.Shared.Constants;

namespace SmartBuilding.Desktop.WPF.ViewModels;

/// <summary>Patrimoine : locateur (bailleur), bâtiment et appartements.</summary>
public partial class LocationsPatrimoineViewModel : BaseViewModel
{
    private readonly SettingsService _settingsService;
    private readonly PropertyStructureService _propertyStructureService;
    private readonly AppConfigurationService _appConfiguration;
    private readonly SessionService _session;
    private readonly ShellNavigationService _shellNavigation;

    [ObservableProperty] private int _selectedPatrimoineTab;
    [ObservableProperty] private string? _formError;
    [ObservableProperty] private string _pageTitle = "Bailleur";

    public bool CanManage => _session.HasPermission(PermissionCodes.LocationManage);

    public bool CanReturnToContract => _shellNavigation.HasPendingContractResume;

    public ObservableCollection<string> OwnerTypeChoices { get; } = ["Particulier", "Société"];

    public LocationsPatrimoineViewModel(
        SettingsService settingsService,
        PropertyStructureService propertyStructureService,
        AppConfigurationService appConfiguration,
        SessionService session,
        ShellNavigationService shellNavigation)
    {
        _settingsService = settingsService;
        _propertyStructureService = propertyStructureService;
        _appConfiguration = appConfiguration;
        _session = session;
        _shellNavigation = shellNavigation;
    }

    [RelayCommand]
    private async Task ReturnToContractAsync() => await _shellNavigation.ResumeContractFormAsync();

    public void Initialize(int tabIndex = 0)
    {
        SelectedPatrimoineTab = tabIndex;
        UpdatePageTitle();
        OnPropertyChanged(nameof(CanReturnToContract));
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        FormError = null;
        try
        {
            var data = await _settingsService.LoadAsync();
            ApplyProfileFromData(data);
            await LoadPropertyStructureAsync();
            await LoadGestionAsync();
        }
        catch (Exception ex)
        {
            FormError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectPatrimoineTab(object? parameter)
    {
        if (parameter is int i)
            SelectedPatrimoineTab = i;
        else if (int.TryParse(parameter?.ToString(), out var idx))
            SelectedPatrimoineTab = idx;
    }

    partial void OnSelectedPatrimoineTabChanged(int value) => UpdatePageTitle();

    private void UpdatePageTitle() => PageTitle = SelectedPatrimoineTab switch
    {
        1 => "Bâtiment",
        2 => "Appartements",
        3 => "Gestion",
        _ => "Bailleur"
    };

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CanManage)
        {
            FormError = "Permission refusée.";
            return;
        }

        if (!SbmsDialogService.Confirm("Enregistrer", "Confirmer l'enregistrement du patrimoine ?"))
            return;

        IsBusy = true;
        FormError = null;
        try
        {
            var structureError = await SavePropertyStructureAsync();
            if (!string.IsNullOrEmpty(structureError))
            {
                FormError = structureError;
                SelectedPatrimoineTab = 2;
                return;
            }

            await _settingsService.SaveBuildingProfileAsync(
                BuildProfileInput(),
                reloadApplicationConfiguration: false);
            await LoadGestionAsync();
            StatusMessage = "Patrimoine enregistré.";
            await LoadAsync();
            await _appConfiguration.ReloadAndApplyAsync();
        }
        catch (Exception ex)
        {
            FormError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
