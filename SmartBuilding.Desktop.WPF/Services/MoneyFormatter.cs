using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

/// <summary>
/// Formatage monétaire global — pas de conversion : affichage direct en USD (ou devise paramètres).
/// </summary>
public static class MoneyFormatter
{
    public static string Format(decimal amount) =>
        (AppConfigurationService.Instance?.Current ?? AppConfiguration.Default).FormatMoney(amount);

    public static string ZeroDisplay => Format(0);

    public static string CurrencyCode =>
        AppConfigurationService.Instance?.Current.Currency ?? "USD";

    public static string AmountHint => $"Montant ({CurrencyCode})";

    /// <summary>Plus de conversion : le taux n'est plus requis pour l'affichage USD.</summary>
    public static bool RequiresUsdRate => false;

    public static bool HasValidUsdRate => true;
}
