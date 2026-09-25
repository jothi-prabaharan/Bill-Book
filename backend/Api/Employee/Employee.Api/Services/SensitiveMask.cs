namespace Employee.Api.Services;

/// <summary>
/// Masks the numbers an employee list must not show (TK-48): PAN, Aadhaar and
/// bank account numbers keep their last four characters and hide the rest.
/// </summary>
public static class SensitiveMask
{
    public const char MaskChar = 'X';

    public static string? Mask(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        int shown = Math.Min(4, value.Length / 2);
        return new string(MaskChar, value.Length - shown) + value[^shown..];
    }

    /// <summary>
    /// The value to store: what was sent, unless what was sent is the mask of
    /// what is stored, which means the caller saw it masked and did not change it.
    /// </summary>
    public static string? Resolve(string? sent, string? stored) =>
        sent is not null && stored is not null && sent == Mask(stored) ? stored : sent;
}
