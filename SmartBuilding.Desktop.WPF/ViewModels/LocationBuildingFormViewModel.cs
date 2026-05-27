using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Desktop.WPF.Services;
using SmartBuilding.Shared.Constants;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class LocationBuildingFormViewModel : BaseViewModel
{
    private readonly LocationsService _locationsService;
    private readonly ShellNavigationService _shellNavigation;
    private readonly SessionService _session;
    private Guid _editId;

    [ObservableProperty] private string _pageTitle = "Nouveau bâtiment";
    [ObservableProperty] private string _breadcrumb = "Locations / Nouveau bâtiment";
    [ObservableProperty] private bool _isEditMode;
    [ObservableProperty] private string? _formError;
    [ObservableProperty] private string _formCode = string.Empty;
    [ObservableProperty] private string _formName = string.Empty;
    [ObservableProperty] private string _formAddress = string.Empty;
    [ObservableProperty] private string _formType = LocationConstants.BuildingTypes.Office;
    [ObservableProperty] private string _formFloorsText = "0";
    [ObservableProperty] private string _formPremisesText = "0";

    public bool CanManage => _session.HasPermission(PermissionCodes.LocationManage);

    public ObservableCollection<string> BuildingTypeChoices { get; } =
    [
        LocationConstants.BuildingTypes.Residential,
        LocationConstants.BuildingTypes.Office,
        LocationConstants.BuildingTypes.Commercial,
        LocationConstants.BuildingTypes.MeetingRoom,
        LocationConstants.BuildingTypes.ConferenceRoom,
        LocationConstants.BuildingTypes.Mixed
    ];

    public LocationBuildingFormViewModel(
        LocationsService locationsService,
        ShellNavigationService shellNavigation,
        SessionService session)
    {
        _locationsService = locationsService;
        _shellNavigation = shellNavigation;
        _session = session;
    }

    public void Initialize(Guid? buildingId = null)
    {
        _editId = buildingId ?? Guid.Empty;
        IsEditMode = buildingId.HasValue && buildingId.Value != Guid.Empty;
        PageTitle = IsEditMode ? "Modifier le bâtiment" : "Nouveau bâtiment";
        Breadcrumb = IsEditMode ? "Locations / Modifier bâtiment" : "Locations / Nouveau bâtiment";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (!CanManage)
        {
            FormError = "Vous n'avez pas la permission de gérer les locations.";
            return;
        }

        IsBusy = true;
        try
        {
            FormError = null;
            if (!IsEditMode)
            {
                var count = (await _locationsService.GetBuildingsAsync()).Count;
                FormCode = $"BAT-{(count + 1):D3}";
                FormName = string.Empty;
                FormAddress = string.Empty;
                FormType = LocationConstants.BuildingTypes.Office;
                FormFloorsText = "0";
                FormPremisesText = "0";
                return;
            }

            var list = await _locationsService.GetBuildingsAsync();
            var b = list.FirstOrDefault(x => x.Id == _editId);
            if (b is null)
            {
                FormError = "Bâtiment introuvable.";
                return;
            }

            FormCode = b.Code;
            FormName = b.Name;
            FormAddress = b.Address;
            FormType = b.BuildingType;
            FormFloorsText = b.FloorCount.ToString();
            FormPremisesText = b.PremiseCount.ToString();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GoBackAsync() => await _shellNavigation.BackToLocationsAsync();

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CanManage) { FormError = "Permission refusée."; return; }
        if (string.IsNullOrWhiteSpace(FormName)) { FormError = "Le nom est obligatoire."; return; }
        if (!SbmsDialogService.Confirm(
                "Confirmation",
                IsEditMode
                    ? "Confirmer la mise à jour de ce bâtiment ?"
                    : "Confirmer la création de ce bâtiment ?"))
            return;

        FormError = null;
        int.TryParse(FormFloorsText, out var floors);
        int.TryParse(FormPremisesText, out var premises);

        IsBusy = true;
        try
        {
            string error;
            if (IsEditMode)
            {
                var list = await _locationsService.GetBuildingsAsync();
                var entity = list.FirstOrDefault(x => x.Id == _editId);
                if (entity is null) { FormError = "Bâtiment introuvable."; return; }
                entity.Name = FormName;
                entity.Address = FormAddress;
                entity.BuildingType = FormType;
                entity.FloorCount = floors;
                entity.PremiseCount = premises;
                error = await _locationsService.UpdateBuildingAsync(entity);
            }
            else
            {
                error = await _locationsService.CreateBuildingAsync(new Building
                {
                    Code = FormCode,
                    Name = FormName,
                    Address = FormAddress,
                    BuildingType = FormType,
                    FloorCount = floors,
                    PremiseCount = premises
                });
            }

            if (!string.IsNullOrEmpty(error))
            {
                FormError = error;
                return;
            }

            StatusMessage = IsEditMode ? "Bâtiment mis à jour." : "Bâtiment créé.";
            await _shellNavigation.BackToLocationsAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }
}
