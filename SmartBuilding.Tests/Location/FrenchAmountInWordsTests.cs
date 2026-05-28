using SmartBuilding.Desktop.WPF.Services;
using Xunit;

namespace SmartBuilding.Tests.Location;

public class FrenchAmountInWordsTests
{
    [Theory]
    [InlineData(1_400_000, "dollars américains")]
    [InlineData(1, "dollar américain")]
    public void ToDollarsUs_ContainsCurrency(long amount, string currencyWord)
    {
        var text = FrenchAmountInWords.ToDollarsUs(amount);
        Assert.Contains(currencyWord, text, StringComparison.OrdinalIgnoreCase);
    }
}
