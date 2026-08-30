namespace SmartBuilding.Desktop.WPF.Models;

public sealed class SettingsPageData
{
    public string CompanyName { get; init; } = string.Empty;
    public string TimeZoneId { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public decimal UsdExchangeRate { get; init; }
    public string DateFormat { get; init; } = string.Empty;
    public string Language { get; init; } = string.Empty;
    public string TimeFormat { get; init; } = string.Empty;
    public bool MaintenanceMode { get; init; }
    public string? LogoPath { get; init; }
    public int ActiveUsers { get; init; }
    public int TotalUsers { get; init; }
    public int DistinctRoles { get; init; }
    public int SyncLogCount { get; init; }
    public DateTime? LastSyncAt { get; init; }
    public string SyncStatusLabel { get; init; } = "—";
    public long DatabaseSizeBytes { get; init; }
    public string DatabaseFilePath { get; init; } = string.Empty;
    public string DatabaseDeploymentLabel { get; init; } = string.Empty;
    public string DatabaseDataDirectory { get; init; } = string.Empty;
    public string? DatabaseDataDirectoryPath { get; init; }
    public bool CanOpenDatabaseDataDirectory { get; init; }
    public string AppVersion { get; init; } = "v1.0.0";
    public string EnvironmentName { get; init; } = "Développement";
    public bool NotifyEmail { get; init; } = true;
    public bool NotifyPush { get; init; } = true;
    public bool NotifyCritical { get; init; } = true;
    public bool NotifyDailyReports { get; init; }
    public int ActiveSessions { get; init; } = 1;
    public int AuthorizedDevices { get; init; } = 1;
    public bool TwoFactorEnabled { get; init; }
    public string BuildingAddress { get; init; } = string.Empty;
    public string BuildingCity { get; init; } = string.Empty;
    public string BuildingCountry { get; init; } = string.Empty;
    public string BuildingPhone { get; init; } = string.Empty;
    public string BuildingEmail { get; init; } = string.Empty;
    public string BuildingWebsite { get; init; } = string.Empty;
    public string BuildingNationalId { get; init; } = string.Empty;
    public int BuildingFloors { get; init; }
    public int PremisesCount { get; init; }
    public decimal BuildingAreaSqM { get; init; }
    public int EmailsCount { get; init; }
    public int EmailAccountsCount { get; init; }
    public int DocumentsCount { get; init; }
    public string ApiBaseUrl { get; init; } = string.Empty;
    public string AccentColorHex { get; init; } = "#2D6A4F";
    public string SidebarColorHex { get; init; } = "#1B3D3B";
    public string SecondaryColorHex { get; init; } = "#0D9488";
    public string ThemeMode { get; init; } = "Light";
    public bool CompactTables { get; init; }
    public bool ShowKpiSparklines { get; init; } = true;

    public string OwnerType { get; init; } = "Particulier";
    public string? LegalRepresentative { get; init; }
    public string? SecondaryPhone { get; init; }
    public string? TaxId { get; init; }
    public string? BankName { get; init; }
    public string? BankAccount { get; init; }
    public string BuildingDisplayName { get; init; } = string.Empty;
    public string BuildingType { get; init; } = string.Empty;
    public int ApartmentCount { get; init; }
    public int CommercialUnitCount { get; init; }
    public int TotalPremises { get; init; }
    public int ParkingSpaces { get; init; }
    public bool HasElevator { get; init; }
    public int? YearBuilt { get; init; }
    public string EquipmentAndInstallations { get; init; } = string.Empty;
    public string ManagementRules { get; init; } = string.Empty;
}

public sealed class BuildingProfileInput
{
    public string CompanyName { get; init; } = string.Empty;
    public string OwnerType { get; init; } = "Particulier";
    public string? LegalRepresentative { get; init; }
    public string Address { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string? SecondaryPhone { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Website { get; init; } = string.Empty;
    public string NationalId { get; init; } = string.Empty;
    public string? TaxId { get; init; }
    public string? BankName { get; init; }
    public string? BankAccount { get; init; }
    public string BuildingDisplayName { get; init; } = string.Empty;
    public string BuildingType { get; init; } = string.Empty;
    public int TotalFloors { get; init; }
    public int TotalPremises { get; init; }
    public int ApartmentCount { get; init; }
    public int CommercialUnitCount { get; init; }
    public decimal TotalAreaSqM { get; init; }
    public int ParkingSpaces { get; init; }
    public bool HasElevator { get; init; }
    public int? YearBuilt { get; init; }
    public string EquipmentAndInstallations { get; init; } = string.Empty;
    public string ManagementRules { get; init; } = string.Empty;
}

public sealed class SettingsCategoryItem
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string IconKind { get; init; } = "Cog";
}

public sealed class SettingsQuickAccessItem
{
    public string CategoryId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string IconKind { get; init; } = "Cog";
    public string IconColor { get; init; } = "#2D6A4F";
    public string IconBg { get; init; } = "#D1FAE5";
}

public sealed class SettingsBackupItem
{
    public DateTime StartedAt { get; init; }
    public string DateDisplay { get; init; } = string.Empty;
    public string StatusDisplay { get; init; } = string.Empty;
    public string DetailsDisplay { get; init; } = string.Empty;
    public bool Success { get; init; }
}

public sealed class SettingsLogPreviewItem
{
    public string TimeDisplay { get; init; } = string.Empty;
    public string Level { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed class SettingsIntegrationItem
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public bool IsConnected { get; init; }
}
