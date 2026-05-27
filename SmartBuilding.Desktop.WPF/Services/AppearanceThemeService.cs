using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace SmartBuilding.Desktop.WPF.Services;

public static class AppearanceThemeService
{
    private static readonly string PrefsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SBMS",
        "appearance-prefs.json");

    public static void ApplySavedAppearance()
    {
        if (AppConfigurationService.Instance is not null)
            return;

        var prefs = LoadPrefs();
        if (!string.IsNullOrWhiteSpace(prefs.AccentColorHex))
            ApplyAccent(prefs.AccentColorHex);
    }

    public static AppearancePrefs LoadPrefs()
    {
        if (!File.Exists(PrefsPath))
            return new AppearancePrefs();

        try
        {
            return JsonSerializer.Deserialize<AppearancePrefs>(File.ReadAllText(PrefsPath))
                   ?? new AppearancePrefs();
        }
        catch
        {
            return new AppearancePrefs();
        }
    }

    public static void SavePrefs(AppearancePrefs prefs)
    {
        if (AppConfigurationService.Instance is not null)
            return;

        var folder = Path.GetDirectoryName(PrefsPath)!;
        Directory.CreateDirectory(folder);
        File.WriteAllText(PrefsPath, JsonSerializer.Serialize(prefs));
        ApplyAccent(prefs.AccentColorHex);
    }

    public static void ApplyAccent(string hex)
    {
        if (System.Windows.Application.Current is null || string.IsNullOrWhiteSpace(hex))
            return;

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex)!;
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            System.Windows.Application.Current.Resources["SbmsAccentGreenBrush"] = brush;
            System.Windows.Application.Current.Resources["SbmsSidebarActiveBrush"] = brush;

            var light = Lighten(color, 0.22);
            var lightBrush = new SolidColorBrush(light);
            lightBrush.Freeze();
            System.Windows.Application.Current.Resources["SbmsAccentGreenLightBrush"] = lightBrush;
        }
        catch
        {
            // Ignore invalid color values.
        }
    }

    private static Color Lighten(Color color, double amount)
    {
        byte Blend(byte channel) => (byte)Math.Min(255, channel + (255 - channel) * amount);
        return Color.FromRgb(Blend(color.R), Blend(color.G), Blend(color.B));
    }

    public sealed class AppearancePrefs
    {
        public string AccentColorHex { get; set; } = "#2D6A4F";
        public bool CompactTables { get; set; }
        public bool ShowKpiSparklines { get; set; } = true;
    }
}
