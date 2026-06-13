using CommunityToolkit.Mvvm.ComponentModel;
using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Shared.Constants;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class LocationsPatrimoineViewModel
{
    [ObservableProperty] private string _companyName = string.Empty;
    [ObservableProperty] private string _ownerType = "Particulier";
    [ObservableProperty] private string _legalRepresentative = string.Empty;
    [ObservableProperty] private string _buildingAddress = string.Empty;
    [ObservableProperty] private string _buildingCity = string.Empty;
    [ObservableProperty] private string _buildingCountry = string.Empty;
    [ObservableProperty] private string _buildingPhone = string.Empty;
    [ObservableProperty] private string _buildingSecondaryPhone = string.Empty;
    [ObservableProperty] private string _buildingEmail = string.Empty;
    [ObservableProperty] private string _buildingWebsite = string.Empty;
    [ObservableProperty] private string _buildingNationalId = string.Empty;
    [ObservableProperty] private string _taxId = string.Empty;
    [ObservableProperty] private string _bankName = string.Empty;
    [ObservableProperty] private string _bankAccount = string.Empty;

    [ObservableProperty] private string _buildingDisplayName = string.Empty;
    [ObservableProperty] private string _buildingType = string.Empty;
    [ObservableProperty] private string _yearBuiltText = string.Empty;
    [ObservableProperty] private decimal _buildingAreaSqMValue;
    [ObservableProperty] private int _parkingSpaces;
    [ObservableProperty] private bool _hasElevator;
    [ObservableProperty] private string _equipmentText = string.Empty;
    [ObservableProperty] private string _managementRulesText = string.Empty;
    [ObservableProperty] private int _buildingFloors;
    [ObservableProperty] private int _apartmentCount;
    [ObservableProperty] private int _commercialUnitCount;
    [ObservableProperty] private int _totalPremisesConfig;

    /// <summary>Immeuble unique — non modifiable.</summary>
    public string SingleBuildingDisplayName => BrandConstants.AppName;

    private void ApplyProfileFromData(SettingsPageData data)
    {
        CompanyName = data.CompanyName;
        OwnerType = data.OwnerType;
        LegalRepresentative = data.LegalRepresentative ?? string.Empty;
        BuildingAddress = data.BuildingAddress;
        BuildingCity = data.BuildingCity;
        BuildingCountry = data.BuildingCountry;
        BuildingPhone = data.BuildingPhone;
        BuildingSecondaryPhone = data.SecondaryPhone ?? string.Empty;
        BuildingEmail = data.BuildingEmail;
        BuildingWebsite = data.BuildingWebsite;
        BuildingNationalId = data.BuildingNationalId;
        TaxId = data.TaxId ?? string.Empty;
        BankName = data.BankName ?? string.Empty;
        BankAccount = data.BankAccount ?? string.Empty;
        BuildingDisplayName = BrandConstants.AppName;
        BuildingType = string.IsNullOrWhiteSpace(data.BuildingType) ? "Immeuble" : data.BuildingType;
        YearBuiltText = data.YearBuilt?.ToString() ?? string.Empty;
        BuildingAreaSqMValue = data.BuildingAreaSqM;
        ParkingSpaces = data.ParkingSpaces;
        HasElevator = data.HasElevator;
        EquipmentText = data.EquipmentAndInstallations;
        ManagementRulesText = data.ManagementRules;
        BuildingFloors = data.BuildingFloors;
        ApartmentCount = data.ApartmentCount;
        CommercialUnitCount = data.CommercialUnitCount;
        TotalPremisesConfig = data.TotalPremises;
    }

    private BuildingProfileInput BuildProfileInput()
    {
        int? yearBuilt = int.TryParse(YearBuiltText, out var y) && y > 1800 && y <= DateTime.Now.Year + 2
            ? y
            : null;

        return new BuildingProfileInput
        {
            CompanyName = CompanyName,
            OwnerType = OwnerType,
            LegalRepresentative = string.IsNullOrWhiteSpace(LegalRepresentative) ? null : LegalRepresentative,
            Address = BuildingAddress,
            City = BuildingCity,
            Country = BuildingCountry,
            Phone = BuildingPhone,
            SecondaryPhone = string.IsNullOrWhiteSpace(BuildingSecondaryPhone) ? null : BuildingSecondaryPhone,
            Email = BuildingEmail,
            Website = BuildingWebsite,
            NationalId = BuildingNationalId,
            TaxId = string.IsNullOrWhiteSpace(TaxId) ? null : TaxId,
            BankName = string.IsNullOrWhiteSpace(BankName) ? null : BankName,
            BankAccount = string.IsNullOrWhiteSpace(BankAccount) ? null : BankAccount,
            BuildingDisplayName = BrandConstants.AppName,
            BuildingType = BuildingType,
            TotalFloors = BuildingFloors,
            TotalPremises = TotalPremisesConfig,
            ApartmentCount = ApartmentCount,
            CommercialUnitCount = CommercialUnitCount,
            TotalAreaSqM = BuildingAreaSqMValue,
            ParkingSpaces = ParkingSpaces,
            HasElevator = HasElevator,
            YearBuilt = yearBuilt,
            EquipmentAndInstallations = EquipmentText,
            ManagementRules = ManagementRulesText
        };
    }
}
