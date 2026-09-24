using Shared.Kernel.Validation;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// A blank optional phone is stored as NULL, never as an empty string (D-04,
/// TK-21). The normaliser trims and nothing more: the leading <c>+</c> that marks
/// a foreign number is kept, and a local number gains no prefix.
/// </summary>
public class PhoneNumbersTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void A_blank_phone_is_null(string? value)
    {
        Assert.Null(PhoneNumbers.NormalizeOptional(value));
    }

    [Theory]
    [InlineData("+919876543210", "+919876543210")]
    [InlineData("  +442071234567 ", "+442071234567")]
    public void A_foreign_number_keeps_its_plus(string value, string expected)
    {
        Assert.Equal(expected, PhoneNumbers.NormalizeOptional(value));
    }

    [Theory]
    [InlineData("9876543210", "9876543210")]
    [InlineData(" 044 24312345  ", "044 24312345")]
    public void A_local_number_is_trimmed_and_gains_no_prefix(string value, string expected)
    {
        Assert.Equal(expected, PhoneNumbers.NormalizeOptional(value));
    }
}
