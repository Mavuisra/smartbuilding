namespace SmartBuilding.Desktop.WPF.Services;

/// <summary>Conversion montant → texte français (francs congolais).</summary>
public static class FrenchAmountInWords
{
    private static readonly string[] Units =
    [
        "zéro", "un", "deux", "trois", "quatre", "cinq", "six", "sept", "huit", "neuf",
        "dix", "onze", "douze", "treize", "quatorze", "quinze", "seize",
        "dix-sept", "dix-huit", "dix-neuf"
    ];

    private static readonly string[] Tens =
    [
        "", "", "vingt", "trente", "quarante", "cinquante", "soixante", "soixante", "quatre-vingt", "quatre-vingt"
    ];

    public static string ToFrancsCongolais(decimal amount)
    {
        var value = (long)Math.Round(amount, 0, MidpointRounding.AwayFromZero);
        if (value == 0)
            return "zéro franc congolais";

        var words = Convert(value);
        var currency = value > 1 ? "francs congolais" : "franc congolais";
        return $"{char.ToUpper(words[0])}{words[1..]} {currency}";
    }

    private static string Convert(long n)
    {
        if (n < 0)
            return "moins " + Convert(-n);
        if (n < 20)
            return Units[n];
        if (n < 100)
        {
            var ten = n / 10;
            var unit = n % 10;
            if (ten == 7 || ten == 9)
            {
                var baseTen = ten == 7 ? "soixante" : "quatre-vingt";
                return unit == 0 ? (ten == 9 ? "quatre-vingts" : "soixante-dix") : $"{baseTen}-{Convert(10 + unit)}";
            }
            if (unit == 0)
                return Tens[ten] + (ten == 8 ? "s" : "");
            if (unit == 1 && ten != 8)
                return $"{Tens[ten]}-et-un";
            return $"{Tens[ten]}-{Units[unit]}";
        }
        if (n < 1000)
        {
            var hundred = n / 100;
            var rest = n % 100;
            var head = hundred == 1 ? "cent" : $"{Units[hundred]} cent";
            if (rest == 0 && hundred > 1)
                head += "s";
            return rest == 0 ? head : $"{head} {Convert(rest)}";
        }
        if (n < 1_000_000)
        {
            var thousand = n / 1000;
            var rest = n % 1000;
            var head = thousand == 1 ? "mille" : $"{Convert(thousand)} mille";
            return rest == 0 ? head : $"{head} {Convert(rest)}";
        }

        var million = n / 1_000_000;
        var restM = n % 1_000_000;
        var millionWord = million == 1 ? "un million" : $"{Convert(million)} millions";
        return restM == 0 ? millionWord : $"{millionWord} {Convert(restM)}";
    }
}
