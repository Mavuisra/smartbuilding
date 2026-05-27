using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;
using SmartBuilding.Shared.Constants;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class LocationRentFormViewModel : BaseViewModel
{
    private readonly LocationsService _locationsService;
    private readonly ShellNavigationService _shellNavigation;
    private readonly SessionService _session;
    private List<LocationsPickItem> _allContracts = [];

    [ObservableProperty] private string _pageTitle = "Paiement de loyer";
    [ObservableProperty] private string _breadcrumb = "Locations / Paiement de loyer";
    [ObservableProperty] private string? _formError;
    [ObservableProperty] private LocationsPickItem? _selectedTenant;
    [ObservableProperty] private LocationsPickItem? _selectedPremise;
    [ObservableProperty] private string _formAmountText = "0";
    [ObservableProperty] private int _formYear;
    [ObservableProperty] private int _formMonth;
    [ObservableProperty] private DateTime _formPaymentDate = DateTime.Today;
    [ObservableProperty] private string _formPaymentMethod = "Virement bancaire";
    [ObservableProperty] private string _formTransactionReference = string.Empty;
    [ObservableProperty] private string _formPaymentStatus = LocationConstants.PaymentStatus.Paid;
    [ObservableProperty] private bool _generateReceipt = true;
    [ObservableProperty] private string _contractSummary = string.Empty;
    [ObservableProperty] private string _periodPaymentSummary = string.Empty;
    [ObservableProperty] private bool _isMonthFullyPaid;

    public bool CanManage => _session.HasPermission(PermissionCodes.LocationManage);
    public bool HasSelectedTenant => SelectedTenant is not null;
    public bool HasSelectedPremise => SelectedPremise is not null;
    public bool CanShowPaymentDetails => HasSelectedTenant && HasSelectedPremise;
    public bool CanRecordPayment => CanShowPaymentDetails && !IsMonthFullyPaid && CanManage;

    public ObservableCollection<LocationsPickItem> Tenants { get; } = [];
    public ObservableCollection<LocationsPickItem> TenantPremises { get; } = [];
    public ObservableCollection<string> PaymentMethods { get; } =
        ["Virement bancaire", "Mobile money", "Espèces", "Chèque", "Carte bancaire"];
    public ObservableCollection<string> PaymentStatuses { get; } =
    [
        LocationConstants.PaymentStatus.Paid,
        LocationConstants.PaymentStatus.Partial,
        LocationConstants.PaymentStatus.Pending,
        LocationConstants.PaymentStatus.Late
    ];
    public ObservableCollection<int> MonthOptions { get; } = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
    public ObservableCollection<int> YearOptions { get; } = [];

    public LocationRentFormViewModel(
        LocationsService locationsService,
        ShellNavigationService shellNavigation,
        SessionService session)
    {
        _locationsService = locationsService;
        _shellNavigation = shellNavigation;
        _session = session;
        var today = DateTime.Today;
        _formYear = today.Year;
        _formMonth = today.Month;
        for (var y = today.Year - 2; y <= today.Year + 1; y++)
            YearOptions.Add(y);
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
            FormAmountText = "0";
            FormPaymentDate = DateTime.Today;
            FormPaymentMethod = PaymentMethods.First();
            FormPaymentStatus = LocationConstants.PaymentStatus.Paid;
            GenerateReceipt = true;
            SelectedTenant = null;
            SelectedPremise = null;
            TenantPremises.Clear();
            ContractSummary = string.Empty;

            _allContracts = (await _locationsService.GetActiveContractsAsync()).ToList();

            Tenants.Clear();
            var tenantIds = _allContracts.Select(c => c.TenantId).Distinct().ToHashSet();
            var tenantsById = (await _locationsService.GetTenantsAsync()).ToDictionary(t => t.Id);
            foreach (var tenantId in tenantIds.OrderBy(id =>
                         tenantsById.TryGetValue(id, out var t) ? t.Name : string.Empty))
            {
                if (tenantsById.TryGetValue(tenantId, out var tenant))
                    Tenants.Add(tenant);
            }

            if (Tenants.Count == 0)
                FormError = "Aucun contrat actif — créez d'abord un contrat.";

            var corrected = await _locationsService.CancelOverpaidRentPaymentsAsync();
            if (corrected > 0)
                StatusMessage = $"{corrected} double(s) paiement(s) corrigé(s) automatiquement.";
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
        if (SelectedTenant is null)
        {
            FormError = "Sélectionnez un locataire.";
            return;
        }

        if (SelectedPremise is null)
        {
            FormError = "Sélectionnez un local.";
            return;
        }

        if (IsMonthFullyPaid)
        {
            FormError = PeriodPaymentSummary;
            return;
        }

        if (!decimal.TryParse(FormAmountText.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
            amount = 0;
        if (amount <= 0)
        {
            FormError = "Le montant doit être supérieur à zéro.";
            return;
        }

        if (!SbmsDialogService.Confirm(
                "Confirmer le paiement",
                $"Enregistrer un paiement de {MoneyFormatter.Format(amount)} pour {SelectedTenant.Label} ?"))
            return;

        FormError = null;
        IsBusy = true;
        try
        {
            var error = await _locationsService.RecordRentPaymentDetailedAsync(
                SelectedPremise.Id,
                amount,
                FormYear,
                FormMonth,
                FormPaymentDate,
                FormPaymentMethod,
                string.IsNullOrWhiteSpace(FormTransactionReference) ? null : FormTransactionReference,
                FormPaymentStatus);

            if (!string.IsNullOrEmpty(error))
            {
                FormError = error;
                return;
            }

            var receiptPath = await _locationsService.GetReceiptPdfPathForPeriodAsync(
                SelectedPremise.Id, FormYear, FormMonth);
            if (GenerateReceipt && !string.IsNullOrWhiteSpace(receiptPath) && File.Exists(receiptPath))
            {
                Process.Start(new ProcessStartInfo(receiptPath) { UseShellExecute = true });
                StatusMessage = $"Paiement enregistré — quittance générée.";
            }
            else
                StatusMessage = "Paiement enregistré avec succès.";
            await _shellNavigation.BackToLocationsAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedTenantChanged(LocationsPickItem? value)
    {
        TenantPremises.Clear();
        SelectedPremise = null;
        ContractSummary = string.Empty;
        OnPropertyChanged(nameof(HasSelectedTenant));
        OnPropertyChanged(nameof(CanShowPaymentDetails));

        if (value is null)
            return;

        var tenantId = ResolveTenantId(value);
        foreach (var contract in _allContracts.Where(c => c.TenantId == tenantId))
            TenantPremises.Add(ToPremisePickItem(contract));

        if (TenantPremises.Count == 1)
            SelectedPremise = TenantPremises[0];

        _ = RefreshPeriodPaymentInfoAsync();
    }

    partial void OnFormYearChanged(int value) => _ = RefreshPeriodPaymentInfoAsync();
    partial void OnFormMonthChanged(int value) => _ = RefreshPeriodPaymentInfoAsync();

    private static Guid ResolveTenantId(LocationsPickItem tenant) =>
        tenant.TenantId != Guid.Empty ? tenant.TenantId : tenant.Id;

    private static LocationsPickItem ToPremisePickItem(LocationsPickItem contract) =>
        new()
        {
            Id = contract.Id,
            TenantId = contract.TenantId,
            PremiseId = contract.PremiseId,
            Label = string.IsNullOrWhiteSpace(contract.Code)
                ? contract.Label
                : $"{contract.Code} — {contract.Name}",
            Code = contract.Code,
            Name = contract.Name,
            MonthlyRent = contract.MonthlyRent,
            RentDisplay = contract.RentDisplay
        };

    partial void OnSelectedPremiseChanged(LocationsPickItem? value)
    {
        OnPropertyChanged(nameof(HasSelectedPremise));
        OnPropertyChanged(nameof(CanShowPaymentDetails));
        OnPropertyChanged(nameof(CanRecordPayment));
        if (value is null)
        {
            ContractSummary = string.Empty;
            PeriodPaymentSummary = string.Empty;
            IsMonthFullyPaid = false;
            return;
        }

        var contract = _allContracts.FirstOrDefault(c => c.Id == value.Id);
        if (contract is not null)
            ContractSummary = $"Loyer mensuel : {contract.RentDisplay}";

        _ = RefreshPeriodPaymentInfoAsync();
    }

    private async Task RefreshPeriodPaymentInfoAsync()
    {
        if (SelectedPremise is null)
            return;

        var info = await _locationsService.GetRentPeriodPaymentInfoAsync(
            SelectedPremise.Id, FormYear, FormMonth);

        PeriodPaymentSummary = info.Summary;
        IsMonthFullyPaid = info.IsFullyPaid;
        OnPropertyChanged(nameof(CanRecordPayment));

        if (info.IsFullyPaid)
        {
            FormAmountText = "0";
            FormError = "Ce mois est déjà payé — aucun nouvel encaissement possible.";
        }
        else if (info.RemainingDue > 0)
        {
            FormAmountText = info.RemainingDue.ToString("F2", CultureInfo.InvariantCulture);
            FormError = null;
        }
    }

}
