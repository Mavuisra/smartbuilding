using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using MaterialDesignColors;
using Microsoft.Extensions.DependencyInjection;
using MaterialDesignThemes.Wpf;
using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Infrastructure.Persistence;

namespace SmartBuilding.Desktop.WPF.Services;

/// <summary>
/// Configuration globale dynamique — charge société (DB) + apparence (JSON), applique le thème à toute l'application.
/// </summary>
public sealed class AppConfigurationService
{
    private static readonly string AppearancePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SBMS",
        "appearance-prefs.json");

    private readonly IServiceProvider _services;
    private readonly AppBrandingState _branding;
    private readonly object _sync = new();

    public AppConfiguration Current { get; private set; } = AppConfiguration.Default;

    /// <summary>Accès statique pour PDF / services non injectés.</summary>
    public static AppConfigurationService? Instance { get; private set; }

    public event EventHandler? ConfigurationChanged;

    public AppConfigurationService(IServiceProvider services, AppBrandingState branding)
    {
        _services = services;
        _branding = branding;
        Instance = this;
    }

    public async Task LoadAndApplyAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartBuildingDbContext>();
        var building = await db.BuildingInfos.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                       ?? new BuildingInfo();

        var buildingRow = await db.BuildingInfos.FirstOrDefaultAsync(cancellationToken);
        if (buildingRow is not null
            && (string.IsNullOrWhiteSpace(buildingRow.Currency)
                || buildingRow.Currency.Equals("CDF", StringComparison.OrdinalIgnoreCase)
                || buildingRow.Currency.Equals("FC", StringComparison.OrdinalIgnoreCase)))
        {
            buildingRow.Currency = "USD";
            await db.SaveChangesAsync(cancellationToken);
            building = buildingRow;
        }

        var appearance = LoadAppearanceFile();

        var config = BuildConfiguration(building, appearance);
        lock (_sync)
            Current = config;

        _branding.Apply(config);

        ThemeResourceHelper.EnsureMutableThemeBrushes();
        ApplyCulture(config);
        ApplyToApplication(config);
        ThemeRefreshHelper.RefreshOpenWindows();

        // Reporter l'événement : évite des accès MySQL concurrents sur le DbContext du shell.
        var handlers = ConfigurationChanged;
        if (handlers is null)
            return;

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            handlers.Invoke(this, EventArgs.Empty);
        else
            _ = dispatcher.InvokeAsync(() => handlers.Invoke(this, EventArgs.Empty),
                System.Windows.Threading.DispatcherPriority.Background);
    }

    public async Task ReloadAndApplyAsync(CancellationToken cancellationToken = default) =>
        await LoadAndApplyAsync(cancellationToken);

    public void SaveAndApplyAppearance(
        AppThemeMode themeMode,
        string primaryColorHex,
        string? sidebarColorHex,
        string? secondaryColorHex,
        bool compactTables,
        bool showKpiSparklines)
    {
        var primary = NormalizeHex(primaryColorHex, "#2D6A4F");
        var secondary = NormalizeHex(secondaryColorHex, "#0D9488");
        var sidebar = ResolveSidebarColor(themeMode, primary, sidebarColorHex);

        var appearance = new AppearanceFile
        {
            ThemeMode = themeMode.ToString(),
            AccentColorHex = primary,
            SidebarColorHex = sidebar,
            SecondaryColorHex = secondary,
            CompactTables = compactTables,
            ShowKpiSparklines = showKpiSparklines
        };
        SaveAppearanceFile(appearance);

        lock (_sync)
            Current = Current.WithAppearance(
                themeMode,
                primary,
                sidebar,
                secondary,
                compactTables,
                showKpiSparklines);

        ApplyToApplication(Current);
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    public BuildingInfo ToBuildingInfo() =>
        new()
        {
            Name = Current.CompanyName,
            Address = Current.Address,
            City = Current.City,
            Country = Current.Country,
            Phone = Current.Phone,
            Email = Current.Email,
            Website = Current.Website,
            NationalId = Current.NationalId,
            LogoPath = Current.LogoPath,
            TimeZoneId = Current.TimeZoneId,
            Currency = Current.Currency,
            DateFormat = Current.DateFormat,
            Language = Current.Language,
            TimeFormat = Current.TimeFormat,
            MaintenanceMode = Current.MaintenanceMode
        };

    private static AppConfiguration BuildConfiguration(BuildingInfo building, AppearanceFile appearance)
    {
        var themeMode = Enum.TryParse<AppThemeMode>(appearance.ThemeMode, true, out var parsed)
            ? parsed
            : AppThemeMode.Light;

        var primary = NormalizeHex(appearance.AccentColorHex, "#2D6A4F");
        var sidebar = ResolveSidebarColor(themeMode, primary, appearance.SidebarColorHex);

        var companyName = string.IsNullOrWhiteSpace(building.Name)
            ? BuildingInfoDefaults.CompanyName
            : building.Name.Trim();

        return new AppConfiguration
        {
            CompanyName = companyName,
            AppTitle = companyName,
            AppSubtitle = AppConfiguration.DefaultAppSubtitle,
            LogoPath = building.LogoPath,
            Address = string.IsNullOrWhiteSpace(building.Address) ? BuildingInfoDefaults.Address : building.Address,
            City = string.IsNullOrWhiteSpace(building.City) ? BuildingInfoDefaults.City : building.City,
            Country = string.IsNullOrWhiteSpace(building.Country) ? BuildingInfoDefaults.Country : building.Country,
            Phone = string.IsNullOrWhiteSpace(building.Phone) ? BuildingInfoDefaults.Phone : building.Phone,
            Email = string.IsNullOrWhiteSpace(building.Email) ? BuildingInfoDefaults.Email : building.Email,
            Website = string.IsNullOrWhiteSpace(building.Website) ? BuildingInfoDefaults.Website : building.Website,
            NationalId = string.IsNullOrWhiteSpace(building.NationalId) ? BuildingInfoDefaults.NationalId : building.NationalId,
            TimeZoneId = building.TimeZoneId ?? "Africa/Kinshasa",
            Currency = NormalizeCurrency(building.Currency),
            UsdExchangeRate = building.UsdExchangeRate > 0 ? building.UsdExchangeRate : 2850m,
            DateFormat = building.DateFormat ?? "dd/MM/yyyy",
            Language = building.Language ?? "Français",
            TimeFormat = building.TimeFormat ?? "24 heures",
            MaintenanceMode = building.MaintenanceMode,
            ThemeMode = themeMode,
            PrimaryColorHex = primary,
            SidebarColorHex = sidebar,
            SecondaryColorHex = NormalizeHex(appearance.SecondaryColorHex, "#0D9488"),
            CompactTables = appearance.CompactTables,
            ShowKpiSparklines = appearance.ShowKpiSparklines
        };
    }

    private static string NormalizeCurrency(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "USD";

        var value = code.Trim();
        if (value.Equals("CDF", StringComparison.OrdinalIgnoreCase)
            || value.Equals("FC", StringComparison.OrdinalIgnoreCase))
            return "USD";

        return value;
    }

    private static string ResolveSidebarColor(AppThemeMode mode, string primary, string? customSidebar)
    {
        if (mode == AppThemeMode.Custom && !string.IsNullOrWhiteSpace(customSidebar))
            return NormalizeHex(customSidebar, "#1B3D3B");

        if (mode == AppThemeMode.Dark)
            return ColorToHex(Darken(ParseColor(primary), 0.35));

        return "#1B3D3B";
    }

    private static string ColorToHex(Color color) =>
        $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static void ApplyCulture(AppConfiguration config)
    {
        try
        {
            var culture = config.Language.Contains("English", StringComparison.OrdinalIgnoreCase)
                ? CultureInfo.GetCultureInfo("en-US")
                : CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }
        catch
        {
            // Ignore invalid culture.
        }
    }

    private static void ApplyToApplication(AppConfiguration config)
    {
        if (System.Windows.Application.Current is null)
            return;

        ThemeResourceHelper.EnsureMutableThemeBrushes();
        var resources = ThemeResourceHelper.GetSbmsThemeDictionary();
        var primary = ParseColor(config.PrimaryColorHex);
        var sidebar = ParseColor(config.SidebarColorHex);
        var secondary = ParseColor(config.SecondaryColorHex);
        var isDark = config.ThemeMode == AppThemeMode.Dark;

        SetBrush(resources, "SbmsAccentGreenBrush", primary);
        SetBrush(resources, "SbmsSidebarActiveBrush", primary);
        SetBrush(resources, "SbmsAccentGreenLightBrush", Lighten(primary, 0.22));
        SetBrush(resources, "SbmsSidebarBrush", sidebar);
        SetBrush(resources, "SbmsSidebarDarkBrush", Darken(sidebar, 0.12));

        SetBrush(resources, "SbmsPageBackgroundBrush", isDark ? ColorFromHex("#0F172A") : ColorFromHex("#F0F4F3"));
        SetBrush(resources, "SbmsCardBrush", isDark ? ColorFromHex("#1E293B") : Colors.White);
        SetBrush(resources, "SbmsSurfaceBrush", isDark ? ColorFromHex("#334155") : ColorFromHex("#F8FAFC"));
        SetBrush(resources, "SbmsBorderBrush", isDark ? ColorFromHex("#475569") : ColorFromHex("#E0E8E6"));
        SetBrush(resources, "SbmsTextMutedBrush", isDark ? ColorFromHex("#94A3B8") : ColorFromHex("#6B7B7A"));

        resources["SbmsDataGridRowHeight"] = config.CompactTables ? 32.0 : 40.0;
        resources["SbmsShowKpiSparklines"] = config.ShowKpiSparklines;

        ApplySettingsVisuals(resources, sidebar, primary, secondary, isDark);
        ApplyMaterialDesignTheme(isDark, primary, secondary);

        ThemeResourceHelper.ApplyMainWindowChrome(isDark);
        ThemeRefreshHelper.RefreshOpenWindows();
    }

    private static void ApplySettingsVisuals(
        ResourceDictionary resources,
        Color sidebar,
        Color primary,
        Color secondary,
        bool isDark)
    {
        if (resources["SbmsSettingsHeroGradient"] is LinearGradientBrush existingGradient
            && existingGradient.GradientStops.Count >= 3
            && !existingGradient.IsFrozen)
        {
            existingGradient.GradientStops[0].Color = Darken(sidebar, isDark ? 0.05 : 0);
            existingGradient.GradientStops[1].Color = primary;
            existingGradient.GradientStops[2].Color = secondary;
        }
        else
        {
            var gradient = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(1, 1)
            };
            gradient.GradientStops.Add(new GradientStop(Darken(sidebar, isDark ? 0.05 : 0), 0));
            gradient.GradientStops.Add(new GradientStop(primary, 0.55));
            gradient.GradientStops.Add(new GradientStop(secondary, 1));
            resources["SbmsSettingsHeroGradient"] = gradient;
        }

        SetBrush(resources, "SbmsSettingsTitleBrush", Darken(primary, isDark ? 0.1 : 0.25));
        SetBrush(resources, "SbmsSettingsPreviewBgBrush", Lighten(primary, isDark ? 0.08 : 0.88));
        SetBrush(resources, "SbmsSettingsPreviewBorderBrush", Lighten(primary, isDark ? 0.25 : 0.55));
        SetBrush(resources, "SbmsSettingsSuccessBadgeBgBrush", Lighten(primary, 0.82));
        SetBrush(resources, "SbmsSettingsSuccessBadgeFgBrush", Darken(primary, 0.15));

        SetBrush(resources, "SbmsSettingsStat1BgBrush", Lighten(primary, 0.88));
        SetBrush(resources, "SbmsSettingsStat1FgBrush", Darken(primary, 0.2));
        SetBrush(resources, "SbmsSettingsStat2BgBrush", Lighten(secondary, 0.85));
        SetBrush(resources, "SbmsSettingsStat2FgBrush", Darken(secondary, 0.15));
    }

    private static void ApplyMaterialDesignTheme(bool isDark, Color primary, Color secondary)
    {
        try
        {
            var palette = new PaletteHelper();
            var theme = palette.GetTheme();
            theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);
            theme.SetPrimaryColor(primary);
            theme.SetSecondaryColor(secondary);
            palette.SetTheme(theme);
        }
        catch
        {
            // MaterialDesign peut ne pas être initialisé très tôt.
        }
    }

    private static AppearanceFile LoadAppearanceFile()
    {
        if (!File.Exists(AppearancePath))
            return new AppearanceFile();

        try
        {
            return JsonSerializer.Deserialize<AppearanceFile>(File.ReadAllText(AppearancePath))
                   ?? new AppearanceFile();
        }
        catch
        {
            return new AppearanceFile();
        }
    }

    private static void SaveAppearanceFile(AppearanceFile file)
    {
        var folder = Path.GetDirectoryName(AppearancePath)!;
        Directory.CreateDirectory(folder);
        File.WriteAllText(AppearancePath, JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void SetBrush(ResourceDictionary resources, string key, Color color) =>
        ThemeResourceHelper.SetBrushColor(resources, key, color);

    private static Color ParseColor(string hex) => ColorFromHex(NormalizeHex(hex, "#2D6A4F"));

    private static Color ColorFromHex(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex)!;
        }
        catch
        {
            return Color.FromRgb(45, 106, 79);
        }
    }

    private static string NormalizeHex(string? hex, string fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return fallback;
        hex = hex.Trim();
        if (!hex.StartsWith('#'))
            hex = "#" + hex;
        return hex.Length is 7 or 9 ? hex : fallback;
    }

    private static Color Lighten(Color color, double amount)
    {
        byte Blend(byte channel) => (byte)Math.Min(255, channel + (255 - channel) * amount);
        return Color.FromRgb(Blend(color.R), Blend(color.G), Blend(color.B));
    }

    private static Color Darken(Color color, double amount)
    {
        byte Blend(byte channel) => (byte)Math.Max(0, channel * (1 - amount));
        return Color.FromRgb(Blend(color.R), Blend(color.G), Blend(color.B));
    }

    private sealed class AppearanceFile
    {
        public string ThemeMode { get; set; } = nameof(AppThemeMode.Light);
        public string AccentColorHex { get; set; } = "#2D6A4F";
        public string SidebarColorHex { get; set; } = "#1B3D3B";
        public string SecondaryColorHex { get; set; } = "#0D9488";
        public bool CompactTables { get; set; }
        public bool ShowKpiSparklines { get; set; } = true;
    }
}
