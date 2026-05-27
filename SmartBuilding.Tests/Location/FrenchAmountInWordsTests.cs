using SmartBuilding.Desktop.WPF.Services;
using Xunit;

namespace SmartBuilding.Tests.Location;

public class FrenchAmountInWordsTests
{
    [Theory]
    [InlineData(1_400_000, "francs congolais")]
    [InlineData(1, "franc congolais")]
    public void ToFrancsCongolais_ContainsCurrency(long amount, string currencyWord)
    {
        var text = FrenchAmountInWords.ToFrancsCongolais(amount);
        Assert.Contains(currencyWord, text, StringComparison.OrdinalIgnoreCase);
    }
}
