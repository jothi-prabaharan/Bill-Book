using Shared.Kernel.Validation;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// Phone validation is where a well-meaning regex quietly refuses a real
/// customer. The rule this codebase settled on is in CLAUDE.md: a local number
/// is stored without a country prefix, a foreign one with a leading <c>+</c>,
/// and the <c>+</c> is the discriminator. A pattern that forgets it rejects
/// every overseas number.
/// </summary>
public class PhoneAttributeTests
{
    private static bool Landline(object? value) => new LandlineAttribute().IsValid(value);

    private static bool Mobile(object? value) => new MobileAttribute().IsValid(value);

    [Theory]
    [InlineData("044 24312345")]   // Chennai, spaced
    [InlineData("044-24312345")]   // Chennai, hyphenated
    [InlineData("04424312345")]    // Chennai, run together
    [InlineData("11 23456789")]    // Delhi, the shortest STD code
    [InlineData("+442071234567")]  // London, foreign, so it carries the +
    public void Landline_AcceptsRealNumbers(string value)
    {
        Assert.True(Landline(value));
    }

    [Theory]
    [InlineData("1 234567")]       // one-digit STD code: no such thing
    [InlineData("044 12")]         // number too short to be one
    [InlineData("12")]             // not enough digits to be either part
    [InlineData("044 2431 2345")]  // only one separator is allowed
    [InlineData("044/24312345")]   // slash is not a separator
    [InlineData("not a number")]
    public void Landline_RejectsMalformedNumbers(string value)
    {
        Assert.False(Landline(value));
    }

    [Fact]
    public void Landline_TreatsAnEmptyValueAsValid()
    {
        // Optional-field behaviour, inherited from RegularExpressionAttribute.
        // Whether the field is required is [Required]'s business; a validator
        // that also enforced presence would make every optional phone mandatory.
        Assert.True(Landline(null));
        Assert.True(Landline(string.Empty));
    }

    [Theory]
    [InlineData("9840012345")]      // Indian mobile
    [InlineData("+6591234567")]     // Singapore, foreign
    [InlineData("+1 415 555 0123")] // US, spaced, foreign
    public void Mobile_AcceptsAnyReasonableFormat(string value)
    {
        // No pattern on purpose. Mobile lengths and groupings vary enough by
        // country that a regex is a source of false rejections, and a rejected
        // customer is a worse outcome than a badly formatted one.
        Assert.True(Mobile(value));
    }

    [Fact]
    public void Mobile_AcceptsExactlyTheMaximumLength()
    {
        Assert.True(Mobile(new string('9', MobileAttribute.MaxLength)));
    }

    [Fact]
    public void Mobile_RejectsOneCharacterTooMany()
    {
        Assert.False(Mobile(new string('9', MobileAttribute.MaxLength + 1)));
    }

    [Fact]
    public void Mobile_TreatsAnEmptyValueAsValid()
    {
        Assert.True(Mobile(null));
        Assert.True(Mobile(string.Empty));
    }
}
