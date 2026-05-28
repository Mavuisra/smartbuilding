namespace SmartBuilding.Desktop.WPF.Services;

/// <summary>
/// Libellés UI et identifiants persistés (fuseau IANA, code devise).
/// </summary>
public static class SettingsLookups
{
    public static readonly IReadOnlyList<string> TimeZoneDisplays =
    [
        "(UTC+01:00) Kinshasa",
        "(UTC+00:00) Londres",
        "(UTC+01:00) Paris",
        "(UTC+02:00) Le Caire"
    ];

    public static readonly IReadOnlyList<string> Currencies =
    [
        "USD - Dollar US",
        "CDF - Franc congolais",
        "EUR - Euro",
        "XAF - Franc CFA"
    ];

    public static readonly IReadOnlyList<string> DateFormats =
    [
        "dd/MM/yyyy",
        "MM/dd/yyyy",
        "yyyy-MM-dd"
    ];

    public static readonly IReadOnlyList<string> Languages =
    [
        "Français",
        "English",
        "Português"
    ];

    public static readonly IReadOnlyList<string> TimeFormats =
    [
        "24 heures",
        "12 heures"
    ];

    private static readonly Dictionary<string, string> TimeZoneIdByDisplay =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["(UTC+01:00) Kinshasa"] = "Africa/Kinshasa",
            ["(UTC+00:00) Londres"] = "Europe/London",
            ["(UTC+01:00) Paris"] = "Europe/Paris",
            ["(UTC+02:00) Le Caire"] = "Africa/Cairo"
        };

    private static readonly Dictionary<string, string> TimeZoneDisplayById =
        TimeZoneIdByDisplay.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> CurrencyDisplayByCode =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["CDF"] = "CDF - Franc congolais",
            ["EUR"] = "EUR - Euro",
            ["USD"] = "USD - Dollar US",
            ["XAF"] = "XAF - Franc CFA"
        };

    public static string ToTimeZoneId(string? displayOrId)
    {
        if (string.IsNullOrWhiteSpace(displayOrId))
            return "Africa/Kinshasa";

        var value = displayOrId.Trim();
        if (TimeZoneIdByDisplay.TryGetValue(value, out var id))
            return id;

        if (TimeZoneDisplayById.ContainsKey(value))
            return value;

        foreach (var (display, zoneId) in TimeZoneIdByDisplay)
        {
            if (value.Contains(zoneId, StringComparison.OrdinalIgnoreCase)
                || value.Contains(display, StringComparison.OrdinalIgnoreCase))
                return zoneId;
        }

        return value;
    }

    public static string ToTimeZoneDisplay(string? idOrDisplay)
    {
        if (string.IsNullOrWhiteSpace(idOrDisplay))
            return TimeZoneDisplays[0];

        var value = idOrDisplay.Trim();
        if (TimeZoneDisplayById.TryGetValue(value, out var display))
            return display;

        if (TimeZoneIdByDisplay.TryGetValue(value, out _))
            return value;

        foreach (var (label, zoneId) in TimeZoneIdByDisplay)
        {
            if (value.Contains(zoneId, StringComparison.OrdinalIgnoreCase)
                || value.Contains(label, StringComparison.OrdinalIgnoreCase))
                return label;
        }

        return TimeZoneDisplays.FirstOrDefault(d => d.Contains(value, StringComparison.OrdinalIgnoreCase))
               ?? TimeZoneDisplays[0];
    }

    public static string ParseCurrencyCode(string? selected)
    {
        if (string.IsNullOrWhiteSpace(selected))
            return "USD";

        var value = selected.Trim();
        var separator = value.IndexOf(" - ", StringComparison.Ordinal);
        return separator > 0 ? value[..separator].Trim() : value;
    }

    public static string ToCurrencyDisplay(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "USD - Dollar US";

        var value = code.Trim();
        if (CurrencyDisplayByCode.TryGetValue(value, out var display))
            return display;

        return Currencies.FirstOrDefault(c =>
                   c.StartsWith(value, StringComparison.OrdinalIgnoreCase))
               ?? Currencies[0];
    }
}
