using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartBuilding.Desktop.WPF.Helpers;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class TenantDetailViewModel : BaseViewModel
{
    private readonly TenantDetailService _tenantDetailService;
    private readonly ShellNavigationService _shellNavigation;
    private Guid _tenantId;

    [ObservableProperty] private string _pageTitle = "Fiche locataire";
    [ObservableProperty] private string _breadcrumb = "Locations / Locataire";
    [ObservableProperty] private int _selectedTab;
    [ObservableProperty] private bool _hasData;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _initials = "L";
    [ObservableProperty] private string _summaryLine = string.Empty;
    [ObservableProperty] private string _email = "—";
    [ObservableProperty] private string _phone = "—";
    [ObservableProperty] private string _company = "—";
    [ObservableProperty] private string _address = "—";
    [ObservableProperty] private string _dossierNumber = "—";
    [ObservableProperty] private string _rentalStatus = "—";
    [ObservableProperty] private string _nationality = "—";
    [ObservableProperty] private string _businessActivity = "—";
    [ObservableProperty] private string _personCountDisplay = "—";
    [ObservableProperty] private string _category = "—";
    [ObservableProperty] private string _nationalId = "—";
    [ObservableProperty] private string _dateOfBirthDisplay = "—";
    [ObservableProperty] private string _ageDisplay = "—";
    [ObservableProperty] private string _gender = "—";
    [ObservableProperty] private string _maritalStatus = "—";
    [ObservableProperty] private string _spouseName = "—";
    [ObservableProperty] private string _childrenDisplay = "—";
    [ObservableProperty] private int _childrenCount;
    [ObservableProperty] private string _profession = "—";
    [ObservableProperty] private string _emergencyContactName = "—";
    [ObservableProperty] private string _emergencyContactPhone = "—";
    [ObservableProperty] private string _notes = "—";
    [ObservableProperty] private string _totalRentDisplay = "—";
    [ObservableProperty] private int _activeContracts;
    [ObservableProperty] private int _latePaymentsCount;

    public bool HasLatePayments => LatePaymentsCount > 0;

    partial void OnLatePaymentsCountChanged(int value) => OnPropertyChanged(nameof(HasLatePayments));

    public ObservableCollection<TenantContractRow> Contracts { get; } = [];
    public ObservableCollection<TenantPaymentRow> Payments { get; } = [];
    public ObservableCollection<TenantActivityRow> Activities { get; } = [];
    public ObservableCollection<TenantGuaranteeRow> Guarantees { get; } = [];

    public TenantDetailViewModel(TenantDetailService tenantDetailService, ShellNavigationService shellNavigation)
    {
        _tenantDetailService = tenantDetailService;
        _shellNavigation = shellNavigation;
    }

    public void Initialize(Guid tenantId)
    {
        _tenantId = tenantId;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (_tenantId == Guid.Empty)
            return;

        IsBusy = true;
        try
        {
            var data = await _tenantDetailService.GetAsync(_tenantId);
            if (data is null)
            {
                HasData = false;
                ErrorMessage = "Locataire introuvable.";
                return;
            }

            HasData = true;
            ErrorMessage = null;
            PageTitle = data.Name;
            Breadcrumb = $"Locations / {data.Name}";
            Name = data.Name;
            Initials = data.Initials;
            SummaryLine = data.SummaryLine;
            Email = data.Email;
            Phone = data.Phone;
            Company = data.Company;
            Address = data.Address;
            DossierNumber = data.DossierNumber;
            RentalStatus = data.RentalStatus;
            Nationality = data.Nationality;
            BusinessActivity = data.BusinessActivity;
            PersonCountDisplay = data.PersonCountDisplay;
            Category = data.Category;
            NationalId = data.NationalId;
            DateOfBirthDisplay = data.DateOfBirthDisplay;
            AgeDisplay = data.AgeDisplay;
            Gender = data.Gender;
            MaritalStatus = data.MaritalStatus;
            SpouseName = data.SpouseName;
            ChildrenCount = data.ChildrenCount;
            ChildrenDisplay = data.ChildrenDisplay;
            Profession = data.Profession;
            EmergencyContactName = data.EmergencyContactName;
            EmergencyContactPhone = data.EmergencyContactPhone;
            Notes = data.Notes;
            TotalRentDisplay = data.TotalRentDisplay;
            ActiveContracts = data.ActiveContracts;
            LatePaymentsCount = data.LatePaymentsCount;

            Contracts.Clear();
            foreach (var c in data.Contracts) Contracts.Add(c);

            Payments.Clear();
            foreach (var p in data.Payments) Payments.Add(p);

            Activities.Clear();
            foreach (var a in data.Activities) Activities.Add(a);

            Guarantees.Clear();
            foreach (var g in data.Guarantees) Guarantees.Add(g);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SetTab(object? parameter) => SelectedTab = TabNavigationHelper.ParseIndex(parameter);

    [RelayCommand]
    private async Task GoBackAsync() => await _shellNavigation.BackToLocationsAsync();
}
