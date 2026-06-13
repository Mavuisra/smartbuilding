using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Services;

namespace SmartBuilding.Desktop.WPF.Services;

public class SettingsService
{
    private readonly SmartBuildingDbContext _db;
    private readonly ISyncService _syncService;
    private readonly IConfiguration _configuration;
    private readonly AppConfigurationService _appConfiguration;
    private readonly DesktopLocalDatabaseConfig _localDb;
    private readonly string _prefsPath;

    public SettingsService(
        SmartBuildingDbContext db,
        ISyncService syncService,
        IConfiguration configuration,
        AppConfigurationService appConfiguration,
        DesktopLocalDatabaseConfig localDb)
    {
        _db = db;
        _syncService = syncService;
        _configuration = configuration;
        _appConfiguration = appConfiguration;
        _localDb = localDb;
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SBMS");
        Directory.CreateDirectory(folder);
        _prefsPath = Path.Combine(folder, "notification-prefs.json");
    }

    public async Task<SettingsPageData> LoadAsync(CancellationToken cancellationToken = default)
    {
        var building = await _db.BuildingInfos.FirstOrDefaultAsync(cancellationToken)
                       ?? new Domain.Entities.Building.BuildingInfo();

        var users = await _db.Users.IgnoreQueryFilters()
            .Where(u => u.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var activeUsers = users.Count(u => u.IsActive);
        var distinctRoles = users.Select(u => u.Role).Distinct().Count();
        var syncLogs = await _db.SyncLogs.IgnoreQueryFilters().CountAsync(cancellationToken);
        var lastSync = _syncService.LastSyncAt;

        var prefs = LoadNotificationPrefs();
        var dbPath = MaskConnectionString(_localDb.ConnectionString);
        var dbDir = _localDb.DisplayLabel;
        const long dbSize = 0;

        var env = _configuration["ASPNETCORE_ENVIRONMENT"]
                  ?? (_configuration["Api:BaseUrl"]?.Contains("localhost") == true ? "Développement" : "Production");

        var premisesCount = await _db.Premises.IgnoreQueryFilters()
            .CountAsync(p => p.DeletedAt == null, cancellationToken);
        var emailsCount = await _db.CachedEmails.IgnoreQueryFilters()
            .CountAsync(e => e.DeletedAt == null, cancellationToken);
        var emailAccountsCount = await _db.EmailAccounts.IgnoreQueryFilters()
            .CountAsync(e => e.DeletedAt == null, cancellationToken);
        var documentsCount = await _db.LeaseContracts.IgnoreQueryFilters()
            .CountAsync(c => c.DeletedAt == null, cancellationToken)
            + await _db.SupplierContracts.IgnoreQueryFilters()
                .CountAsync(c => c.DeletedAt == null, cancellationToken)
            + await _db.CachedEmails.IgnoreQueryFilters()
                .CountAsync(e => e.DeletedAt == null && e.HasAttachments, cancellationToken);

        var appearance = AppearanceThemeService.LoadPrefs();
        var config = _appConfiguration.Current;
        var apiUrl = _configuration["Api:BaseUrl"] ?? "—";

        return new SettingsPageData
        {
            CompanyName = building.Name,
            TimeZoneId = building.TimeZoneId,
            Currency = building.Currency,
            UsdExchangeRate = building.UsdExchangeRate,
            DateFormat = building.DateFormat,
            Language = building.Language,
            TimeFormat = building.TimeFormat,
            MaintenanceMode = building.MaintenanceMode,
            LogoPath = building.LogoPath,
            ActiveUsers = activeUsers,
            TotalUsers = users.Count,
            DistinctRoles = distinctRoles,
            SyncLogCount = syncLogs,
            LastSyncAt = lastSync,
            SyncStatusLabel = lastSync.HasValue
                ? (DateTime.UtcNow - lastSync.Value).TotalHours < 24 ? "À jour" : "En retard"
                : "Jamais",
            DatabaseSizeBytes = dbSize,
            DatabaseFilePath = dbPath,
            DatabaseDataDirectory = dbDir,
            AppVersion = $"v{typeof(SettingsService).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"}",
            EnvironmentName = env,
            NotifyEmail = prefs.NotifyEmail,
            NotifyPush = prefs.NotifyPush,
            NotifyCritical = prefs.NotifyCritical,
            NotifyDailyReports = prefs.NotifyDailyReports,
            ActiveSessions = 1,
            AuthorizedDevices = 1,
            TwoFactorEnabled = false,
            BuildingAddress = building.Address,
            BuildingCity = building.City,
            BuildingCountry = building.Country,
            BuildingPhone = building.Phone,
            BuildingEmail = building.Email,
            BuildingWebsite = building.Website,
            BuildingNationalId = building.NationalId,
            BuildingFloors = building.TotalFloors,
            OwnerType = building.OwnerType,
            LegalRepresentative = building.LegalRepresentative,
            SecondaryPhone = building.SecondaryPhone,
            TaxId = building.TaxId,
            BankName = building.BankName,
            BankAccount = building.BankAccount,
            BuildingDisplayName = building.BuildingDisplayName,
            BuildingType = building.BuildingType,
            ApartmentCount = building.ApartmentCount,
            CommercialUnitCount = building.CommercialUnitCount,
            TotalPremises = building.TotalPremises,
            ParkingSpaces = building.ParkingSpaces,
            HasElevator = building.HasElevator,
            YearBuilt = building.YearBuilt,
            EquipmentAndInstallations = building.EquipmentAndInstallations,
            ManagementRules = building.ManagementRules,
            PremisesCount = premisesCount,
            BuildingAreaSqM = building.TotalAreaSqM,
            EmailsCount = emailsCount,
            EmailAccountsCount = emailAccountsCount,
            DocumentsCount = documentsCount,
            ApiBaseUrl = apiUrl,
            AccentColorHex = config.PrimaryColorHex,
            SidebarColorHex = config.SidebarColorHex,
            SecondaryColorHex = config.SecondaryColorHex,
            ThemeMode = config.ThemeMode.ToString(),
            CompactTables = config.CompactTables,
            ShowKpiSparklines = config.ShowKpiSparklines
        };
    }

    public async Task<IReadOnlyList<SettingsBackupItem>> GetRecentBackupsAsync(
        int take = 12,
        CancellationToken cancellationToken = default)
    {
        var fr = CultureInfo.GetCultureInfo("fr-FR");
        var logs = await _db.SyncLogs.IgnoreQueryFilters()
            .OrderByDescending(s => s.StartedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return logs.Select(s => new SettingsBackupItem
        {
            StartedAt = s.StartedAt,
            DateDisplay = s.StartedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", fr),
            Success = s.Success,
            StatusDisplay = s.Success ? "Réussie" : "Échec",
            DetailsDisplay = s.Success
                ? $"{s.RecordsPushed} envoyés · {s.RecordsPulled} reçus · {s.Direction}"
                : s.ErrorMessage ?? "Erreur inconnue"
        }).ToList();
    }

    public async Task<IReadOnlyList<SettingsLogPreviewItem>> GetRecentSystemLogsAsync(
        int take = 8,
        CancellationToken cancellationToken = default)
    {
        var fr = CultureInfo.GetCultureInfo("fr-FR");
        var logs = await _db.SystemLogs.IgnoreQueryFilters()
            .OrderByDescending(s => s.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return logs.Select(s => new SettingsLogPreviewItem
        {
            TimeDisplay = s.CreatedAt.ToLocalTime().ToString("dd/MM HH:mm", fr),
            Level = s.Level,
            Source = s.Source,
            Message = s.Message.Length > 120 ? s.Message[..117] + "…" : s.Message
        }).ToList();
    }

    public IReadOnlyList<SettingsIntegrationItem> GetIntegrations(string apiBaseUrl, DateTime? lastSyncAt)
    {
        var syncOk = lastSyncAt.HasValue && (DateTime.UtcNow - lastSyncAt.Value).TotalHours < 48;
        return
        [
            new()
            {
                Name = "API SBMS",
                Description = apiBaseUrl,
                StatusLabel = string.IsNullOrWhiteSpace(apiBaseUrl) ? "Non configurée" : "Configurée",
                IsConnected = !string.IsNullOrWhiteSpace(apiBaseUrl)
            },
            new()
            {
                Name = "Synchronisation cloud",
                Description = "Échange bidirectionnel des données",
                StatusLabel = syncOk ? "À jour" : lastSyncAt.HasValue ? "En retard" : "Jamais synchronisé",
                IsConnected = syncOk
            },
            new()
            {
                Name = "Base MySQL (XAMPP)",
                Description = "Stockage hors ligne",
                StatusLabel = "Opérationnelle",
                IsConnected = true
            },
            new()
            {
                Name = "Comptes email",
                Description = "Gmail / Outlook via IMAP",
                StatusLabel = "Voir module Emails",
                IsConnected = false
            }
        ];
    }

    public async Task SaveCompanyProfileAsync(
        string companyName,
        string address,
        string city,
        string country,
        string phone,
        string email,
        string website,
        string nationalId,
        int totalFloors,
        CancellationToken cancellationToken = default)
    {
        await SaveBuildingProfileAsync(new BuildingProfileInput
        {
            CompanyName = companyName,
            Address = address,
            City = city,
            Country = country,
            Phone = phone,
            Email = email,
            Website = website,
            NationalId = nationalId,
            TotalFloors = totalFloors
        }, cancellationToken);
    }

    public async Task SaveBuildingProfileAsync(
        BuildingProfileInput input,
        CancellationToken cancellationToken = default,
        bool reloadApplicationConfiguration = true)
    {
        using (await DbContextAccessLock.AcquireAsync(cancellationToken))
        {
        var building = await _db.BuildingInfos.FirstOrDefaultAsync(cancellationToken);
        if (building is null)
        {
            building = new Domain.Entities.Building.BuildingInfo();
            _db.BuildingInfos.Add(building);
        }

        building.Name = string.IsNullOrWhiteSpace(input.CompanyName)
            ? Domain.Entities.Building.BuildingInfoDefaults.CompanyName
            : input.CompanyName.Trim();
        building.OwnerType = string.IsNullOrWhiteSpace(input.OwnerType)
            ? "Particulier"
            : input.OwnerType.Trim();
        building.LegalRepresentative = input.LegalRepresentative?.Trim();
        building.Address = input.Address.Trim();
        building.City = input.City.Trim();
        building.Country = input.Country.Trim();
        building.Phone = input.Phone.Trim();
        building.SecondaryPhone = input.SecondaryPhone?.Trim();
        building.Email = input.Email.Trim();
        building.Website = input.Website.Trim();
        building.NationalId = input.NationalId.Trim();
        building.TaxId = input.TaxId?.Trim();
        building.BankName = input.BankName?.Trim();
        building.BankAccount = input.BankAccount?.Trim();
        building.BuildingDisplayName = Domain.Entities.Building.BuildingInfoDefaults.ManagedBuildingName;
        building.BuildingType = input.BuildingType.Trim();
        building.TotalFloors = Math.Max(0, input.TotalFloors);
        building.TotalPremises = Math.Max(0, input.TotalPremises);
        building.ApartmentCount = Math.Max(0, input.ApartmentCount);
        building.CommercialUnitCount = Math.Max(0, input.CommercialUnitCount);
        building.TotalAreaSqM = Math.Max(0, input.TotalAreaSqM);
        building.ParkingSpaces = Math.Max(0, input.ParkingSpaces);
        building.HasElevator = input.HasElevator;
        building.YearBuilt = input.YearBuilt;
        building.EquipmentAndInstallations = input.EquipmentAndInstallations.Trim();
        building.ManagementRules = input.ManagementRules.Trim();
        if (string.IsNullOrWhiteSpace(building.TimeZoneId))
            building.TimeZoneId = "Africa/Kinshasa";
        if (string.IsNullOrWhiteSpace(building.Currency))
            building.Currency = "USD";
        building.MarkUpdated();
        await _db.SaveChangesAsync(cancellationToken);
        }

        if (reloadApplicationConfiguration)
            await _appConfiguration.ReloadAndApplyAsync(cancellationToken);
    }

    public async Task SaveAppearancePrefsAsync(
        string themeMode,
        string accentColorHex,
        string? sidebarColorHex,
        string? secondaryColorHex,
        bool compactTables,
        bool showKpiSparklines,
        CancellationToken cancellationToken = default)
    {
        var mode = Enum.TryParse<Models.AppThemeMode>(themeMode, true, out var parsed)
            ? parsed
            : Models.AppThemeMode.Light;

        _appConfiguration.SaveAndApplyAppearance(
            mode,
            accentColorHex,
            sidebarColorHex,
            secondaryColorHex,
            compactTables,
            showKpiSparklines);

        await Task.CompletedTask;
    }

    public async Task SaveGeneralAsync(
        string companyName,
        string timeZoneId,
        string currency,
        decimal usdExchangeRate,
        string dateFormat,
        string language,
        string timeFormat,
        bool maintenanceMode,
        string? logoPath,
        CancellationToken cancellationToken = default)
    {
        var building = await _db.BuildingInfos.FirstOrDefaultAsync(cancellationToken);
        if (building is null)
        {
            building = new Domain.Entities.Building.BuildingInfo();
            _db.BuildingInfos.Add(building);
        }

        building.Name = string.IsNullOrWhiteSpace(companyName)
            ? "Smart Building"
            : companyName.Trim();
        building.TimeZoneId = SettingsLookups.ToTimeZoneId(timeZoneId);
        building.Currency = SettingsLookups.ParseCurrencyCode(currency);
        building.UsdExchangeRate = usdExchangeRate > 0 ? usdExchangeRate : 2850m;
        building.DateFormat = string.IsNullOrWhiteSpace(dateFormat) ? "dd/MM/yyyy" : dateFormat;
        building.Language = string.IsNullOrWhiteSpace(language) ? "Français" : language;
        building.TimeFormat = string.IsNullOrWhiteSpace(timeFormat) ? "24 heures" : timeFormat;
        building.MaintenanceMode = maintenanceMode;
        building.LogoPath = PersistLogo(logoPath);
        building.MarkUpdated();

        await _db.SaveChangesAsync(cancellationToken);
        await _appConfiguration.ReloadAndApplyAsync(cancellationToken);
    }

    private static string? PersistLogo(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            return null;

        if (!File.Exists(sourcePath))
            return null;

        var logosDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SBMS",
            "logos");
        Directory.CreateDirectory(logosDir);

        var storedRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SBMS");
        var fullSource = Path.GetFullPath(sourcePath);
        if (fullSource.StartsWith(Path.GetFullPath(storedRoot), StringComparison.OrdinalIgnoreCase))
            return fullSource;

        var extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrEmpty(extension))
            extension = ".png";

        var destination = Path.Combine(logosDir, $"company-logo{extension}");
        File.Copy(sourcePath, destination, overwrite: true);
        return destination;
    }

    public void SaveNotificationPrefs(bool email, bool push, bool critical, bool dailyReports)
    {
        var prefs = new NotificationPrefs
        {
            NotifyEmail = email,
            NotifyPush = push,
            NotifyCritical = critical,
            NotifyDailyReports = dailyReports
        };
        File.WriteAllText(_prefsPath, JsonSerializer.Serialize(prefs));
    }

    private NotificationPrefs LoadNotificationPrefs()
    {
        if (!File.Exists(_prefsPath))
            return new NotificationPrefs();

        try
        {
            return JsonSerializer.Deserialize<NotificationPrefs>(File.ReadAllText(_prefsPath))
                   ?? new NotificationPrefs();
        }
        catch
        {
            return new NotificationPrefs();
        }
    }

    public Task ResetLocalDatabaseAsync(CancellationToken cancellationToken = default) =>
        DesktopDatabaseResetService.ResetLocalDatabaseAsync(_db, cancellationToken);

    private static string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return "—";

        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].StartsWith("Password=", StringComparison.OrdinalIgnoreCase))
                parts[i] = "Password=***";
        }

        return string.Join(';', parts);
    }

    private sealed class NotificationPrefs
    {
        public bool NotifyEmail { get; set; } = true;
        public bool NotifyPush { get; set; } = true;
        public bool NotifyCritical { get; set; } = true;
        public bool NotifyDailyReports { get; set; }
    }
}
