namespace Master.Entity.Models;

/// <summary>
/// Everything a screen needs to render a date, a quantity and an amount the way
/// this branch expects them.
///
/// <b>Composed, not stored.</b> There is no format table and there should not
/// be one: the currency half already lives on <c>mst.Currency</c> — symbol,
/// position, decimals and the grouping mask that separates Indian
/// <c>##,##,##0.00</c> from Western <c>###,###,##0.00</c> — and the decimal
/// counts already live in <c>mst.Configuration</c> as
/// <c>unitPrice.decimals</c> and <c>quantity.decimals</c>. Copying either into
/// a new key would leave two places to change one answer and no rule for which
/// wins. Only <see cref="DatePattern"/> was genuinely missing, because a date
/// pattern belongs to the branch rather than to a currency.
///
/// The frontend reads this instead of hardcoding, which is the whole point: on
/// 4 September the one screen that needed a date format carried the string
/// <c>'dd/MM/yyyy'</c> in its own source.
/// </summary>
public sealed class FormatSettingsDto
{
    /// <summary>
    /// Display pattern for dates, from the <c>format.date</c> configuration key
    /// — the org's override when it has one, else the shipped default.
    /// </summary>
    public string DatePattern { get; set; } = "dd/MM/yyyy";

    /// <summary>ISO 4217 code of the branch's base currency, e.g. <c>INR</c>.</summary>
    public string CurrencyCode { get; set; } = "INR";

    /// <summary>The symbol to draw, e.g. <c>₹</c>.</summary>
    public string CurrencySymbol { get; set; } = "₹";

    /// <summary>
    /// <c>Prefix</c> or <c>Suffix</c> — which side of the number the symbol
    /// goes. Sent as the enum's name rather than its number so the payload
    /// survives someone renumbering the enum.
    /// </summary>
    public string SymbolPosition { get; set; } = "Prefix";

    /// <summary>
    /// The grouping mask, e.g. <c>##,##,##0.00</c>. This is what tells a
    /// renderer to group Indian-style rather than in thousands.
    /// </summary>
    public string CurrencyMask { get; set; } = "##,##,##0.00";

    /// <summary>Decimals money is shown and rounded to.</summary>
    public int CurrencyDecimals { get; set; } = 2;

    /// <summary>Decimals for unit prices, from <c>unitPrice.decimals</c>.</summary>
    public int UnitPriceDecimals { get; set; } = 2;

    /// <summary>Decimals for quantities, from <c>quantity.decimals</c>.</summary>
    public int QuantityDecimals { get; set; } = 2;
}
