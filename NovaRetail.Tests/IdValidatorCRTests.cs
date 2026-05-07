using NovaRetail.Services;

namespace NovaRetail.Tests;

public sealed class IdValidatorCRTests
{
    [Theory]
    [InlineData("01", "9", true)]
    [InlineData("01", "A", false)]
    [InlineData("05", "A", true)]
    [InlineData("05", "-", false)]
    public void IsInputCharAllowed_matches_expected_rules(string tipoCodigo, string inputChar, bool expected)
    {
        var result = IdValidatorCR.IsInputCharAllowed(tipoCodigo, inputChar);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("01", "123456789", true)]
    [InlineData("03", "01234567890", false)]
    [InlineData("05", "ABCD1234", true)]
    [InlineData("05", "AB CD", false)]
    [InlineData("06", "123456", true)]
    public void ValidateFinal_applies_expected_costa_rica_id_rules(string tipoCodigo, string id, bool expected)
    {
        var result = IdValidatorCR.ValidateFinal(tipoCodigo, id, out var error);

        Assert.Equal(expected, result);
        Assert.Equal(expected, string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData("01", "123456789", true)]
    [InlineData("01", "123-456", false)]
    [InlineData("05", "ABCD1234", true)]
    [InlineData("05", "AB CD", false)]
    public void IsPasteAllowed_matches_expected_rules(string tipoCodigo, string pastedText, bool expected)
    {
        var result = IdValidatorCR.IsPasteAllowed(tipoCodigo, pastedText);

        Assert.Equal(expected, result);
    }
}
