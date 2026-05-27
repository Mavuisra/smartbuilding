using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Shared.Constants;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class LocationContractFormViewModel
{
    [ObservableProperty] private string _formQuickDossier = string.Empty;
    [ObservableProperty] private string _formQuickTenantName = string.Empty;
    [ObservableProperty] private string _formQuickTenantPhone = string.Empty;
    [ObservableProperty] private string _formQuickTenantEmail = string.Empty;
    [ObservableProperty] private string _formQuickTenantCompany = string.Empty;

    [ObservableProperty] private string _formPremiseCode = string.Empty;
    [ObservableProperty] private string _formPremiseName = string.Empty;
    [ObservableProperty] private string _formPremiseBuilding = string.Empty;
    [ObservableProperty] private string _formPremiseFloor = string.Empty;
    [ObservableProperty] private string _formPremiseType = "Bureau";
    [ObservableProperty] private string _formPremiseAreaText = "0";
    [ObservableProperty] private string _formPremiseRentText = "0";

    [ObservableProperty] private bool _isTenantQuickPanelVisible;
    [ObservableProperty] private bool _isPremiseQuickPanelVisible;

    public ObservableCollection<string> QuickBuildings { get; } = [];

    [RelayCommand]
    private void ToggleTenantQuickPanel()
    {
        IsTenantQuickPanelVisible = !IsTenantQuickPanelVisible;
        if (IsTenantQuickPanelVisible)
            IsPremiseQuickPanelVisible = false;
        if (IsTenantQuickPanelVisible)
            _ = EnsureQuickCreateFieldsAsync();
    }

    [RelayCommand]
    private void TogglePremiseQuickPanel()
    {
        IsPremiseQuickPanelVisible = !IsPremiseQuickPanelVisible;
        if (IsPremiseQuickPanelVisible)
            IsTenantQuickPanelVisible = false;
        if (IsPremiseQuickPanelVisible)
            _ = EnsureQuickCreateFieldsAsync();
    }

    [RelayCommand]
    private void SaveDraft()
    {
        FormError = null;
        StatusMessage = "Brouillon enregistré — vous pouvez reprendre plus tard.";
    }

    [RelayCommand]
    private async Task SaveQuickTenantAsync()
    {
        if (!CanManage)
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

        FormError = null;
        IsBusy = true;
        try
        {
            var error = await _locationsService.CreateTenantAsync(new Tenant
            {
                DossierNumber = FormQuickDossier,
                Name = FormQuickTenantName.Trim(),
                Phone = FormQuickTenantPhone.Trim(),
                Email = FormQuickTenantEmail.Trim(),
                Company = FormQuickTenantCompany.Trim(),
                RentalStatus = LocationConstants.TenantStatus.Active
            });

            if (!string.IsNullOrEmpty(error))
            {
                FormError = error;
                return;
            }

            Tenants.Clear();
            foreach (var t in await _locationsService.GetTenantsAsync())
                Tenants.Add(t);

            var created = Tenants.FirstOrDefault(t =>
                string.Equals(t.Name, FormQuickTenantName.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? Tenants.LastOrDefault();

            FormSelectedTenant = created;
            IsTenantQuickPanelVisible = false;
            StatusMessage = $"Locataire « {FormQuickTenantName.Trim()} » créé et sélectionné.";
            await ResetQuickTenantFieldsAsync();
            RefreshContractSummaryDisplays();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveQuickPremiseAsync()
    {
        if (!CanManage)
        {
            FormError = "Permission refusée.";
            return;
        }

        if (string.IsNullOrWhiteSpace(FormPremiseName))
        {
            FormError = "Le nom du local est obligatoire.";
            return;
        }

        if (string.IsNullOrWhiteSpace(FormPremiseCode))
        {
            FormError = "Le code du local est obligatoire.";
            return;
        }

        FormError = null;
        IsBusy = true;
        try
        {
            if (!decimal.TryParse(FormPremiseAreaText.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var area))
                area = 0;
            if (!decimal.TryParse(FormPremiseRentText.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var rent))
                rent = 0;

            var error = await _locationsService.CreatePremiseAsync(new Premise
            {
                Code = FormPremiseCode.Trim(),
                Name = FormPremiseName.Trim(),
                Building = string.IsNullOrWhiteSpace(FormPremiseBuilding) ? "Tour SBMS" : FormPremiseBuilding.Trim(),
                Floor = FormPremiseFloor.Trim(),
                PremiseType = string.IsNullOrWhiteSpace(FormPremiseType) ? "Bureau" : FormPremiseType,
                AreaSqM = area,
                MonthlyRent = rent,
                IsOccupied = false,
                OccupancyStatus = LocationConstants.PremiseOccupancyStatus.Available
            });

            if (!string.IsNullOrEmpty(error))
            {
                FormError = error;
                return;
            }

            AvailablePremises.Clear();
            foreach (var p in await _locationsService.GetAvailablePremisesAsync())
                AvailablePremises.Add(p);

            var created = AvailablePremises.FirstOrDefault(p =>
                string.Equals(p.Code, FormPremiseCode.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? AvailablePremises.LastOrDefault();

            FormSelectedPremise = created;
            IsPremiseQuickPanelVisible = false;
            StatusMessage = $"Local « {FormPremiseName.Trim()} » créé et sélectionné.";
            await ResetQuickPremiseFieldsAsync();
            await LoadPremiseStatsAsync();
            RefreshContractSummaryDisplays();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task EnsureQuickCreateFieldsAsync()
    {
        if (string.IsNullOrWhiteSpace(FormQuickDossier))
            FormQuickDossier = await _locationsService.GenerateNextDossierNumberAsync();
        if (string.IsNullOrWhiteSpace(FormPremiseCode))
            FormPremiseCode = await _locationsService.GenerateNextCodeAsync();
    }

    private async Task ResetQuickTenantFieldsAsync()
    {
        FormQuickTenantName = string.Empty;
        FormQuickTenantPhone = string.Empty;
        FormQuickTenantEmail = string.Empty;
        FormQuickTenantCompany = string.Empty;
        FormQuickDossier = await _locationsService.GenerateNextDossierNumberAsync();
    }

    private async Task ResetQuickPremiseFieldsAsync()
    {
        FormPremiseName = string.Empty;
        FormPremiseFloor = string.Empty;
        FormPremiseType = ContractTypes.FirstOrDefault() ?? "Bureau";
        FormPremiseAreaText = "0";
        FormPremiseRentText = "0";
        FormPremiseCode = await _locationsService.GenerateNextCodeAsync();
        FormPremiseBuilding = QuickBuildings.FirstOrDefault() ?? "Tour SBMS";
    }

    private async Task LoadQuickBuildingsAsync()
    {
        QuickBuildings.Clear();
        foreach (var b in await _locationsService.GetBuildingsAsync())
        {
            if (!string.IsNullOrWhiteSpace(b.Name))
                QuickBuildings.Add(b.Name);
        }

        if (QuickBuildings.Count == 0)
            QuickBuildings.Add("Tour SBMS");
    }
}
