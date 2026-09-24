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
