using System.Globalization;

namespace SmartBuilding.Shared.Money;

/// <summary>Formatage monétaire partagé (desktop, rapports infra) — USD par défaut.</summary>
public static class BuildingMoneyFormat
{
    public static string Format(decimal amountInCdf, string? currencyCode = null, decimal usdExchangeRate = 2850m)
    {
        var (value, suffix) = ConvertFromCdf(amountInCdf, currencyCode ?? "USD", usdExchangeRate);
        return string.Format(CultureInfo.GetCultureInfo("fr-FR"), "{0:N0} {1}", value, suffix);
    }

    public static (decimal DisplayValue, string Suffix) ConvertFromCdf(
        decimal amountInCdf,
        string currency,
        decimal usdExchangeRate)
    {
        if (string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase) && usdExchangeRate > 0)
            return (amountInCdf / usdExchangeRate, "USD");

        return currency.ToUpperInvariant() switch
        {
            "EUR" => (amountInCdf, "EUR"),
            "XAF" => (amountInCdf, "XAF"),
            "CDF" => (amountInCdf, "CDF"),
            "USD" => (amountInCdf, "USD"),
            _ => (amountInCdf, "USD")
        };
    }
}
