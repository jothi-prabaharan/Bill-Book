using Master.Repository.Clients;
using Master.Entity.Enums;

namespace Master.Repository.Repositories;

/// <summary>
/// What belongs to one trade only (D-10, TK-30). The list is also in
/// <c>docs/Modules.md</c>, "A branch's trade"; the two change together.
///
/// <b>Anything not listed is every trade's.</b> Retail is the everything
/// branch and is shown everything. The asymmetry is deliberate: a jeweller
/// without purities cannot price an ornament, while a chemist with them only
/// has an unused screen.
///
/// <b>A trade hides, it never deletes.</b> A branch that changes trade keeps
/// every row it has; the other trade's screens are dropped from its menu, and
/// the new trade's seeds are added by the idempotent seed.
/// </summary>
public static class TradeScopeRepository
{
    private static readonly Vertical[] RetailAndJewellery = [Vertical.Retail, Vertical.Jewellery];

    /// <summary>Every trade that holds stock: all but Service (TK-136).</summary>
    private static readonly Vertical[] Stocked =
        [Vertical.Retail, Vertical.Pharma, Vertical.Jewellery, Vertical.School, Vertical.Restaurant];

    /// <summary>
    /// The screens only a business that holds stock needs (TK-136): a Service
    /// branch sells time and work, so warehouses, stock adjustments, goods
    /// receipts, delivery challans and the stock-tracking reports would be
    /// empty screens for it. Items, categories, units and HSN/SAC stay: a
    /// service is still an item, with a SAC code.
    /// </summary>
    public static readonly IReadOnlyList<string> StockOnlyMenus =
    [
        "adj", "whs", "grn", "dlc", "pt-dlc",
        "r-delivery-order-track-report", "r-purchase-receive-order-details", "r-receive-order-track-report",
        "r-inventory-item-summary", "r-inventory-item-detail", "r-inventory-aging-report",
        "r-batch-tracking-status-report", "r-batch-tracking-detail-report",
        "r-serial-tracking-status-report", "r-serial-tracking-detail-report",
        "r-warehouse-tracking-status-report",
    ];

    /// <summary>Menu codes shown to some trades only, and which.</summary>
    public static readonly IReadOnlyDictionary<string, Vertical[]> MenuTrades = BuildMenuTrades();

    private static Dictionary<string, Vertical[]> BuildMenuTrades()
    {
        Dictionary<string, Vertical[]> trades = new(StringComparer.OrdinalIgnoreCase)
        {
            // Settings › Metal purity: the jewellery trade's karat and fineness table.
            ["mtp"] = RetailAndJewellery,
        };

        foreach (string code in StockOnlyMenus)
        {
            trades[code] = Stocked;
        }

        return trades;
    }

    public static bool ShowsMenu(string code, Vertical trade) =>
        !MenuTrades.TryGetValue(code, out Vertical[]? trades) || trades.Contains(trade);
}

