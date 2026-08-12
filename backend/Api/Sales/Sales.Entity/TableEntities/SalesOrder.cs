using Sales.Entity.Enums;
using Shared.Kernel.Documents;

namespace Sales.Entity.TableEntities;

/// <summary>
/// A sales order — <c>SOR</c>. Committed, but not yet delivered.
///
/// <b>Posts nothing, and is the only thing in the sales flow that reserves
/// stock.</b> Confirming one calls Inventory's <c>ReserveAsync</c>; the goods
/// stay on the shelf and in the valuation, but stop being available to anyone
/// else. Without that, an order confirmed and not yet shipped leaves stock fully
/// available and it can be promised twice.
/// </summary>
public class SalesOrder : DocumentHeaderBase
{
    public long SalesOrderId { get; set; }

    /// <summary>The quote this came from, when one did. A real foreign key.</summary>
    public long? QuoteId { get; set; }

    /// <summary>When the customer is expecting the goods.</summary>
    public DateOnly? DeliveryDate { get; set; }

    /// <summary>
    /// How far it has been delivered. Its own column rather than arithmetic over
    /// the challans, because an order closed short by agreement and an order
    /// fully delivered are different facts that produce the same sums.
    /// </summary>
    public FulfilmentStatus FulfilmentStatus { get; set; } = FulfilmentStatus.Open;
}
