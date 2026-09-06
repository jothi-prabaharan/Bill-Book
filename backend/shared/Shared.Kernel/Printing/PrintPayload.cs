using System.Globalization;

namespace Shared.Kernel.Printing;

/// <summary>
/// The data one document offers a template, in the shape the substitution rules
/// name: singles resolve anywhere, list rows resolve inside a repeated row.
///
/// Values are left as their own types — decimal, DateOnly, string — and
/// formatted by the renderer against the placeholder's mask. A payload builder
/// that formatted them itself would be deciding presentation in Sales, which is
/// the thing templates exist to take away from it.
/// </summary>
public sealed class PrintPayload
{
    public Dictionary<string, object?> Singles { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Keyed by collection — Item, Tax, Payment, Alloc, Line.</summary>
    public Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> Lists { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows(string group) =>
        Lists.TryGetValue(group, out var rows) ? rows : [];
}

/// <summary>
/// The branch's currency and the culture to render digits in. Passed in rather
/// than read from the thread, because a document is formatted for the branch
/// that raised it and not for whoever happens to be printing it.
///
/// <b>The default is the invariant culture, and it has to be.</b> This solution
/// sets <c>InvariantGlobalization</c> in Directory.Build.props, so no named
/// culture exists at run time anywhere in the product — asking for "en-IN"
/// throws CultureNotFoundException in production exactly as it does in a test.
/// That is not a limitation here: grouping comes from the mask
/// (<c>##,##,##0.00</c> is Indian, <c>###,###,##0.00</c> Western), which is the
/// same rule the frontend applies, so the answer never depended on a locale in
/// the first place.
/// </summary>
public sealed class PrintFormatContext
{
    public static readonly PrintFormatContext Default = new();

    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;

    public string CurrencySymbol { get; init; } = string.Empty;
}

/// <summary>
/// Applies the catalogue's masks — <c>##,##,##0.00</c>, <c>##,##,##0.0##</c>,
/// <c>DD-MMM-YYYY</c>.
///
/// These are the masks the product already stores on mst.Currency and reads on
/// the frontend, so they are interpreted here rather than translated into .NET
/// format strings somewhere else and kept in step by hand. The grouping is read
/// off the mask itself: <c>##,##,##0</c> is the Indian 3-2-2 grouping and
/// <c>###,###,##0</c> the Western 3-3-3, which is exactly how the frontend
/// tells them apart.
/// </summary>
public static class MaskFormatter
{
    public static string Format(object? value, PlaceholderDefinition placeholder, PrintFormatContext context)
    {
        ArgumentNullException.ThrowIfNull(placeholder);
        ArgumentNullException.ThrowIfNull(context);

        if (value is null)
        {
            return string.Empty;
        }

        return placeholder.Type switch
        {
            PlaceholderType.Amount or PlaceholderType.Number => Number(value, placeholder.Format, context),
            PlaceholderType.Date => Date(value, placeholder.Format, context),
            _ => Convert.ToString(value, context.Culture) ?? string.Empty,
        };
    }

    private static string Number(object value, string? mask, PrintFormatContext context)
    {
        if (!TryDecimal(value, context, out decimal number))
        {
            return Convert.ToString(value, context.Culture) ?? string.Empty;
        }

        mask ??= PlaceholderCatalog.AmountFormat;

        int decimals = Decimals(mask);
        decimal rounded = Math.Round(number, decimals, MidpointRounding.AwayFromZero);

        var format = (NumberFormatInfo)context.Culture.NumberFormat.Clone();
        format.NumberGroupSizes = IsIndianGrouping(mask) ? [3, 2] : [3];
        format.NumberDecimalDigits = decimals;

        string text = rounded.ToString("N", format);

        // "0.0##" means up to three decimals with the optional ones dropped —
        // a quantity of 2 prints as 2, not 2.000.
        if (HasOptionalDecimals(mask) && text.Contains(format.NumberDecimalSeparator, StringComparison.Ordinal))
        {
            text = text.TrimEnd('0').TrimEnd(format.NumberDecimalSeparator.ToCharArray());
        }

        return text;
    }

    private static string Date(object value, string? mask, PrintFormatContext context)
    {
        DateTime moment = value switch
        {
            DateOnly date => date.ToDateTime(TimeOnly.MinValue),
            DateTimeOffset offset => offset.DateTime,
            DateTime dateTime => dateTime,
            string text when DateTime.TryParse(text, context.Culture, DateTimeStyles.None, out DateTime parsed) => parsed,
            _ => default,
        };

        if (moment == default)
        {
            return Convert.ToString(value, context.Culture) ?? string.Empty;
        }

        return moment.ToString(NetDateFormat(mask ?? PlaceholderCatalog.DateFormat), context.Culture);
    }

    /// <summary>
    /// Translates the mask's tokens to .NET's. Longest token first, or the D of
    /// DD would be consumed before DD is recognised.
    /// </summary>
    private static string NetDateFormat(string mask) =>
        mask.Replace("YYYY", "yyyy", StringComparison.Ordinal)
            .Replace("YY", "yy", StringComparison.Ordinal)
            .Replace("DD", "dd", StringComparison.Ordinal)
            .Replace("D", "d", StringComparison.Ordinal);

    /// <summary>A mask grouping two digits above the first three is the Indian lakh/crore grouping.</summary>
    private static bool IsIndianGrouping(string mask) => mask.Contains("##,##,##", StringComparison.Ordinal);

    private static bool HasOptionalDecimals(string mask)
    {
        int dot = mask.IndexOf('.', StringComparison.Ordinal);
        return dot >= 0 && mask[(dot + 1)..].Contains('#', StringComparison.Ordinal);
    }

    private static int Decimals(string mask)
    {
        int dot = mask.IndexOf('.', StringComparison.Ordinal);
        return dot < 0 ? 0 : mask.Length - dot - 1;
    }

    private static bool TryDecimal(object value, PrintFormatContext context, out decimal number)
    {
        switch (value)
        {
            case decimal d: number = d; return true;
            case double dbl: number = (decimal)dbl; return true;
            case float f: number = (decimal)f; return true;
            case int i: number = i; return true;
            case long l: number = l; return true;
            case string s when decimal.TryParse(s, NumberStyles.Any, context.Culture, out decimal parsed):
                number = parsed;
                return true;
            default: number = 0; return false;
        }
    }
}
