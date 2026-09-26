using Master.Api.Services;
using Xunit;

namespace Master.Api.Tests;

/// <summary>The branch's line discount limit (D-29, TK-102), as it is read from configuration.</summary>
public sealed class DiscountLimitSettingTests
{
    [Theory]
    [InlineData("15", 15)]
    [InlineData("12.5", 12.5)]
    [InlineData("0", 0)]
    [InlineData("100", 100)]
    public void A_percentage_from_0_to_100_is_the_limit(string value, decimal expected) =>
        Assert.Equal(expected, ConfigurationDiscountLimitSetting.Parse(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("150")]
    public void Anything_else_reads_as_no_limit(string? value) =>
        Assert.Equal(100m, ConfigurationDiscountLimitSetting.Parse(value));
}
