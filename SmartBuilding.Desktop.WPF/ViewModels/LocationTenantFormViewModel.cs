using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Desktop.WPF.Services;
using SmartBuilding.Shared.Constants;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class TenantDependentFormRow : ObservableObject
{
    [ObservableProperty] private string _fullName = string.Empty;
    [ObservableProperty] private string _relationship = LocationConstants.DependentRelationships.Child;
    [ObservableProperty] private string _dateOfBirthText = string.Empty;
    [ObservableProperty] private string _nationalId = string.Empty;
}

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
    [ObservableProperty] private string _formSecondaryPhone = string.Empty;
    [ObservableProperty] private string _formEmail = string.Empty;
    [ObservableProperty] private string _formCompany = string.Empty;
    [ObservableProperty] private string _formAddress = string.Empty;
    [ObservableProperty] private string _formPreviousAddress = string.Empty;
    [ObservableProperty] private string _formNationality = string.Empty;
    [ObservableProperty] private string _formNationalId = string.Empty;
    [ObservableProperty] private string _formIdDocumentType = string.Empty;
    [ObservableProperty] private string _formIdDocumentExpiryText = string.Empty;
    [ObservableProperty] private string _formProfession = string.Empty;
    [ObservableProperty] private string _formEmployer = string.Empty;
    [ObservableProperty] private string _formStatus = LocationConstants.TenantStatus.Active;
    [ObservableProperty] private string _formEmergencyName = string.Empty;
    [ObservableProperty] private string _formEmergencyPhone = string.Empty;
    [ObservableProperty] private string _formNotes = string.Empty;
    [ObservableProperty] private string _formDateOfBirthText = string.Empty;
    [ObservableProperty] private string _formGender = string.Empty;
    [ObservableProperty] private string _formMaritalStatus = string.Empty;
    [ObservableProperty] private string _formSpouseName = string.Empty;
    [ObservableProperty] private string _formChildrenCountText = "0";
    [ObservableProperty] private string _formPersonCountText = "1";
    [ObservableProperty] private string _formBusinessActivity = string.Empty;
    [ObservableProperty] private string _formTenantCategory = LocationConstants.TenantCategories.Individual;
    [ObservableProperty] private bool _hasFamilyMembers;

    public bool ShowSpouseName =>
        FormMaritalStatus.Contains("Mari", StringComparison.OrdinalIgnoreCase) ||
        FormMaritalStatus.Contains("Union", StringComparison.OrdinalIgnoreCase);

    public bool CanManage => _session.HasPermission(PermissionCodes.LocationManage);

    public ObservableCollection<string> TenantStatusChoices { get; } =
    [
        LocationConstants.TenantStatus.Active,
        LocationConstants.TenantStatus.Suspended,
        LocationConstants.TenantStatus.Terminated,
        LocationConstants.TenantStatus.Pending,
        LocationConstants.TenantStatus.Archived
    ];

    public ObservableCollection<string> DependentRelationshipChoices { get; } =
    [
        LocationConstants.DependentRelationships.Spouse,
        LocationConstants.DependentRelationships.Child,
        LocationConstants.DependentRelationships.Parent,
        LocationConstants.DependentRelationships.Other
    ];

    public ObservableCollection<string> GenderChoices { get; } =
    [
        string.Empty,
        LocationConstants.TenantGenders.Male,
        LocationConstants.TenantGenders.Female,
        LocationConstants.TenantGenders.Other
    ];

    public ObservableCollection<string> MaritalStatusChoices { get; } =
    [
        string.Empty,
        LocationConstants.TenantMaritalStatuses.Single,
        LocationConstants.TenantMaritalStatuses.Married,
        LocationConstants.TenantMaritalStatuses.Divorced,
        LocationConstants.TenantMaritalStatuses.Widowed,
        LocationConstants.TenantMaritalStatuses.UnionLibre,
        LocationConstants.TenantMaritalStatuses.Separated
    ];

    public ObservableCollection<string> TenantCategoryChoices { get; } =
    [
        LocationConstants.TenantCategories.Individual,
        LocationConstants.TenantCategories.Company
    ];

    public ObservableCollection<TenantDependentFormRow> Dependents { get; } = [];

    partial void OnFormMaritalStatusChanged(string value) => OnPropertyChanged(nameof(ShowSpouseName));

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
            Dependents.Clear();

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
            FormSecondaryPhone = tenant.SecondaryPhone ?? string.Empty;
            FormEmail = tenant.Email;
            FormCompany = tenant.Company ?? string.Empty;
            FormAddress = tenant.Address ?? string.Empty;
            FormPreviousAddress = tenant.PreviousAddress ?? string.Empty;
            FormNationality = tenant.Nationality ?? string.Empty;
            FormNationalId = tenant.NationalId ?? string.Empty;
            FormIdDocumentType = tenant.IdDocumentType ?? string.Empty;
            FormIdDocumentExpiryText = tenant.IdDocumentExpiry?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;
            FormProfession = tenant.Profession ?? string.Empty;
            FormEmployer = tenant.Employer ?? string.Empty;
            FormStatus = tenant.RentalStatus;
            FormEmergencyName = tenant.EmergencyContactName ?? string.Empty;
            FormEmergencyPhone = tenant.EmergencyContactPhone ?? string.Empty;
            FormNotes = tenant.Notes ?? string.Empty;
            FormDateOfBirthText = tenant.DateOfBirth?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;
            FormGender = tenant.Gender ?? string.Empty;
            FormMaritalStatus = tenant.MaritalStatus ?? string.Empty;
            FormSpouseName = tenant.SpouseName ?? string.Empty;
            FormChildrenCountText = tenant.ChildrenCount.ToString();
            FormPersonCountText = tenant.PersonCount > 0 ? tenant.PersonCount.ToString() : "1";
            FormBusinessActivity = tenant.BusinessActivity ?? string.Empty;
            FormTenantCategory = string.IsNullOrWhiteSpace(tenant.TenantCategory)
                ? LocationConstants.TenantCategories.Individual
                : tenant.TenantCategory;

            foreach (var d in tenant.Dependents.OrderBy(x => x.FullName))
            {
                Dependents.Add(new TenantDependentFormRow
                {
                    FullName = d.FullName,
                    Relationship = d.Relationship,
                    DateOfBirthText = d.DateOfBirth?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty,
                    NationalId = d.NationalId ?? string.Empty
                });
            }
            RefreshFamilyMembersFlag();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void AddDependent()
    {
        Dependents.Add(new TenantDependentFormRow());
        RefreshFamilyMembersFlag();
    }

    [RelayCommand]
    private void RemoveDependent(TenantDependentFormRow? row)
    {
        if (row is not null)
            Dependents.Remove(row);
        RefreshFamilyMembersFlag();
    }

    private void RefreshFamilyMembersFlag() => HasFamilyMembers = Dependents.Count > 0;

    [RelayCommand]
    private async Task GoBackAsync()
    {
        if (_shellNavigation.HasPendingContractResume)
        {
            await _shellNavigation.ResumeContractFormAsync();
            return;
        }

        if (IsEditMode && _editId != Guid.Empty)
            await _shellNavigation.OpenTenantDetailAsync(_editId);
        else
            await _shellNavigation.BackToTenantsAsync();
    }

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
            var idExpiry = ParseOptionalDate(FormIdDocumentExpiryText);
            var dependentDrafts = Dependents
                .Where(d => !string.IsNullOrWhiteSpace(d.FullName))
                .Select(d => new TenantDependentDraft
                {
                    FullName = d.FullName,
                    Relationship = d.Relationship,
                    DateOfBirth = ParseOptionalDate(d.DateOfBirthText),
                    NationalId = string.IsNullOrWhiteSpace(d.NationalId) ? null : d.NationalId
                })
                .ToList();

            string error;
            Guid savedId;
            if (IsEditMode)
            {
                var tenant = await _locationsService.GetTenantAsync(_editId);
                if (tenant is null) { FormError = "Locataire introuvable."; return; }
                ApplyFormToTenant(tenant, idExpiry);
                error = await _locationsService.UpdateTenantAsync(tenant);
                if (string.IsNullOrEmpty(error))
                    error = await _locationsService.ReplaceTenantDependentsAsync(_editId, dependentDrafts);
                savedId = _editId;
            }
            else
            {
                var tenant = new Tenant();
                ApplyFormToTenant(tenant, idExpiry);
                tenant.DossierNumber = FormDossier;
                error = await _locationsService.CreateTenantAsync(tenant);
                if (string.IsNullOrEmpty(error) && dependentDrafts.Count > 0)
                    error = await _locationsService.ReplaceTenantDependentsAsync(tenant.Id, dependentDrafts);
                savedId = tenant.Id;
            }

            if (!string.IsNullOrEmpty(error))
            {
                FormError = error;
                return;
            }

            StatusMessage = IsEditMode ? "Locataire mis à jour." : "Locataire enregistré.";

            if (_shellNavigation.HasPendingContractResume)
            {
                _shellNavigation.SetSelectTenantOnContractResume(savedId);
                await _shellNavigation.ResumeContractFormAsync();
                return;
            }

            await _shellNavigation.OpenTenantDetailAsync(savedId);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFormToTenant(Tenant tenant, DateTime? idExpiry)
    {
        tenant.Name = FormName;
        tenant.Phone = FormPhone;
        tenant.SecondaryPhone = FormSecondaryPhone;
        tenant.Email = FormEmail;
        tenant.Company = FormCompany;
        tenant.Address = FormAddress;
        tenant.PreviousAddress = FormPreviousAddress;
        tenant.Nationality = FormNationality;
        tenant.NationalId = FormNationalId;
        tenant.IdDocumentType = FormIdDocumentType;
        tenant.IdDocumentExpiry = idExpiry;
        tenant.Profession = FormProfession;
        tenant.Employer = FormEmployer;
        tenant.RentalStatus = FormStatus;
        tenant.EmergencyContactName = FormEmergencyName;
        tenant.EmergencyContactPhone = FormEmergencyPhone;
        tenant.Notes = FormNotes;
        tenant.TenantCategory = FormTenantCategory;
        tenant.DateOfBirth = ParseOptionalDate(FormDateOfBirthText);
        tenant.Gender = FormGender.Trim();
        tenant.MaritalStatus = FormMaritalStatus.Trim();
        tenant.SpouseName = string.IsNullOrWhiteSpace(FormSpouseName) ? null : FormSpouseName.Trim();
        tenant.ChildrenCount = ParseChildrenCount(FormChildrenCountText);
        tenant.BusinessActivity = string.IsNullOrWhiteSpace(FormBusinessActivity) ? null : FormBusinessActivity.Trim();
        var dependentCount = Dependents.Count(d => !string.IsNullOrWhiteSpace(d.FullName));
        tenant.PersonCount = ParsePersonCount(FormPersonCountText, dependentCount);
    }

    private static int ParseChildrenCount(string text) =>
        int.TryParse(text, out var n) && n >= 0 ? n : 0;

    private static int ParsePersonCount(string text, int dependentCount)
    {
        if (int.TryParse(text, out var n) && n > 0)
            return Math.Max(n, 1 + dependentCount);
        return Math.Max(1, 1 + dependentCount);
    }

    private static DateTime? ParseOptionalDate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        if (DateTime.TryParseExact(text.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d.Date;
        return DateTime.TryParse(text, CultureInfo.GetCultureInfo("fr-FR"), DateTimeStyles.None, out var parsed)
            ? parsed.Date
            : null;
    }
}
