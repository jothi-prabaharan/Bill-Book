using System.Text.RegularExpressions;

namespace Shared.Kernel.Tax;

/// <summary>
/// The shape and check character of a GSTIN (TK-91).
///
/// Fifteen characters: the two-digit state code, the holder's PAN, the entity
/// number within that PAN, a <c>Z</c>, and a check character computed over the
/// first fourteen in base 36. The IRP refuses a GSTIN whose check character is
/// wrong, and a refusal costs an attempt, so it is checked here first.
/// </summary>
public static partial class Gstin
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    [GeneratedRegex("^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$")]
    private static partial Regex Shape();

    /// <summary>True when the value has the GSTIN shape and its check character is right.</summary>
    public static bool IsValid(string? gstin) =>
        gstin is { Length: 15 } && Shape().IsMatch(gstin) && CheckCharacter(gstin) == gstin[14];

    /// <summary>The two-digit state code a GSTIN starts with, or null for a value too short to have one.</summary>
    public static string? StateCodeOf(string? gstin) =>
        gstin is { Length: >= 2 } ? gstin[..2] : null;

    /// <summary>The check character for the first fourteen characters.</summary>
    public static char CheckCharacter(string gstin)
    {
        int sum = 0;
        for (int i = 0; i < 14; i++)
        {
            int product = Alphabet.IndexOf(gstin[i]) * (i % 2 == 0 ? 1 : 2);
            sum += (product / 36) + (product % 36);
        }

        return Alphabet[(36 - (sum % 36)) % 36];
    }
}
