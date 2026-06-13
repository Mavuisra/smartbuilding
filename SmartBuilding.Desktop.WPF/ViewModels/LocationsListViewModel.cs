using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Desktop.WPF.Helpers;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;
using SmartBuilding.Shared.Constants;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class LocationsListViewModel : BaseViewModel
{
    private const string AllStatuses = "Tous statuts";

    private readonly LocationsService _locationsService;
    private readonly ShellNavigationService _shellNavigation;
    private readonly AppConfigurationService _appConfiguration;
    private readonly SessionService _session;

    private List<LocationsContractItem> _allContracts = [];
    private List<LocationsPremiseItem> _allPremises = [];
    private List<LocationsPaymentItem> _allPayments = [];
    private List<LocationsGuaranteeItem> _allGuarantees = [];

    [ObservableProperty] private int _selectedTab;
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _filterStatus = AllStatuses;
    [ObservableProperty] private string? _formError;
    [ObservableProperty] private string _pageSubtitle = "Contrats de location";

    [ObservableProperty] private bool _isPremiseFormOpen;
    [ObservableProperty] private bool _isContractFormOpen;
    [ObservableProperty] private bool _isPaymentFormOpen;
    [ObservableProperty] private bool _isGuaranteeFormOpen;
    [ObservableProperty] private bool _isEditMode;
    [ObservableProperty] private string _premiseFormTitle = "Nouveau local";

    [ObservableProperty] private Guid _editPremiseId;
    [ObservableProperty] private string _formCode = string.Empty;
    [ObservableProperty] private string _formName = string.Empty;
    [ObservableProperty] private string _formBuilding = string.Empty;
    [ObservableProperty] private string _formFloor = string.Empty;
    [ObservableProperty] private string _formType = LocationConstants.DefaultPremiseType;
    [ObservableProperty] private string _formRentText = "0";

    [ObservableProperty] private Guid _editContractId;
    [ObservableProperty] private string _formContractNumber = string.Empty;
    [ObservableProperty] private string _formContractType = LocationConstants.DefaultContractType;
    [ObservableProperty] private string _formStartDate = string.Empty;
    [ObservableProperty] private string _formEndDate = string.Empty;
    [ObservableProperty] private string _formContractRentText = "0";
    [ObservableProperty] private string _formDepositText = "0";

    [ObservableProperty] private Guid _editPaymentId;
    [ObservableProperty] private string _formAmountDueText = "0";
    [ObservableProperty] private string _formAmountPaidText = "0";
    [ObservableProperty] private string _formDueDate = string.Empty;
    [ObservableProperty] private string _formPaidDate = string.Empty;
    [ObservableProperty] private string _formPaymentStatus = LocationConstants.PaymentStatus.Pending;

    [ObservableProperty] private Guid _editGuaranteeId;
    [ObservableProperty] private LocationsPickItem? _formSelectedTenant;
    [ObservableProperty] private LocationsPickItem? _formSelectedContract;
    public bool HasFormSelectedTenant => FormSelectedTenant is not null;

    public ObservableCollection<LocationsPickItem> GuaranteeTenants { get; } = [];
    public ObservableCollection<LocationsPickItem> TenantContractsForGuarantee { get; } = [];
    [ObservableProperty] private string _formGuaranteeAmountText = "0";
    [ObservableProperty] private string _formGuaranteeStatus = LocationConstants.GuaranteeStatus.Active;
    [ObservableProperty] private string _formGuaranteeNotes = string.Empty;

    public bool CanManage => _session.HasPermission(PermissionCodes.LocationManage);

    public ObservableCollection<LocationsContractItem> Contracts { get; } = [];
    public ObservableCollection<LocationsPremiseItem> Premises { get; } = [];
    public ObservableCollection<LocationsPaymentItem> Payments { get; } = [];
    public ObservableCollection<LocationsGuaranteeItem> Guarantees { get; } = [];
    public ObservableCollection<string> ListTabs { get; } = ["Contrats", "Locaux", "Paiements", "Garanties"];
    public ObservableCollection<LocationsPickItem> ActiveContracts { get; } = [];

    public ObservableCollection<string> ContractStatusFilters { get; } =
        [AllStatuses, "Actif", "En attente validation", "Résilié", "Annulé", "Expiré", "Brouillon"];

    public ObservableCollection<string> PremiseStatusFilters { get; } =
        [AllStatuses, "Occupé", "Disponible"];

    public ObservableCollection<string> PaymentStatusFilters { get; } =
        [AllStatuses, "Payé", "En attente", "En retard", "Partiel"];

    public ObservableCollection<string> GuaranteeStatusFilters { get; } =
        [AllStatuses, LocationConstants.GuaranteeStatus.Active, LocationConstants.GuaranteeStatus.Refunded,
            LocationConstants.GuaranteeStatus.Partial, LocationConstants.GuaranteeStatus.Suspended];

    public ObservableCollection<string> CurrentStatusFilters { get; } = [];

    public ObservableCollection<string> ContractTypes { get; } = new(LocationConstants.ContractTypes.All);

    public ObservableCollection<string> PaymentStatuses { get; } =
    [
        LocationConstants.PaymentStatus.Pending,
        LocationConstants.PaymentStatus.Paid,
        LocationConstants.PaymentStatus.Partial,
        LocationConstants.PaymentStatus.Late,
        LocationConstants.PaymentStatus.Cancelled
    ];

    public ObservableCollection<string> GuaranteeStatuses { get; } =
    [
        LocationConstants.GuaranteeStatus.Active,
        LocationConstants.GuaranteeStatus.Refunded,
        LocationConstants.GuaranteeStatus.Partial,
        LocationConstants.GuaranteeStatus.Suspended
    ];

    public LocationsListViewModel(
        LocationsService locationsService,
        ShellNavigationService shellNavigation,
        AppConfigurationService appConfiguration,
        SessionService session)
    {
        _locationsService = locationsService;
        _shellNavigation = shellNavigation;
        _appConfiguration = appConfiguration;
        _appConfiguration.ConfigurationChanged += (_, _) => _ = LoadAsync();
        _session = session;
        RefreshStatusFilters();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            _allContracts = (await _locationsService.GetAllContractsAsync()).ToList();
            _allPremises = (await _locationsService.GetAllPremisesAsync()).ToList();
            _allPayments = (await _locationsService.GetAllPaymentsAsync()).ToList();
            _allGuarantees = (await _locationsService.GetAllGuaranteesAsync()).ToList();

            ActiveContracts.Clear();
            foreach (var c in await _locationsService.GetActiveContractsAsync())
                ActiveContracts.Add(c);

            ApplyFilter();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SetTab(object? parameter)
    {
        SelectedTab = TabNavigationHelper.ParseIndex(parameter);
        FilterStatus = AllStatuses;
        UpdateSubtitle();
        ApplyFilter();
    }

    [RelayCommand]
    private async Task CreateForTabAsync()
    {
        if (!CanManage)
        {
            StatusMessage = "Permission refusée.";
            return;
        }

        switch (SelectedTab)
        {
            case 0:
                await _shellNavigation.OpenContractFormAsync();
                break;
            case 1:
                OpenPremiseCreate();
                break;
            case 2:
                await _shellNavigation.OpenRentFormAsync();
                return;
            case 3:
                await OpenGuaranteeCreateAsync();
                break;
        }
    }

    #region Contrats

    [RelayCommand]
    private void ViewContract(LocationsContractItem? item)
    {
        if (item is null) return;
        SbmsDialogService.ShowInfo("Détails du contrat",
            $"N° {item.ContractNumber}\nLocataire : {item.TenantName}\nLocal : {item.PremiseLabel}\nType : {item.ContractType}\n" +
            $"Début : {item.StartDisplay} — Fin : {item.EndDisplay}\nLoyer : {item.RentDisplay}\nStatut : {item.StatusLabel}");
    }

    [RelayCommand]
    private async Task EditContractAsync(LocationsContractItem? item)
    {
        if (!CanManage || item is null) return;
        var contract = await _locationsService.GetContractAsync(item.Id);
        if (contract is null)
        {
            StatusMessage = "Contrat introuvable.";
            return;
        }

        IsEditMode = true;
        EditContractId = contract.Id;
        FormContractNumber = contract.ContractNumber;
        FormContractType = contract.ContractType;
        FormStartDate = contract.StartDate.ToString("dd/MM/yyyy");
        FormEndDate = contract.EndDate.ToString("dd/MM/yyyy");
        FormContractRentText = contract.MonthlyRent.ToString("F2", CultureInfo.InvariantCulture);
        FormDepositText = contract.Deposit.ToString("F2", CultureInfo.InvariantCulture);
        FormError = null;
        IsContractFormOpen = true;
    }

    [RelayCommand]
    private async Task DeleteContractAsync(LocationsContractItem? item)
    {
        if (!CanManage || item is null) return;
        if (!SbmsDialogService.Confirm("Supprimer", $"Supprimer le contrat « {item.ContractNumber} » ?"))
            return;

        IsBusy = true;
        try
        {
            var error = await _locationsService.DeleteContractAsync(item.Id);
            StatusMessage = string.IsNullOrEmpty(error) ? "Contrat supprimé." : error;
            if (string.IsNullOrEmpty(error))
                await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ValidateContractAsync(LocationsContractItem? item)
    {
        if (!CanManage || item is null) return;
        if (!SbmsDialogService.Confirm("Valider", $"Valider le contrat « {item.ContractNumber} » ?"))
            return;

        IsBusy = true;
        try
        {
            var error = await _locationsService.ValidateContractAsync(
                item.Id, _session.CurrentUser?.FullName ?? "Admin");
            StatusMessage = string.IsNullOrEmpty(error) ? "Contrat validé." : error;
            if (string.IsNullOrEmpty(error))
                await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ViewContractPdfAsync(LocationsContractItem? item)
    {
        if (item is null) return;
        IsBusy = true;
        try
        {
            var path = await _locationsService.GenerateContractPdfAsync(item.Id);
            StatusMessage = path is null ? "Contrat introuvable." : $"PDF : {path}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenTenantFromContractAsync(LocationsContractItem? item)
    {
        if (item is null || item.TenantId == Guid.Empty) return;
        await _shellNavigation.OpenTenantDetailAsync(item.TenantId);
    }

    #endregion

    #region Locaux

    [RelayCommand]
    private void ViewPremise(LocationsPremiseItem? item)
    {
        if (item is null) return;
        SbmsDialogService.ShowInfo("Détails du local",
            $"Code : {item.Code}\nNom : {item.Name}\nBâtiment : {item.Building}\nÉtage : {item.Floor}\n" +
            $"Type : {item.PremiseType}\nSurface : {item.AreaDisplay}\nLoyer : {item.RentDisplay}\n" +
            $"Statut : {item.StatusLabel}\nLocataire : {item.TenantName}");
    }

    [RelayCommand]
    private void EditPremise(LocationsPremiseItem? item)
    {
        if (!CanManage || item is null) return;
        IsEditMode = true;
        PremiseFormTitle = "Modifier le local";
        EditPremiseId = item.Id;
        FormCode = item.Code;
        FormName = item.Name;
        FormBuilding = item.Building;
        FormFloor = item.Floor == "—" ? string.Empty : item.Floor;
        FormType = item.PremiseType;
        FormRentText = item.MonthlyRent.ToString("F2", CultureInfo.InvariantCulture);
        FormError = null;
        IsPremiseFormOpen = true;
    }

    [RelayCommand]
    private async Task DeletePremiseAsync(LocationsPremiseItem? item)
    {
        if (!CanManage || item is null) return;
        if (!SbmsDialogService.Confirm("Supprimer", $"Supprimer le local « {item.Code} — {item.Name} » ?"))
            return;

        IsBusy = true;
        try
        {
            var error = await _locationsService.DeletePremiseAsync(item.Id);
            StatusMessage = string.IsNullOrEmpty(error) ? "Local supprimé." : error;
            if (string.IsNullOrEmpty(error))
                await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenPremiseCreate()
    {
        IsEditMode = false;
        PremiseFormTitle = "Nouveau local";
        EditPremiseId = Guid.Empty;
        FormError = null;
        _ = InitPremiseCreateAsync();
    }

    private async Task InitPremiseCreateAsync()
    {
        FormCode = await _locationsService.GenerateNextCodeAsync();
        FormName = string.Empty;
        FormBuilding = string.Empty;
        FormFloor = string.Empty;
        FormType = LocationConstants.DefaultPremiseType;
        FormRentText = "0";
        IsPremiseFormOpen = true;
    }

    #endregion

    #region Paiements

    [RelayCommand]
    private void ViewPayment(LocationsPaymentItem? item)
    {
        if (item is null) return;
        SbmsDialogService.ShowInfo("Détails du paiement",
            $"Locataire : {item.TenantName}\nLocal : {item.PremiseLabel}\nPériode : {item.PeriodDisplay}\n" +
            $"Dû : {item.AmountDisplay} — Payé : {item.AmountPaidDisplay}\nÉchéance : {item.DueDisplay}\n" +
            $"Date paiement : {item.PaidDisplay}\nRetard : {item.LateLabel}\nStatut : {item.StatusLabel}");
    }

    [RelayCommand]
    private async Task EditPaymentAsync(LocationsPaymentItem? item)
    {
        if (!CanManage || item is null) return;
        var payment = await _locationsService.GetRentPaymentAsync(item.Id);
        if (payment is null)
        {
            StatusMessage = "Paiement introuvable.";
            return;
        }

        IsEditMode = true;
        EditPaymentId = payment.Id;
        FormAmountDueText = payment.AmountDue.ToString("F2", CultureInfo.InvariantCulture);
        FormAmountPaidText = payment.AmountPaid.ToString("F2", CultureInfo.InvariantCulture);
        FormDueDate = payment.DueDate.ToString("dd/MM/yyyy");
        FormPaidDate = payment.PaidDate?.ToString("dd/MM/yyyy") ?? string.Empty;
        FormPaymentStatus = payment.PaymentStatus;
        FormError = null;
        IsPaymentFormOpen = true;
    }

    [RelayCommand]
    private async Task DeletePaymentAsync(LocationsPaymentItem? item)
    {
        if (!CanManage || item is null) return;
        if (!SbmsDialogService.Confirm("Supprimer", $"Supprimer le paiement {item.PeriodDisplay} — {item.TenantName} ?"))
            return;

        IsBusy = true;
        try
        {
            var error = await _locationsService.DeleteRentPaymentAsync(item.Id);
            StatusMessage = string.IsNullOrEmpty(error) ? "Paiement supprimé." : error;
            if (string.IsNullOrEmpty(error))
                await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GenerateReceiptAsync(LocationsPaymentItem? item)
    {
        if (item is null) return;
        if (!SbmsDialogService.Confirm("Quittance", "Générer la quittance PDF de ce paiement ?"))
            return;

        IsBusy = true;
        try
        {
            var path = await _locationsService.GenerateRentReceiptPdfAsync(item.Id);
            StatusMessage = path is null ? "Paiement introuvable ou montant nul." : $"Quittance : {path}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region Garanties

    [RelayCommand]
    private void ViewGuarantee(LocationsGuaranteeItem? item)
    {
        if (item is null) return;
        SbmsDialogService.ShowInfo("Détails de la garantie",
            $"Contrat : {item.ContractNumber}\nLocataire : {item.TenantName}\nType : {item.TypeLabel}\n" +
            $"Montant : {item.AmountDisplay}\nRemboursé : {item.RefundedDisplay}\nDate : {item.DateDisplay}\nStatut : {item.Status}");
    }

    [RelayCommand]
    private async Task EditGuaranteeAsync(LocationsGuaranteeItem? item)
    {
        if (!CanManage || item is null) return;
        var guarantee = await _locationsService.GetGuaranteeAsync(item.Id);
        if (guarantee is null)
        {
            StatusMessage = "Garantie introuvable.";
            return;
        }

        GuaranteeTenants.Clear();
        var tenantIds = ActiveContracts.Select(c => c.TenantId).Distinct().ToHashSet();
        foreach (var t in (await _locationsService.GetTenantsAsync()).Where(t => tenantIds.Contains(t.Id)).OrderBy(t => t.Name))
            GuaranteeTenants.Add(t);

        IsEditMode = true;
        EditGuaranteeId = guarantee.Id;
        FormGuaranteeAmountText = guarantee.Amount.ToString("F2", CultureInfo.InvariantCulture);
        FormGuaranteeStatus = guarantee.Status;
        FormGuaranteeNotes = guarantee.Notes ?? string.Empty;
        var contractPick = ActiveContracts.FirstOrDefault(c => c.Id == guarantee.LeaseContractId);
        FormSelectedContract = contractPick;
        FormSelectedTenant = contractPick is null
            ? null
            : GuaranteeTenants.FirstOrDefault(t => t.Id == contractPick.TenantId)
              ?? new LocationsPickItem { Id = contractPick.TenantId, TenantId = contractPick.TenantId, Label = contractPick.Label };
        if (FormSelectedTenant is not null)
        {
            TenantContractsForGuarantee.Clear();
            foreach (var c in ActiveContracts.Where(c => c.TenantId == FormSelectedTenant.Id))
                TenantContractsForGuarantee.Add(c);
        }

        FormError = null;
        IsGuaranteeFormOpen = true;
    }

    [RelayCommand]
    private async Task DeleteGuaranteeAsync(LocationsGuaranteeItem? item)
    {
        if (!CanManage || item is null) return;
        if (!SbmsDialogService.Confirm("Supprimer", $"Supprimer la garantie de {item.TenantName} ?"))
            return;

        IsBusy = true;
        try
        {
            var error = await _locationsService.DeleteGuaranteeAsync(item.Id);
            StatusMessage = string.IsNullOrEmpty(error) ? "Garantie supprimée." : error;
            if (string.IsNullOrEmpty(error))
                await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefundGuaranteeAsync(LocationsGuaranteeItem? item)
    {
        if (!CanManage || item is null) return;
        if (!SbmsDialogService.Confirm("Rembourser",
                "Confirmer le remboursement de cette garantie ?\n\nLe contrat sera clôturé, le local libéré et une décharge PDF sera générée automatiquement."))
            return;

        IsBusy = true;
        try
        {
            var guarantee = await _locationsService.GetGuaranteeAsync(item.Id);
            if (guarantee is null)
            {
                StatusMessage = "Garantie introuvable.";
                return;
            }

            var remaining = guarantee.Amount - guarantee.AmountRefunded;
            var error = await _locationsService.RefundGuaranteeAsync(item.Id, remaining);
            if (!string.IsNullOrEmpty(error))
            {
                StatusMessage = error;
                return;
            }

            var dischargePath = await _locationsService.GetGuaranteeDischargePdfPathAsync(item.Id);
            if (!string.IsNullOrWhiteSpace(dischargePath) && File.Exists(dischargePath))
            {
                Process.Start(new ProcessStartInfo(dischargePath) { UseShellExecute = true });
                StatusMessage = "Garantie remboursée — décharge générée.";
            }
            else
                StatusMessage = "Garantie remboursée.";

            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OpenGuaranteeCreateAsync()
    {
        IsEditMode = false;
        EditGuaranteeId = Guid.Empty;
        FormGuaranteeAmountText = "0";
        FormGuaranteeStatus = LocationConstants.GuaranteeStatus.Active;
        FormGuaranteeNotes = string.Empty;
        FormSelectedTenant = null;
        FormSelectedContract = null;
        TenantContractsForGuarantee.Clear();
        FormError = null;

        GuaranteeTenants.Clear();
        var tenantIds = ActiveContracts.Select(c => c.TenantId).Distinct().ToHashSet();
        foreach (var t in (await _locationsService.GetTenantsAsync()).Where(t => tenantIds.Contains(t.Id)).OrderBy(t => t.Name))
            GuaranteeTenants.Add(t);

        IsGuaranteeFormOpen = true;
    }

    partial void OnFormSelectedTenantChanged(LocationsPickItem? value)
    {
        TenantContractsForGuarantee.Clear();
        FormSelectedContract = null;
        OnPropertyChanged(nameof(HasFormSelectedTenant));
        if (value is null)
            return;

        foreach (var c in ActiveContracts.Where(c => c.TenantId == value.Id))
            TenantContractsForGuarantee.Add(c);

        if (TenantContractsForGuarantee.Count == 1)
            FormSelectedContract = TenantContractsForGuarantee[0];
    }

    #endregion

    #region Modales — enregistrement

    [RelayCommand]
    private void ClosePremiseForm() => IsPremiseFormOpen = false;

    [RelayCommand]
    private void CloseContractForm() => IsContractFormOpen = false;

    [RelayCommand]
    private void ClosePaymentForm() => IsPaymentFormOpen = false;

    [RelayCommand]
    private void CloseGuaranteeForm() => IsGuaranteeFormOpen = false;

    [RelayCommand]
    private async Task SavePremiseFormAsync()
    {
        if (!CanManage) { FormError = "Permission refusée."; return; }
        if (!SbmsDialogService.Confirm("Confirmer", IsEditMode ? "Enregistrer les modifications ?" : "Créer ce local ?"))
            return;

        FormError = null;
        if (!decimal.TryParse(FormRentText.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var rent))
            rent = 0;

        IsBusy = true;
        try
        {
            if (IsEditMode)
            {
                var premise = await _locationsService.GetPremiseAsync(EditPremiseId);
                if (premise is null) { FormError = "Local introuvable."; return; }
                premise.Name = FormName;
                premise.Building = FormBuilding;
                premise.Floor = FormFloor;
                premise.PremiseType = FormType;
                premise.MonthlyRent = rent;
                var error = await _locationsService.UpdatePremiseAsync(premise);
                if (!string.IsNullOrEmpty(error)) { FormError = error; return; }
            }
            else
            {
                var error = await _locationsService.CreatePremiseAsync(new Premise
                {
                    Code = FormCode,
                    Name = FormName,
                    Building = FormBuilding,
                    Floor = FormFloor,
                    PremiseType = FormType,
                    MonthlyRent = rent,
                    IsOccupied = false
                });
                if (!string.IsNullOrEmpty(error)) { FormError = error; return; }
            }

            IsPremiseFormOpen = false;
            StatusMessage = IsEditMode ? "Local mis à jour." : "Local créé.";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveContractFormAsync()
    {
        if (!CanManage) { FormError = "Permission refusée."; return; }
        if (!SbmsDialogService.Confirm("Confirmer", "Enregistrer les modifications du contrat ?"))
            return;

        FormError = null;
        if (!TryParseDate(FormStartDate, out var start)) { FormError = "Date de début invalide (jj/mm/aaaa)."; return; }
        if (!TryParseDate(FormEndDate, out var end)) { FormError = "Date de fin invalide (jj/mm/aaaa)."; return; }
        if (!decimal.TryParse(FormContractRentText.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var rent))
            rent = 0;
        if (!decimal.TryParse(FormDepositText.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var deposit))
            deposit = 0;

        IsBusy = true;
        try
        {
            var contract = await _locationsService.GetContractAsync(EditContractId);
            if (contract is null) { FormError = "Contrat introuvable."; return; }

            contract.StartDate = start;
            contract.EndDate = end;
            contract.MonthlyRent = rent;
            contract.Deposit = deposit;
            contract.ContractType = FormContractType;

            var error = await _locationsService.UpdateContractAsync(contract);
            if (!string.IsNullOrEmpty(error)) { FormError = error; return; }

            IsContractFormOpen = false;
            StatusMessage = "Contrat mis à jour.";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SavePaymentFormAsync()
    {
        if (!CanManage) { FormError = "Permission refusée."; return; }
        if (!SbmsDialogService.Confirm("Confirmer", "Enregistrer les modifications du paiement ?"))
            return;

        FormError = null;
        if (!decimal.TryParse(FormAmountDueText.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var due))
            due = 0;
        if (!decimal.TryParse(FormAmountPaidText.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var paid))
            paid = 0;
        if (!TryParseDate(FormDueDate, out var dueDate)) { FormError = "Échéance invalide."; return; }
        DateTime? paidDate = null;
        if (!string.IsNullOrWhiteSpace(FormPaidDate))
        {
            if (!TryParseDate(FormPaidDate, out var pd)) { FormError = "Date de paiement invalide."; return; }
            paidDate = pd;
        }

        IsBusy = true;
        try
        {
            var payment = await _locationsService.GetRentPaymentAsync(EditPaymentId);
            if (payment is null) { FormError = "Paiement introuvable."; return; }

            payment.AmountDue = due;
            payment.AmountPaid = paid;
            payment.DueDate = dueDate;
            payment.PaidDate = paidDate;
            payment.PaymentStatus = FormPaymentStatus;

            var error = await _locationsService.UpdateRentPaymentAsync(payment);
            if (!string.IsNullOrEmpty(error)) { FormError = error; return; }

            IsPaymentFormOpen = false;
            StatusMessage = "Paiement mis à jour.";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveGuaranteeFormAsync()
    {
        if (!CanManage) { FormError = "Permission refusée."; return; }
        if (!SbmsDialogService.Confirm("Confirmer", IsEditMode ? "Enregistrer les modifications ?" : "Créer cette garantie ?"))
            return;

        if (FormSelectedTenant is null)
        {
            FormError = "Sélectionnez un locataire.";
            return;
        }

        if (FormSelectedContract is null)
        {
            FormError = "Sélectionnez un contrat.";
            return;
        }

        if (!decimal.TryParse(FormGuaranteeAmountText.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
            amount = 0;

        IsBusy = true;
        try
        {
            if (IsEditMode)
            {
                var guarantee = await _locationsService.GetGuaranteeAsync(EditGuaranteeId);
                if (guarantee is null) { FormError = "Garantie introuvable."; return; }
                guarantee.Amount = amount;
                guarantee.Status = FormGuaranteeStatus;
                guarantee.Notes = FormGuaranteeNotes;
                var error = await _locationsService.UpdateGuaranteeAsync(guarantee);
                if (!string.IsNullOrEmpty(error)) { FormError = error; return; }
            }
            else
            {
                var error = await _locationsService.CreateGuaranteeAsync(new LeaseGuarantee
                {
                    LeaseContractId = FormSelectedContract.Id,
                    Amount = amount,
                    Status = FormGuaranteeStatus,
                    Notes = string.IsNullOrWhiteSpace(FormGuaranteeNotes) ? null : FormGuaranteeNotes.Trim()
                });
                if (!string.IsNullOrEmpty(error)) { FormError = error; return; }
            }

            IsGuaranteeFormOpen = false;
            StatusMessage = IsEditMode ? "Garantie mise à jour." : "Garantie créée.";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    partial void OnSearchQueryChanged(string value) => ApplyFilter();
    partial void OnFilterStatusChanged(string value) => ApplyFilter();

    partial void OnSelectedTabChanged(int value)
    {
        UpdateSubtitle();
        ApplyFilter();
    }

    private void UpdateSubtitle()
    {
        PageSubtitle = SelectedTab switch
        {
            1 => "Liste des locaux et espaces",
            2 => "Suivi des paiements de loyer",
            3 => "Cautions et garanties locatives",
            _ => "Contrats de location"
        };
        RefreshStatusFilters();
    }

    private void RefreshStatusFilters()
    {
        CurrentStatusFilters.Clear();
        IEnumerable<string> source = SelectedTab switch
        {
            1 => PremiseStatusFilters,
            2 => PaymentStatusFilters,
            3 => GuaranteeStatusFilters,
            _ => ContractStatusFilters
        };
        foreach (var item in source)
            CurrentStatusFilters.Add(item);
        FilterStatus = PageFilterHelper.RestoreSelection(FilterStatus, CurrentStatusFilters, AllStatuses);
    }

    private void ApplyFilter()
    {
        var q = SearchQuery.Trim();
        switch (SelectedTab)
        {
            case 1:
                ApplyPremiseFilter(q);
                break;
            case 2:
                ApplyPaymentFilter(q);
                break;
            case 3:
                ApplyGuaranteeFilter(q);
                break;
            default:
                ApplyContractFilter(q);
                break;
        }
    }

    private void ApplyContractFilter(string q)
    {
        var filtered = _allContracts.AsEnumerable();
        if (!PageFilterHelper.IsAll(FilterStatus, AllStatuses))
            filtered = filtered.Where(c => c.StatusLabel.Equals(FilterStatus, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(q))
        {
            filtered = filtered.Where(c =>
                c.ContractNumber.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.TenantName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.PremiseLabel.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.ContractType.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.StatusLabel.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        Contracts.Clear();
        foreach (var row in filtered)
            Contracts.Add(row);
    }

    private void ApplyPremiseFilter(string q)
    {
        var filtered = _allPremises.AsEnumerable();
        if (!PageFilterHelper.IsAll(FilterStatus, AllStatuses))
            filtered = filtered.Where(p => p.StatusLabel.Equals(FilterStatus, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(q))
        {
            filtered = filtered.Where(p =>
                p.Code.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.Building.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.PremiseType.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.TenantName.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        Premises.Clear();
        foreach (var row in filtered)
            Premises.Add(row);
    }

    private void ApplyPaymentFilter(string q)
    {
        var filtered = _allPayments.AsEnumerable();
        if (!PageFilterHelper.IsAll(FilterStatus, AllStatuses))
        {
            filtered = FilterStatus switch
            {
                "Partiel" => filtered.Where(p =>
                    p.PaymentStatus.Equals(LocationConstants.PaymentStatus.Partial, StringComparison.OrdinalIgnoreCase)),
                _ => filtered.Where(p => p.StatusLabel.Equals(FilterStatus, StringComparison.OrdinalIgnoreCase))
            };
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            filtered = filtered.Where(p =>
                p.TenantName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.PremiseLabel.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.PeriodDisplay.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.PaymentStatus.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        Payments.Clear();
        foreach (var row in filtered)
            Payments.Add(row);
    }

    private void ApplyGuaranteeFilter(string q)
    {
        var filtered = _allGuarantees.AsEnumerable();
        if (!PageFilterHelper.IsAll(FilterStatus, AllStatuses))
            filtered = filtered.Where(g => g.Status.Equals(FilterStatus, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(q))
        {
            filtered = filtered.Where(g =>
                g.ContractNumber.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                g.TenantName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                g.Status.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        Guarantees.Clear();
        foreach (var row in filtered)
            Guarantees.Add(row);
    }

    private static bool TryParseDate(string text, out DateTime date)
    {
        if (DateTime.TryParseExact(text.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;
        return DateTime.TryParse(text, CultureInfo.GetCultureInfo("fr-FR"), DateTimeStyles.None, out date);
    }
}
