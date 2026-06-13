using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Desktop.WPF.Helpers;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;
using SmartBuilding.Shared.Constants;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class LocationContractFormViewModel : BaseViewModel
{
    private readonly LocationsService _locationsService;
    private readonly ShellNavigationService _shellNavigation;
    private readonly SessionService _session;

    [ObservableProperty] private string _pageTitle = "Nouveau contrat";
    [ObservableProperty] private string _breadcrumb = "Locations / Nouveau contrat";
    [ObservableProperty] private string? _formError;
    [ObservableProperty] private string _formContractNumber = string.Empty;
    [ObservableProperty] private LocationsPickItem? _formSelectedPremise;
    [ObservableProperty] private LocationsPickItem? _formSelectedTenant;
    [ObservableProperty] private DateTime _formStart = DateTime.Today;
    [ObservableProperty] private DateTime _formEnd = DateTime.Today.AddYears(1);
    [ObservableProperty] private string _formRentText = "0";
    [ObservableProperty] private string _formDepositText = "0";
    [ObservableProperty] private string _formContractType = LocationConstants.DefaultContractType;
    [ObservableProperty] private string _formPaymentFrequency = "Mensuelle";
    [ObservableProperty] private string _formPaymentMethod = "Virement bancaire";
    [ObservableProperty] private string _formClauses = "Accès 24/7, entretien inclus, internet inclus";
    [ObservableProperty] private bool _formAutomaticRenewal = true;
    [ObservableProperty] private int _currentStep;

    public bool CanManage => _session.HasPermission(PermissionCodes.LocationManage);
    public bool IsFirstStep => CurrentStep <= 0;
    public bool IsLastStep => CurrentStep >= 4;
    public string NextButtonLabel => IsLastStep ? "Créer le contrat" : "Suivant";
    public string AutomaticRenewalDisplay => FormAutomaticRenewal ? "Oui" : "Non";

    public IReadOnlyList<LocationWizardStepItem> WizardSteps { get; } =
    [
        new() { Index = 0, Number = "1", Title = "Locataire", Subtitle = "Informations personnelles" },
        new() { Index = 1, Number = "2", Title = "Appartement", Subtitle = "Sélection de l'espace" },
        new() { Index = 2, Number = "3", Title = "Contrat", Subtitle = "Détails du contrat" },
        new() { Index = 3, Number = "4", Title = "Garantie & Paiement", Subtitle = "Conditions financières" },
        new() { Index = 4, Number = "5", Title = "Documents", Subtitle = "Résumé et confirmation" }
    ];
    public bool HasSelectedTenant => FormSelectedTenant is not null;
    public bool HasSelectedPremise => FormSelectedPremise is not null;
    public bool CanShowContractDetails => HasSelectedTenant && HasSelectedPremise;

    public ObservableCollection<LocationsPickItem> AvailablePremises { get; } = [];
    public ObservableCollection<LocationsPickItem> Tenants { get; } = [];
    public ObservableCollection<string> ContractTypes { get; } = new(LocationConstants.ContractTypes.All);
    public ObservableCollection<string> PaymentFrequencies { get; } = ["Mensuelle", "Trimestrielle", "Semestrielle", "Annuelle"];
    public ObservableCollection<string> PaymentMethods { get; } = ["Virement bancaire", "Mobile money", "Espèces", "Chèque"];

    [ObservableProperty] private string _selectedTenantName = "Aucun locataire";
    [ObservableProperty] private string _selectedTenantPhone = "—";
    [ObservableProperty] private string _selectedTenantEmail = "—";
    [ObservableProperty] private string _selectedPremiseName = "Aucun espace";
    [ObservableProperty] private string _selectedPremiseSubtitle = "Sélectionnez un espace";
    [ObservableProperty] private string _selectedPremiseRent = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _selectedPremiseSelectionSummary = "Sélectionnez un espace";
    [ObservableProperty] private string _contractDurationDisplay = "12 mois";
    [ObservableProperty] private string _nextPaymentDateDisplay = string.Empty;
    [ObservableProperty] private string _rentSummaryDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _depositSummaryDisplay = MoneyFormatter.ZeroDisplay;

    [ObservableProperty] private int _statsTotalPremises;
    [ObservableProperty] private int _statsAvailablePremises;
    [ObservableProperty] private int _statsOccupiedPremises;
    [ObservableProperty] private int _statsPendingPremises;
    [ObservableProperty] private string _statsOccupancyRateDisplay = "0 %";

    public LocationContractFormViewModel(
        LocationsService locationsService,
        ShellNavigationService shellNavigation,
        SessionService session)
    {
        _locationsService = locationsService;
        _shellNavigation = shellNavigation;
        _session = session;
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
            FormContractNumber = await _locationsService.GenerateNextContractNumberAsync();
            FormStart = DateTime.Today;
            FormEnd = DateTime.Today.AddYears(1);
            FormRentText = "0";
            FormDepositText = "0";
            FormContractType = LocationConstants.DefaultContractType;
            FormPaymentFrequency = PaymentFrequencies.First();
            FormPaymentMethod = PaymentMethods.First();
            FormClauses = "Accès 24/7, entretien inclus, internet inclus";
            FormAutomaticRenewal = true;

            AvailablePremises.Clear();
            foreach (var p in await _locationsService.GetAvailablePremisesAsync())
                AvailablePremises.Add(p);

            Tenants.Clear();
            foreach (var t in await _locationsService.GetTenantsAsync())
                Tenants.Add(t);

            FormSelectedPremise = null;
            FormSelectedTenant = null;
            CurrentStep = 0;
            NotifyStepChanged();

            if (AvailablePremises.Count == 0)
                FormError = "Aucun local disponible. Créez d'abord un local libre.";
            else if (Tenants.Count == 0)
                FormError = "Aucun locataire. Ajoutez d'abord un locataire.";

            await LoadPremiseStatsAsync();
            RefreshContractSummaryDisplays();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GoBackAsync() => await _shellNavigation.BackToLocationsAsync();

    /// <summary>Ouvre la fiche complète locateur (menu Location → Locateur).</summary>
    [RelayCommand]
    private async Task OpenCreateTenantFormAsync()
    {
        _shellNavigation.BeginContractSubFlow(this);
        await _shellNavigation.OpenTenantFormAsync(null);
    }

    /// <summary>Ouvre le patrimoine — onglet Bâtiment (étages / locaux).</summary>
    [RelayCommand]
    private async Task OpenCreatePremiseInPatrimoineAsync()
    {
        _shellNavigation.BeginContractSubFlow(this);
        await _shellNavigation.OpenPatrimoineTabAsync(1);
    }

    public void ApplyTenantSelection(Guid tenantId) =>
        FormSelectedTenant = Tenants.FirstOrDefault(t => t.Id == tenantId);

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
            _ = SaveAsync();
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
    private async Task SaveAsync()
    {
        if (!CanManage) { FormError = "Permission refusée."; return; }
        if (FormSelectedPremise is null || FormSelectedTenant is null)
        {
            FormError = "Sélectionnez un local et un locataire.";
            return;
        }
        FormContractNumber = await _locationsService.GenerateNextContractNumberAsync();

        if (!SbmsDialogService.Confirm(
                "Créer le contrat",
                $"Créer le contrat {FormContractNumber} pour {FormSelectedTenant.Label} ?"))
            return;

        FormError = null;
        if (!decimal.TryParse(FormRentText.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var rent))
            rent = 0;
        if (!decimal.TryParse(FormDepositText.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var deposit))
            deposit = 0;

        IsBusy = true;
        try
        {
            var result = await _locationsService.CreateContractAsync(
                FormSelectedPremise.Id,
                FormSelectedTenant.Id,
                FormStart,
                FormEnd,
                rent,
                deposit,
                FormContractType,
                FormClauses,
                FormPaymentFrequency,
                FormPaymentMethod);

            if (!string.IsNullOrEmpty(result.Error))
            {
                FormError = result.Error;
                return;
            }

            if (!string.IsNullOrWhiteSpace(result.SummaryPdfPath) && File.Exists(result.SummaryPdfPath))
            {
                Process.Start(new ProcessStartInfo(result.SummaryPdfPath) { UseShellExecute = true });
                StatusMessage = "Contrat créé — récapitulatif PDF généré.";
            }
            else
                StatusMessage = "Contrat créé — en attente de validation.";

            await _shellNavigation.BackToLocationsAsync();
        }
        catch (Exception ex)
        {
            FormError = SmartBuilding.Infrastructure.Services.DbSaveExceptionTranslator.ToUserMessage(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnFormSelectedPremiseChanged(LocationsPickItem? value)
    {
        OnPropertyChanged(nameof(HasSelectedPremise));
        OnPropertyChanged(nameof(CanShowContractDetails));
        if (value is not null && value.MonthlyRent > 0)
            FormRentText = value.MonthlyRent.ToString("0");

        RefreshContractSummaryDisplays();
    }

    partial void OnFormSelectedTenantChanged(LocationsPickItem? value)
    {
        OnPropertyChanged(nameof(HasSelectedTenant));
        OnPropertyChanged(nameof(CanShowContractDetails));
        if (value is null)
            FormSelectedPremise = null;
        RefreshContractSummaryDisplays();
    }

    partial void OnFormStartChanged(DateTime value) => RefreshContractSummaryDisplays();

    partial void OnFormEndChanged(DateTime value) => RefreshContractSummaryDisplays();

    partial void OnFormRentTextChanged(string value) => RefreshContractSummaryDisplays();

    partial void OnFormDepositTextChanged(string value) => RefreshContractSummaryDisplays();

    partial void OnCurrentStepChanged(int value) => NotifyStepChanged();

    partial void OnFormAutomaticRenewalChanged(bool value) =>
        OnPropertyChanged(nameof(AutomaticRenewalDisplay));

    private bool ValidateStep(int step)
    {
        FormError = step switch
        {
            0 when FormSelectedTenant is null => "Sélectionnez un locataire.",
            1 when FormSelectedTenant is null => "Sélectionnez d'abord un locataire.",
            1 when FormSelectedPremise is null => "Sélectionnez un espace disponible.",
            2 when FormEnd.Date < FormStart.Date => "La date de fin doit être après la date de début.",
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

    private void RefreshContractSummaryDisplays()
    {
        SelectedTenantName = FormSelectedTenant?.Name ?? FormSelectedTenant?.Label ?? "Aucun locataire";
        SelectedTenantPhone = EmptyToDash(FormSelectedTenant?.Phone);
        SelectedTenantEmail = EmptyToDash(FormSelectedTenant?.Email);
        SelectedPremiseName = FormSelectedPremise?.Name ?? FormSelectedPremise?.Label ?? "Aucun espace";
        SelectedPremiseSubtitle = FormSelectedPremise is null
            ? "Sélectionnez un espace"
            : $"{FormSelectedPremise.Building} · {FormSelectedPremise.Floor}";
        SelectedPremiseRent = FormSelectedPremise?.RentDisplay ?? FormatMoney(ParseAmount(FormRentText));
        ContractDurationDisplay =
            $"{Math.Max(1, ((FormEnd.Year - FormStart.Year) * 12) + FormEnd.Month - FormStart.Month)} mois";
        NextPaymentDateDisplay = FormStart.AddMonths(1).ToString("dd/MM/yyyy");
        RentSummaryDisplay = FormatMoney(ParseAmount(FormRentText));
        DepositSummaryDisplay = FormatMoney(ParseAmount(FormDepositText));
        SelectedPremiseSelectionSummary = FormSelectedPremise is null
            ? "Espace sélectionné : sélectionnez un espace"
            : $"Espace sélectionné : {SelectedPremiseName} - {RentSummaryDisplay} / mois";
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

    private async Task LoadPremiseStatsAsync()
    {
        var stats = await _locationsService.GetPremiseOccupancyStatsAsync();
        StatsTotalPremises = stats.TotalPremises;
        StatsAvailablePremises = stats.AvailablePremises;
        StatsOccupiedPremises = stats.OccupiedPremises;
        StatsPendingPremises = stats.PendingPremises;
        StatsOccupancyRateDisplay = stats.OccupancyRateDisplay;
    }
}
