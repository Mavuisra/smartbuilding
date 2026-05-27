using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Win32;
using SkiaSharp;
using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;
using SmartBuilding.Desktop.WPF.Views;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly SettingsService _settingsService;
    private readonly AppConfigurationService _appConfiguration;
    private readonly SessionService _session;

    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _userRole = string.Empty;
    [ObservableProperty] private string _userInitials = "AD";
    [ObservableProperty] private int _notificationCount;

    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _selectedCategoryId = "general";

    [ObservableProperty] private string _companyName = string.Empty;
    [ObservableProperty] private string _selectedTimeZone = string.Empty;
    [ObservableProperty] private string _selectedCurrency = string.Empty;
    [ObservableProperty] private string _usdExchangeRateText = "2850";
    public bool IsUsdCurrency =>
        string.Equals(SettingsLookups.ParseCurrencyCode(SelectedCurrency), "USD", StringComparison.OrdinalIgnoreCase);
    [ObservableProperty] private string _selectedDateFormat = string.Empty;
    [ObservableProperty] private string _selectedLanguage = string.Empty;
    [ObservableProperty] private string _selectedTimeFormat = string.Empty;
    [ObservableProperty] private bool _maintenanceMode;
    [ObservableProperty] private string? _logoPath;
    [ObservableProperty] private bool _hasLogo;

    [ObservableProperty] private string _activeUsersDisplay = "0";
    [ObservableProperty] private string _activeUsersSub = "0% actifs";
    [ObservableProperty] private string _rolesDisplay = "0";
    [ObservableProperty] private string _rolesSub = "—";
    [ObservableProperty] private string _backupsDisplay = "0";
    [ObservableProperty] private string _backupsSub = "—";
    [ObservableProperty] private string _syncKpiLabel = "—";
    [ObservableProperty] private string _syncKpiSub = "—";
    [ObservableProperty] private string _securityKpiLabel = "Élevée";
    [ObservableProperty] private string _securityKpiSub = "JWT configuré";

    [ObservableProperty] private bool _notifyEmail = true;
    [ObservableProperty] private bool _notifyPush = true;
    [ObservableProperty] private bool _notifyCritical = true;
    [ObservableProperty] private bool _notifyDailyReports;

    [ObservableProperty] private string _twoFactorLabel = "Désactivée";
    [ObservableProperty] private string _activeSessionsLabel = "1";
    [ObservableProperty] private string _authorizedDevicesLabel = "1";
    [ObservableProperty] private string _appVersion = "v1.0.0";
    [ObservableProperty] private string _databaseLabel = "SQLite locale";
    [ObservableProperty] private string _storageLabel = "—";
    [ObservableProperty] private string _environmentName = "Développement";

    [ObservableProperty] private ISeries[] _usersSparkline = [];
    [ObservableProperty] private ISeries[] _rolesSparkline = [];
    [ObservableProperty] private ISeries[] _backupsSparkline = [];
    [ObservableProperty] private ISeries[] _syncSparkline = [];
    [ObservableProperty] private ISeries[] _securitySparkline = [];

    [ObservableProperty] private string _buildingAddress = string.Empty;
    [ObservableProperty] private string _buildingCity = string.Empty;
    [ObservableProperty] private string _buildingCountry = string.Empty;
    [ObservableProperty] private string _buildingPhone = string.Empty;
    [ObservableProperty] private string _buildingEmail = string.Empty;
    [ObservableProperty] private string _buildingWebsite = string.Empty;
    [ObservableProperty] private string _buildingNationalId = string.Empty;
    [ObservableProperty] private string _companyLocationBadge = "—";
    [ObservableProperty] private int _buildingFloors;
    [ObservableProperty] private string _premisesCountDisplay = "0";
    [ObservableProperty] private string _buildingAreaDisplay = "—";
    [ObservableProperty] private string _emailsCountDisplay = "0";
    [ObservableProperty] private string _emailAccountsDisplay = "0";
    [ObservableProperty] private string _documentsCountDisplay = "0";
    [ObservableProperty] private string _apiBaseUrl = "—";
    [ObservableProperty] private string _selectedAccentColor = "#2D6A4F";
    [ObservableProperty] private string _selectedSidebarColor = "#1B3D3B";
    [ObservableProperty] private string _selectedSecondaryColor = "#0D9488";
    [ObservableProperty] private string _selectedThemeMode = "Clair";
    [ObservableProperty] private bool _compactTables;
    [ObservableProperty] private bool _showKpiSparklines = true;
    [ObservableProperty] private string _categoryTitle = "Général";
    [ObservableProperty] private string _categoryDescription = "Configuration globale de l'application";

    public ObservableCollection<string> TimeZones { get; } = [];
    public ObservableCollection<string> AccentColorOptions { get; } = [];
    public ObservableCollection<string> ThemeModeOptions { get; } = ["Clair", "Sombre", "Personnalisé"];
    public ObservableCollection<SettingsBackupItem> BackupHistory { get; } = [];
    public ObservableCollection<SettingsLogPreviewItem> RecentLogs { get; } = [];
    public ObservableCollection<SettingsIntegrationItem> Integrations { get; } = [];
    public ObservableCollection<string> Currencies { get; } = [];
    public ObservableCollection<string> DateFormats { get; } = [];
    public ObservableCollection<string> Languages { get; } = [];
    public ObservableCollection<string> TimeFormats { get; } = [];
    public ObservableCollection<SettingsCategoryItem> Categories { get; } = [];
    public ObservableCollection<SettingsQuickAccessItem> QuickAccessItems { get; } = [];

    public event Action<string>? NavigateToModuleRequested;

    public SettingsViewModel(
        SettingsService settingsService,
        AppConfigurationService appConfiguration,
        SessionService session)
    {
        _settingsService = settingsService;
        _appConfiguration = appConfiguration;
        _appConfiguration.ConfigurationChanged += (_, _) => SyncAppearanceFromGlobalConfig();
        _session = session;
        UserName = session.CurrentUser?.FullName ?? "Admin SBMS";
        UserRole = session.CurrentUser?.Role ?? "Administrateur";
        UserInitials = GetInitials(UserName);

        InitLookups();
        InitCategories();
        InitQuickAccess();
        InitAccentOptions();
    }

    partial void OnSelectedCategoryIdChanged(string value) => UpdateCategoryHeader(value);

    partial void OnSelectedCurrencyChanged(string value)
    {
        OnPropertyChanged(nameof(IsUsdCurrency));
        if (IsUsdCurrency && !TryParseUsdRate(out _))
            _ = PromptUsdExchangeRateAsync();
    }

    partial void OnBuildingCityChanged(string value) => UpdateCompanyLocationBadge();
    partial void OnBuildingCountryChanged(string value) => UpdateCompanyLocationBadge();
    partial void OnBuildingAddressChanged(string value) => UpdateCompanyLocationBadge();

    private void UpdateCompanyLocationBadge()
    {
        var city = BuildingCity?.Trim() ?? "";
        var country = BuildingCountry?.Trim() ?? "";
        CompanyLocationBadge = city.Length > 0 && country.Length > 0
            ? $"{city} — {country}"
            : city.Length > 0 ? city : country.Length > 0 ? country : "—";
    }

    private void SyncAppearanceFromGlobalConfig()
    {
        var c = _appConfiguration.Current;
        SelectedAccentColor = c.PrimaryColorHex;
        SelectedSidebarColor = c.SidebarColorHex;
        SelectedSecondaryColor = c.SecondaryColorHex;
        SelectedThemeMode = ToThemeModeLabel(c.ThemeMode.ToString());
        CompactTables = c.CompactTables;
        ShowKpiSparklines = c.ShowKpiSparklines;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var data = await _settingsService.LoadAsync();
            CompanyName = data.CompanyName;
            SelectedTimeZone = SettingsLookups.ToTimeZoneDisplay(data.TimeZoneId);
            SelectedCurrency = SettingsLookups.ToCurrencyDisplay(data.Currency);
            UsdExchangeRateText = data.UsdExchangeRate > 0
                ? data.UsdExchangeRate.ToString("N0", CultureInfo.GetCultureInfo("fr-FR"))
                : "2850";
            SelectedDateFormat = data.DateFormat;
            SelectedLanguage = data.Language;
            SelectedTimeFormat = data.TimeFormat;
            MaintenanceMode = data.MaintenanceMode;
            LogoPath = data.LogoPath;
            HasLogo = !string.IsNullOrEmpty(data.LogoPath) && File.Exists(data.LogoPath);

            ActiveUsersDisplay = data.ActiveUsers.ToString();
            ActiveUsersSub = data.TotalUsers == 0
                ? "0% actifs"
                : $"{data.ActiveUsers * 100 / data.TotalUsers}% actifs";
            RolesDisplay = data.DistinctRoles.ToString();
            RolesSub = "Rôles utilisés";
            BackupsDisplay = data.SyncLogCount.ToString();
            BackupsSub = data.LastSyncAt?.ToLocalTime().Date == DateTime.Today
                ? "Dernière : Aujourd'hui"
                : data.LastSyncAt.HasValue
                    ? $"Dernière : {data.LastSyncAt.Value.ToLocalTime():dd/MM/yyyy}"
                    : "Aucune opération";
            SyncKpiLabel = data.SyncStatusLabel;
            SyncKpiSub = data.LastSyncAt.HasValue
                ? $"Dernière sync : {data.LastSyncAt.Value.ToLocalTime():HH:mm}"
                : "Dernière sync : —";

            NotifyEmail = data.NotifyEmail;
            NotifyPush = data.NotifyPush;
            NotifyCritical = data.NotifyCritical;
            NotifyDailyReports = data.NotifyDailyReports;

            TwoFactorLabel = data.TwoFactorEnabled ? "Activée" : "Désactivée";
            ActiveSessionsLabel = data.ActiveSessions.ToString();
            AuthorizedDevicesLabel = data.AuthorizedDevices.ToString();
            AppVersion = data.AppVersion;
            EnvironmentName = data.EnvironmentName;
            StorageLabel = FormatBytes(data.DatabaseSizeBytes);

            UsersSparkline = BuildSparkline([data.ActiveUsers], "#8B5CF6");
            RolesSparkline = BuildSparkline([data.DistinctRoles], "#3B82F6");
            BackupsSparkline = BuildSparkline([data.SyncLogCount], "#2D6A4F");
            SyncSparkline = BuildSparkline([data.LastSyncAt.HasValue ? 1 : 0], "#F59E0B");
            SecuritySparkline = BuildSparkline([1], "#EF4444");

            NotificationCount = data.NotifyCritical ? 1 : 0;

            BuildingAddress = data.BuildingAddress;
            BuildingCity = data.BuildingCity;
            BuildingCountry = data.BuildingCountry;
            BuildingPhone = data.BuildingPhone;
            BuildingEmail = data.BuildingEmail;
            BuildingWebsite = data.BuildingWebsite;
            BuildingNationalId = data.BuildingNationalId;
            UpdateCompanyLocationBadge();
            BuildingFloors = data.BuildingFloors;
            PremisesCountDisplay = data.PremisesCount.ToString();
            BuildingAreaDisplay = data.BuildingAreaSqM > 0
                ? $"{data.BuildingAreaSqM:N0} m²"
                : "—";
            EmailsCountDisplay = data.EmailsCount.ToString();
            EmailAccountsDisplay = data.EmailAccountsCount.ToString();
            DocumentsCountDisplay = data.DocumentsCount.ToString();
            ApiBaseUrl = data.ApiBaseUrl;
            SelectedAccentColor = data.AccentColorHex;
            SelectedSidebarColor = data.SidebarColorHex;
            SelectedSecondaryColor = data.SecondaryColorHex;
            SelectedThemeMode = ToThemeModeLabel(data.ThemeMode);
            CompactTables = data.CompactTables;
            ShowKpiSparklines = data.ShowKpiSparklines;

            BackupHistory.Clear();
            foreach (var item in await _settingsService.GetRecentBackupsAsync())
                BackupHistory.Add(item);

            RecentLogs.Clear();
            foreach (var item in await _settingsService.GetRecentSystemLogsAsync())
                RecentLogs.Add(item);

            Integrations.Clear();
            foreach (var item in _settingsService.GetIntegrations(data.ApiBaseUrl, data.LastSyncAt))
                Integrations.Add(item);

            UpdateCategoryHeader(SelectedCategoryId);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var timeZone = string.IsNullOrWhiteSpace(SelectedTimeZone) ? TimeZones[0] : SelectedTimeZone;
            var currency = string.IsNullOrWhiteSpace(SelectedCurrency) ? Currencies[0] : SelectedCurrency;
            var dateFormat = string.IsNullOrWhiteSpace(SelectedDateFormat) ? DateFormats[0] : SelectedDateFormat;
            var language = string.IsNullOrWhiteSpace(SelectedLanguage) ? Languages[0] : SelectedLanguage;
            var timeFormat = string.IsNullOrWhiteSpace(SelectedTimeFormat) ? TimeFormats[0] : SelectedTimeFormat;

            if (!await EnsureUsdExchangeRateAsync())
                return;

            await _settingsService.SaveGeneralAsync(
                CompanyName,
                timeZone,
                currency,
                ParseUsdRateOrDefault(),
                dateFormat,
                language,
                timeFormat,
                MaintenanceMode,
                LogoPath);

            await _settingsService.SaveAppearancePrefsAsync(
                FromThemeModeLabel(SelectedThemeMode),
                SelectedAccentColor,
                SelectedSidebarColor,
                SelectedSecondaryColor,
                CompactTables,
                ShowKpiSparklines);

            _settingsService.SaveNotificationPrefs(NotifyEmail, NotifyPush, NotifyCritical, NotifyDailyReports);
            StatusMessage = "Configuration globale enregistrée et appliquée à toute l'application.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = null;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectCategory(string? categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
            return;
        SelectedCategoryId = categoryId;
        ErrorMessage = null;
    }

    [RelayCommand]
    private void QuickNavigate(string? categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
            return;
        SelectedCategoryId = categoryId;
    }

    [RelayCommand]
    private void OpenModule(string? moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            return;
        NavigateToModuleRequested?.Invoke(moduleId);
    }

    [RelayCommand]
    private async Task SaveBuildingAsync()
    {
        if (string.IsNullOrWhiteSpace(CompanyName))
        {
            ErrorMessage = "La raison sociale est obligatoire.";
            return;
        }
        if (string.IsNullOrWhiteSpace(BuildingAddress) || string.IsNullOrWhiteSpace(BuildingCity))
        {
            ErrorMessage = "L'adresse et la ville sont obligatoires.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _settingsService.SaveCompanyProfileAsync(
                CompanyName,
                BuildingAddress,
                BuildingCity,
                BuildingCountry,
                BuildingPhone,
                BuildingEmail,
                BuildingWebsite,
                BuildingNationalId,
                BuildingFloors);
            StatusMessage = "Société enregistrée — interface et documents mis à jour.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = null;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ApplyKinshasaDefaults()
    {
        CompanyName = BuildingInfoDefaults.CompanyName;
        BuildingAddress = BuildingInfoDefaults.Address;
        BuildingCity = BuildingInfoDefaults.City;
        BuildingCountry = BuildingInfoDefaults.Country;
        BuildingPhone = BuildingInfoDefaults.Phone;
        BuildingEmail = BuildingInfoDefaults.Email;
        BuildingWebsite = BuildingInfoDefaults.Website;
        BuildingNationalId = BuildingInfoDefaults.NationalId;
        UpdateCompanyLocationBadge();
        StatusMessage = "Modèle Kinshasa — Gombe appliqué. Cliquez sur Enregistrer pour confirmer.";
        ErrorMessage = null;
    }

    [RelayCommand]
    private void SaveNotifications()
    {
        ErrorMessage = null;
        try
        {
            _settingsService.SaveNotificationPrefs(NotifyEmail, NotifyPush, NotifyCritical, NotifyDailyReports);
            StatusMessage = "Préférences de notifications enregistrées.";
        }
        catch (Exception ex)
        {
            StatusMessage = null;
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task SaveAppearanceAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            await _settingsService.SaveAppearancePrefsAsync(
                FromThemeModeLabel(SelectedThemeMode),
                SelectedAccentColor,
                SelectedSidebarColor,
                SelectedSecondaryColor,
                CompactTables,
                ShowKpiSparklines);

            if (System.Windows.Application.Current?.MainWindow is not null)
                ThemeRefreshHelper.RefreshElement(System.Windows.Application.Current.MainWindow);

            SyncAppearanceFromGlobalConfig();
            StatusMessage = "Apparence appliquée — thème mis à jour dans toute l'application.";
        }
        catch (Exception ex)
        {
            StatusMessage = null;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PromptUsdExchangeRateAsync()
    {
        var owner = System.Windows.Application.Current?.MainWindow;
        var dialog = new ExchangeRateDialog(ParseUsdRateOrDefault(), owner);
        if (dialog.ShowDialog() != true || dialog.ExchangeRate is not { } rate)
            return;

        UsdExchangeRateText = rate.ToString("N0", CultureInfo.GetCultureInfo("fr-FR"));
        ErrorMessage = null;
        await Task.CompletedTask;
    }

    private async Task<bool> EnsureUsdExchangeRateAsync()
    {
        if (!IsUsdCurrency)
            return true;

        if (TryParseUsdRate(out _))
            return true;

        ErrorMessage = "Le taux de change USD est obligatoire.";
        await PromptUsdExchangeRateAsync();
        return TryParseUsdRate(out _);
    }

    private bool TryParseUsdRate(out decimal rate)
    {
        var raw = UsdExchangeRateText?.Replace(" ", "").Replace(",", ".") ?? "";
        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out rate) && rate > 0)
            return true;
        rate = 0;
        return false;
    }

    private decimal ParseUsdRateOrDefault() =>
        TryParseUsdRate(out var rate) ? rate : 2850m;

    [RelayCommand]
    private void ChangeLogo()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp",
            Title = "Choisir un logo"
        };
        if (dialog.ShowDialog() == true)
        {
            LogoPath = dialog.FileName;
            HasLogo = true;
        }
    }

    [RelayCommand]
    private void RemoveLogo()
    {
        LogoPath = null;
        HasLogo = false;
    }

    private void InitAccentOptions()
    {
        AccentColorOptions.Clear();
        foreach (var hex in new[] { "#2D6A4F", "#1B3D3B", "#2563EB", "#7C3AED", "#0D9488", "#DC2626" })
            AccentColorOptions.Add(hex);
    }

    private static string ToThemeModeLabel(string mode) => mode switch
    {
        "Dark" => "Sombre",
        "Custom" => "Personnalisé",
        _ => "Clair"
    };

    private static string FromThemeModeLabel(string label) => label switch
    {
        "Sombre" => nameof(AppThemeMode.Dark),
        "Personnalisé" => nameof(AppThemeMode.Custom),
        _ => nameof(AppThemeMode.Light)
    };

    private void UpdateCategoryHeader(string categoryId)
    {
        var item = Categories.FirstOrDefault(c =>
            string.Equals(c.Id, categoryId, StringComparison.OrdinalIgnoreCase));
        CategoryTitle = item?.Label ?? "Paramètres";
        CategoryDescription = categoryId switch
        {
            "general" => "Nom, devise, langue et logo de l'entreprise",
            "buildings" => "Coordonnées bailleur sur les quittances PDF (Kinshasa, Gombe)",
            "utilisateurs" => "Comptes, rôles et activité des utilisateurs",
            "permissions" => "Droits d'accès par rôle et module",
            "emails" => "Boîtes mail et communication intégrée",
            "synchronisation" => "État cloud et dernières synchronisations",
            "backups" => "Historique des opérations de synchronisation",
            "security" => "Authentification, sessions et accès",
            "notifications" => "Alertes email, push et rapports",
            "documents" => "Contrats, factures et pièces jointes",
            "appearance" => "Thème clair/sombre, couleurs et affichage global",
            "logs" => "Journal système et événements récents",
            "integrations" => "Services connectés et API",
            "about" => "Version, base de données et environnement",
            _ => "Configuration du système SBMS"
        };
    }

    private void InitLookups()
    {
        TimeZones.Clear();
        foreach (var z in SettingsLookups.TimeZoneDisplays)
            TimeZones.Add(z);

        Currencies.Clear();
        foreach (var c in SettingsLookups.Currencies)
            Currencies.Add(c);

        DateFormats.Clear();
        foreach (var d in SettingsLookups.DateFormats)
            DateFormats.Add(d);

        Languages.Clear();
        foreach (var l in SettingsLookups.Languages)
            Languages.Add(l);

        TimeFormats.Clear();
        foreach (var t in SettingsLookups.TimeFormats)
            TimeFormats.Add(t);
    }

    private void InitCategories()
    {
        Categories.Clear();
        var items = new[]
        {
            ("general", "Général", "Tune"),
            ("buildings", "Société & bailleur", "Domain"),
            ("utilisateurs", "Utilisateurs & rôles", "AccountGroup"),
            ("permissions", "Permissions", "ShieldKey"),
            ("emails", "Emails & SMTP", "Email"),
            ("synchronisation", "Synchronisation", "Sync"),
            ("backups", "Sauvegardes", "BackupRestore"),
            ("security", "Sécurité", "ShieldCheck"),
            ("notifications", "Notifications", "Bell"),
            ("documents", "Documents", "FileDocument"),
            ("appearance", "Apparence", "Palette"),
            ("logs", "Logs système", "TextBox"),
            ("integrations", "Intégrations", "Connection"),
            ("about", "À propos", "Information")
        };
        foreach (var (id, label, icon) in items)
            Categories.Add(new SettingsCategoryItem { Id = id, Label = label, IconKind = icon });

        UpdateCategoryHeader(SelectedCategoryId);
    }

    private void InitQuickAccess()
    {
        QuickAccessItems.Clear();
        QuickAccessItems.Add(new SettingsQuickAccessItem
        {
            CategoryId = "utilisateurs", Title = "Utilisateurs & rôles",
            Description = "Gérer les comptes et rôles", IconKind = "AccountGroup",
            IconColor = "#7C3AED", IconBg = "#EDE9FE"
        });
        QuickAccessItems.Add(new SettingsQuickAccessItem
        {
            CategoryId = "permissions", Title = "Permissions",
            Description = "Configurer les droits d'accès", IconKind = "ShieldKey",
            IconColor = "#2563EB", IconBg = "#DBEAFE"
        });
        QuickAccessItems.Add(new SettingsQuickAccessItem
        {
            CategoryId = "backups", Title = "Sauvegardes",
            Description = "Historique et restauration", IconKind = "BackupRestore",
            IconColor = "#2D6A4F", IconBg = "#D1FAE5"
        });
        QuickAccessItems.Add(new SettingsQuickAccessItem
        {
            CategoryId = "emails", Title = "Emails & SMTP",
            Description = "Comptes et serveur mail", IconKind = "Email",
            IconColor = "#0891B2", IconBg = "#CFFAFE"
        });
        QuickAccessItems.Add(new SettingsQuickAccessItem
        {
            CategoryId = "synchronisation", Title = "Synchronisation",
            Description = "Cloud et mode hors ligne", IconKind = "Sync",
            IconColor = "#D97706", IconBg = "#FFEDD5"
        });
        QuickAccessItems.Add(new SettingsQuickAccessItem
        {
            CategoryId = "logs", Title = "Logs système",
            Description = "Journal d'activité", IconKind = "TextBox",
            IconColor = "#64748B", IconBg = "#F1F5F9"
        });
        QuickAccessItems.Add(new SettingsQuickAccessItem
        {
            CategoryId = "security", Title = "Sécurité",
            Description = "Authentification et accès", IconKind = "ShieldCheck",
            IconColor = "#DC2626", IconBg = "#FEE2E2"
        });
        QuickAccessItems.Add(new SettingsQuickAccessItem
        {
            CategoryId = "integrations", Title = "Intégrations",
            Description = "Services connectés", IconKind = "Connection",
            IconColor = "#0D9488", IconBg = "#CCFBF1"
        });
    }

    private static ISeries[] BuildSparkline(int[] values, string color) =>
    [
        new LineSeries<int>
        {
            Values = values,
            Fill = new SolidColorPaint(SKColor.Parse(color).WithAlpha(40)),
            Stroke = new SolidColorPaint(SKColor.Parse(color)) { StrokeThickness = 2 },
            GeometrySize = 0,
            LineSmoothness = 0.5
        }
    ];

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F2} GB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "AD";
        if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
        return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
    }
}
