using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Shared.Constants;

namespace SmartBuilding.Desktop.WPF.Models;

/// <summary>Configuration globale unique (société + apparence) — source de vérité après chargement.</summary>
public sealed class AppConfiguration
{
    public string CompanyName { get; init; } = BuildingInfoDefaults.CompanyName;
    public string AppTitle { get; init; } = BrandConstants.AppName;
    public string AppSubtitle { get; init; } = BrandConstants.AppSubtitle;
    public string? LogoPath { get; init; }

    public string Address { get; init; } = BuildingInfoDefaults.Address;
    public string City { get; init; } = BuildingInfoDefaults.City;
    public string Country { get; init; } = BuildingInfoDefaults.Country;
    public string Phone { get; init; } = BuildingInfoDefaults.Phone;
    public string Email { get; init; } = BuildingInfoDefaults.Email;
    public string Website { get; init; } = BuildingInfoDefaults.Website;
    public string NationalId { get; init; } = BuildingInfoDefaults.NationalId;

    public string TimeZoneId { get; init; } = "Africa/Kinshasa";
    public string Currency { get; init; } = "USD";
    /// <summary>Taux : 1 USD = X CDF.</summary>
    public decimal UsdExchangeRate { get; init; } = 2850m;
    public string DateFormat { get; init; } = "dd/MM/yyyy";
    public string Language { get; init; } = "Français";
    public string TimeFormat { get; init; } = "24 heures";
    public bool MaintenanceMode { get; init; }

    public AppThemeMode ThemeMode { get; init; } = AppThemeMode.Light;
    public string PrimaryColorHex { get; init; } = "#2D6A4F";
    public string SidebarColorHex { get; init; } = "#1B3D3B";
    public string SecondaryColorHex { get; init; } = "#0D9488";
    public bool CompactTables { get; init; }
    public bool ShowKpiSparklines { get; init; } = true;

    public string PdfAccentHex => PrimaryColorHex;
    public string PdfHeaderHex => ThemeMode == AppThemeMode.Dark ? "#E2E8F0" : "#1B365D";

    public string FullAddress =>
        string.Join(", ", new[] { Address, City, Country }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public string FormatMoney(decimal amount) =>
        SmartBuilding.Shared.Money.BuildingMoneyFormat.Format(amount, Currency);

    public (decimal DisplayValue, string Suffix) ToDisplayAmount(decimal amount) =>
        SmartBuilding.Shared.Money.BuildingMoneyFormat.ToDisplay(amount, Currency);

    public static AppConfiguration Default { get; } = new();

    public AppConfiguration WithAppearance(
        AppThemeMode themeMode,
        string primaryHex,
        string? sidebarHex,
        string? secondaryHex,
        bool compactTables,
        bool showKpiSparklines) =>
        new()
        {
            CompanyName = CompanyName,
            AppTitle = AppTitle,
            AppSubtitle = AppSubtitle,
            LogoPath = LogoPath,
            Address = Address,
            City = City,
            Country = Country,
            Phone = Phone,
            Email = Email,
            Website = Website,
            NationalId = NationalId,
            TimeZoneId = TimeZoneId,
            Currency = Currency,
            UsdExchangeRate = UsdExchangeRate,
            DateFormat = DateFormat,
            Language = Language,
            TimeFormat = TimeFormat,
            MaintenanceMode = MaintenanceMode,
            ThemeMode = themeMode,
            PrimaryColorHex = primaryHex,
            SidebarColorHex = sidebarHex ?? SidebarColorHex,
            SecondaryColorHex = secondaryHex ?? SecondaryColorHex,
            CompactTables = compactTables,
            ShowKpiSparklines = showKpiSparklines
        };
}
