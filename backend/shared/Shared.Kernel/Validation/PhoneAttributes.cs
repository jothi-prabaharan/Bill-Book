using System.ComponentModel.DataAnnotations;

namespace Shared.Kernel.Validation;

/// <summary>
/// Landline: an STD code of two or more digits, then a number of three or more,
/// optionally separated by a space or hyphen. A leading + is allowed because a
/// foreign number is stored with one — CLAUDE.md makes the + the discriminator
/// between local and foreign, so a pattern without it rejects every overseas
/// landline.
/// </summary>
public sealed class LandlineAttribute : RegularExpressionAttribute
{
    /// <summary>
    /// The regex, exposed for tests. <c>new</c> because
    /// <see cref="RegularExpressionAttribute.Pattern"/> is an instance property
    /// of the same name — hiding it is intended, and the base value is the same
    /// string, since it is what the constructor passes up.
    /// </summary>
    public new const string Pattern = @"^\+?\d{2,}[\s\-]?\d{3,}$";

    public LandlineAttribute()
        : base(Pattern) =>
        ErrorMessage = "Phone number needs an STD code then the number.";
}

/// <summary>
/// Mobile numbers carry no pattern on purpose: lengths vary too much by country
/// for a regex to be anything but a source of false rejections. Length is the
/// only constraint, and the leading + still marks a foreign number.
/// </summary>
public sealed class MobileAttribute : ValidationAttribute
{
    public const int MaxLength = 20;

    public MobileAttribute() =>
        ErrorMessage = $"Mobile number cannot exceed {MaxLength} characters.";

    public override bool IsValid(object? value) =>
        value is not string text || text.Length <= MaxLength;
}
