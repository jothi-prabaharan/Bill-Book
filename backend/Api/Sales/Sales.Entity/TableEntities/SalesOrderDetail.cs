using Shared.Kernel.Documents;

namespace Sales.Entity.TableEntities;

/// <summary>
/// One line of a sales order.
///
/// The two extra quantities are what make partial fulfilment possible: an order
/// can reserve ten and deliver four, and both figures have to survive the gap
/// between those two events.
/// </summary>
public class SalesOrderDetail : DocumentLineBase
{
    public long SalesOrderDetailId { get; set; }

    public long SalesOrderId { get; set; }

    /// <summary>
    /// How much of this line Inventory is currently holding back.
    ///
    /// Kept here as well as in Inventory because releasing has to be exact: a
    /// partial delivery releases and issues only what shipped, and converting to
    /// an invoice is release-then-issue in one transaction — issue first and the
    /// order's own reservation is counted against it, so the sale is refused for
    /// stock it is holding itself.
    /// </summary>
    public decimal ReservedQuantity { get; set; }

    /// <summary>How much has actually gone out, across every challan and invoice against this line.</summary>
    public decimal DeliveredQuantity { get; set; }
}
