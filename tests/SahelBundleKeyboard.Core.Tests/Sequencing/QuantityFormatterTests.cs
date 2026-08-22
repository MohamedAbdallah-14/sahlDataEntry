using SahelBundleKeyboard.Core.Sequencing;

namespace SahelBundleKeyboard.Core.Tests.Sequencing;

public class QuantityFormatterTests
{
    [Theory]
    [InlineData("2", 2)]
    [InlineData("1.5", 1.5)]
    [InlineData("0.125", 0.125)]
    public void Format_ProducesInvariantDotDecimal(string expected, decimal value)
    {
        Assert.Equal(expected, QuantityFormatter.Format(value));
    }

    [Fact]
    public void Format_RemovesUnnecessaryTrailingZeros()
    {
        Assert.Equal("2", QuantityFormatter.Format(2.000m));
        Assert.Equal("1.25", QuantityFormatter.Format(1.2500m));
        Assert.Equal("10.5", QuantityFormatter.Format(10.500m));
        Assert.Equal("3.14", QuantityFormatter.Format(3.14000m));
    }

    [Fact]
    public void Format_IntegerKeepsNoDecimalPoint()
    {
        var formatted = QuantityFormatter.Format(150m);
        Assert.DoesNotContain(".", formatted);
        Assert.Equal("150", formatted);
    }

    [Fact]
    public void Format_UsesDotNeverComma()
    {
        var formatted = QuantityFormatter.Format(7.25m);
        Assert.Contains(".", formatted);
        Assert.DoesNotContain(",", formatted);
    }

    [Fact]
    public void Format_NeverUsesScientificNotation()
    {
        var tiny = QuantityFormatter.Format(0.000000000001m);
        Assert.Equal("0.000000000001", tiny);
        Assert.DoesNotContain("E", tiny, StringComparison.OrdinalIgnoreCase);

        var large = QuantityFormatter.Format(99999999999999999999m);
        Assert.Equal("99999999999999999999", large);
    }

    [Theory]
    [InlineData(true, "1.5", 1.5)]
    [InlineData(true, "12", 12)]
    [InlineData(true, "٠٫٥", 0.5)]
    [InlineData(true, "٣", 3)]
    [InlineData(false, "", 0)]
    [InlineData(false, null, 0)]
    [InlineData(false, "abc", 0)]
    [InlineData(false, "1,5", 0)]
    [InlineData(false, "1 2", 0)]
    public void TryParse_AcceptsOnlyPlainNumbers(bool expectedOk, string? input, decimal expectedValue)
    {
        var ok = QuantityFormatter.TryParse(input, out var value);
        Assert.Equal(expectedOk, ok);
        if (expectedOk)
        {
            Assert.Equal(expectedValue, value);
        }
    }

    [Fact]
    public void TryParse_RejectsCommaToNeverGuessDecimalOrThousands()
    {
        Assert.False(QuantityFormatter.TryParse("1,500", out _));
    }

    [Fact]
    public void NormalizeDigits_ConvertsArabicIndicDigits()
    {
        Assert.Equal("1234567890", QuantityFormatter.NormalizeDigits("١٢٣٤٥٦٧٨٩٠"));
        Assert.Equal("123", QuantityFormatter.NormalizeDigits("۱۲۳"));
    }
}
