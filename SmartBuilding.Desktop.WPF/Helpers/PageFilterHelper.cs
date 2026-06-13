namespace SmartBuilding.Desktop.WPF.Helpers;

/// <summary>
/// Utilitaires partagés pour les filtres ComboBox des pages listes.
/// WPF remet souvent SelectedItem à null après ItemsSource.Clear().
/// </summary>
public static class PageFilterHelper
{
    public static bool IsAll(string? filter, string allLabel) =>
        string.IsNullOrWhiteSpace(filter)
        || string.Equals(filter, allLabel, StringComparison.Ordinal);

    public static bool Matches(string? filter, string allLabel, string value) =>
        IsAll(filter, allLabel)
        || string.Equals(filter, value, StringComparison.OrdinalIgnoreCase);

    /// <summary>Rétablit une sélection valide après repopulation de ItemsSource.</summary>
    public static string RestoreSelection(string? current, IEnumerable<string> items, string allLabel)
    {
        var list = items as IList<string> ?? items.ToList();
        if (!string.IsNullOrWhiteSpace(current) && list.Contains(current))
            return current;
        return list.Contains(allLabel) ? allLabel : list.FirstOrDefault() ?? allLabel;
    }
}
