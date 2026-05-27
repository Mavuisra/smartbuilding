using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

/// <summary>
/// Formatage monétaire global — montants stockés en CDF, affichage selon la devise des paramètres.
/// </summary>
public static class MoneyFormatter
{
    public static string Format(decimal amountInCdf) =>
        (AppConfigurationService.Instance?.Current ?? AppConfiguration.Default).FormatMoney(amountInCdf);

    public static string CurrencyCode =>
        AppConfigurationService.Instance?.Current.Currency ?? "CDF";

    public static bool RequiresUsdRate =>
        string.Equals(CurrencyCode, "USD", StringComparison.OrdinalIgnoreCase);

    public static bool HasValidUsdRate =>
        (AppConfigurationService.Instance?.Current.UsdExchangeRate ?? 0) > 0;
}
