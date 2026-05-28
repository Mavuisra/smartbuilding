using System.Globalization;

namespace SmartBuilding.Shared.Money;

/// <summary>Formatage monétaire — pas de conversion : le montant affiché = le montant en base, suffixe USD par défaut.</summary>
public static class BuildingMoneyFormat
{
    public static string Format(decimal amount, string? currencyCode = null, decimal _ = 0)
    {
        var (value, suffix) = ToDisplay(amount, currencyCode ?? "USD");
        return string.Format(CultureInfo.GetCultureInfo("fr-FR"), "{0:N0} {1}", value, suffix);
    }

    public static (decimal DisplayValue, string Suffix) ToDisplay(decimal amount, string currency)
    {
        var code = NormalizeCode(currency);

        return code switch
        {
            "EUR" => (amount, "EUR"),
            "XAF" => (amount, "XAF"),
            "CDF" => (amount, "USD"),
            "FC" => (amount, "USD"),
            _ => (amount, "USD")
        };
    }

    public static string NormalizeCode(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            return "USD";

        var value = currency.Trim().ToUpperInvariant();
        return value switch
        {
            "CDF" or "FC" => "USD",
            _ => value
        };
    }

    [Obsolete("Utiliser ToDisplay — aucune conversion.")]
    public static (decimal DisplayValue, string Suffix) ConvertFromCdf(decimal amount, string currency, decimal usdExchangeRate) =>
        ToDisplay(amount, currency);
}
