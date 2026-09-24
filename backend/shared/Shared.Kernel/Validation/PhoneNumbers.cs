namespace Shared.Kernel.Validation;

/// <summary>
/// How an optional phone number is stored (D-04, TK-21).
/// </summary>
public static class PhoneNumbers
{
    /// <summary>
    /// Trimmed, and <c>null</c> when blank. A blank optional phone is NULL in
    /// every schema, never an empty string: a column holding both says "no
    /// number" two ways, and every query, report and SMS job has to remember to
    /// check for both.
    ///
    /// Nothing else changes. A leading <c>+</c> is kept, because CLAUDE.md makes
    /// it the discriminator between a local number (stored without a prefix) and
    /// a foreign one; the separators inside a landline are the
    /// <see cref="LandlineAttribute"/>'s business, not this.
    /// </summary>
    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
