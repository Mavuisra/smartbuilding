using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Desktop.WPF.Services;
using SmartBuilding.Shared.Constants;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class LocationTenantFormViewModel : BaseViewModel
{
    private readonly LocationsService _locationsService;
    private readonly ShellNavigationService _shellNavigation;
    private readonly SessionService _session;
    private Guid _editId;

    [ObservableProperty] private string _pageTitle = "Nouveau locataire";
    [ObservableProperty] private string _breadcrumb = "Locations / Nouveau locataire";
    [ObservableProperty] private bool _isEditMode;
    [ObservableProperty] private string? _formError;
    [ObservableProperty] private string _formDossier = string.Empty;
    [ObservableProperty] private string _formName = string.Empty;
    [ObservableProperty] private string _formPhone = string.Empty;
    [ObservableProperty] private string _formEmail = string.Empty;
    [ObservableProperty] private string _formCompany = string.Empty;
    [ObservableProperty] private string _formAddress = string.Empty;
    [ObservableProperty] private string _formNationality = string.Empty;
    [ObservableProperty] private string _formProfession = string.Empty;
    [ObservableProperty] private string _formStatus = LocationConstants.TenantStatus.Active;
    [ObservableProperty] private string _formEmergencyName = string.Empty;
    [ObservableProperty] private string _formEmergencyPhone = string.Empty;
    [ObservableProperty] private string _formNotes = string.Empty;

    public bool CanManage => _session.HasPermission(PermissionCodes.LocationManage);

    public ObservableCollection<string> TenantStatusChoices { get; } =
    [
        LocationConstants.TenantStatus.Active,
        LocationConstants.TenantStatus.Suspended,
        LocationConstants.TenantStatus.Terminated,
        LocationConstants.TenantStatus.Pending,
        LocationConstants.TenantStatus.Archived
    ];

    public LocationTenantFormViewModel(
        LocationsService locationsService,
        ShellNavigationService shellNavigation,
        SessionService session)
    {
        _locationsService = locationsService;
        _shellNavigation = shellNavigation;
        _session = session;
    }

    public void Initialize(Guid? tenantId = null)
    {
        _editId = tenantId ?? Guid.Empty;
        IsEditMode = tenantId.HasValue && tenantId.Value != Guid.Empty;
        PageTitle = IsEditMode ? "Modifier le locataire" : "Nouveau locataire";
        Breadcrumb = IsEditMode ? "Locations / Modifier locataire" : "Locations / Nouveau locataire";
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
                FormDossier = await _locationsService.GenerateNextDossierNumberAsync();
                return;
            }

            var tenant = await _locationsService.GetTenantAsync(_editId);
            if (tenant is null)
            {
                FormError = "Locataire introuvable.";
                return;
            }

            FormDossier = tenant.DossierNumber;
            FormName = tenant.Name;
            FormPhone = tenant.Phone;
            FormEmail = tenant.Email;
            FormCompany = tenant.Company ?? string.Empty;
            FormAddress = tenant.Address ?? string.Empty;
            FormNationality = tenant.Nationality ?? string.Empty;
            FormProfession = tenant.Profession ?? string.Empty;
            FormStatus = tenant.RentalStatus;
            FormEmergencyName = tenant.EmergencyContactName ?? string.Empty;
            FormEmergencyPhone = tenant.EmergencyContactPhone ?? string.Empty;
            FormNotes = tenant.Notes ?? string.Empty;
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
        if (string.IsNullOrWhiteSpace(FormPhone)) { FormError = "Le téléphone est obligatoire."; return; }
        if (!SbmsDialogService.Confirm(
                "Confirmation",
                IsEditMode
                    ? "Confirmer la mise à jour de ce locataire ?"
                    : "Confirmer l'enregistrement de ce locataire ?"))
            return;

        FormError = null;
        IsBusy = true;
        try
        {
            string error;
            if (IsEditMode)
            {
                var tenant = await _locationsService.GetTenantAsync(_editId);
                if (tenant is null) { FormError = "Locataire introuvable."; return; }
                tenant.Name = FormName;
                tenant.Phone = FormPhone;
                tenant.Email = FormEmail;
                tenant.Company = FormCompany;
                tenant.Address = FormAddress;
                tenant.Nationality = FormNationality;
                tenant.Profession = FormProfession;
                tenant.RentalStatus = FormStatus;
                tenant.EmergencyContactName = FormEmergencyName;
                tenant.EmergencyContactPhone = FormEmergencyPhone;
                tenant.Notes = FormNotes;
                error = await _locationsService.UpdateTenantAsync(tenant);
            }
            else
            {
                error = await _locationsService.CreateTenantAsync(new Tenant
                {
                    DossierNumber = FormDossier,
                    Name = FormName,
                    Phone = FormPhone,
                    Email = FormEmail,
                    Company = FormCompany,
                    Address = FormAddress,
                    Nationality = FormNationality,
                    Profession = FormProfession,
                    RentalStatus = FormStatus,
                    EmergencyContactName = FormEmergencyName,
                    EmergencyContactPhone = FormEmergencyPhone,
                    Notes = FormNotes
                });
            }

            if (!string.IsNullOrEmpty(error))
            {
                FormError = error;
                return;
            }

            StatusMessage = IsEditMode ? "Locataire mis à jour." : "Locataire enregistré.";
            await _shellNavigation.BackToLocationsAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }
}
