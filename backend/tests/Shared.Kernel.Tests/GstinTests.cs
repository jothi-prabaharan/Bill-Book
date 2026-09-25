using Shared.Kernel.Tax;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>The GSTIN shape and check character (TK-91), against published registrations.</summary>
public sealed class GstinTests
{
    [Theory]
    [InlineData("27AAPFU0939F1ZV")]
    [InlineData("29AAGCB7383J1Z4")]
    [InlineData("33AAACH7409R1Z8")]
    public void A_real_gstin_is_valid(string gstin) => Assert.True(Gstin.IsValid(gstin));

    [Theory]
    [InlineData("27AAPFU0939F1ZW")]
    [InlineData("27AAPFU0939F1Z")]
    [InlineData("27aapfu0939f1zv")]
    [InlineData("2AAAPFU0939F1ZV")]
    [InlineData("27AAPFU0939F0ZV")]
    [InlineData("27AAPFU0939F1YV")]
    [InlineData("")]
    [InlineData(null)]
    public void A_wrong_check_character_or_shape_is_invalid(string? gstin) => Assert.False(Gstin.IsValid(gstin));

    [Fact]
    public void The_state_code_is_the_first_two_digits()
    {
        Assert.Equal("33", Gstin.StateCodeOf("33AAACH7409R1Z8"));
        Assert.Null(Gstin.StateCodeOf("3"));
    }
}
