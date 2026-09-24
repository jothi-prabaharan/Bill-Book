using Master.Entity.Enums;

namespace Master.Api.Services;

/// <summary>
/// What belongs to one trade only (D-10, TK-30). The list is also in
/// <c>docs/Modules.md</c>, "A branch's trade"; the two change together.
///
/// <b>Anything not listed is every trade's.</b> General is the everything
/// branch and is shown everything. The asymmetry is deliberate: a jeweller
/// without purities cannot price an ornament, while a chemist with them only
/// has an unused screen.
///
/// <b>A trade hides, it never deletes.</b> A branch that changes trade keeps
/// every row it has; the other trade's screens are dropped from its menu, and
/// the new trade's seeds are added by the idempotent seed.
/// </summary>
public static class TradeScope
{
    private static readonly Vertical[] GeneralAndJewellery = [Vertical.General, Vertical.Jewellery];

    /// <summary>Menu codes shown to some trades only, and which.</summary>
    public static readonly IReadOnlyDictionary<string, Vertical[]> MenuTrades =
        new Dictionary<string, Vertical[]>(StringComparer.OrdinalIgnoreCase)
        {
            // Settings › Metal purity: the jewellery trade's karat and fineness table.
            ["mtp"] = GeneralAndJewellery,
        };

    public static bool ShowsMenu(string code, Vertical trade) =>
        !MenuTrades.TryGetValue(code, out Vertical[]? trades) || trades.Contains(trade);
}
