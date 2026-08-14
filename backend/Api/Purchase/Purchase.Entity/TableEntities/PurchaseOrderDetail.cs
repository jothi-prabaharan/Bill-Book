using Shared.Kernel.Documents;

namespace Purchase.Entity.TableEntities;

/// <summary>
/// One line of a purchase order.
///
/// The two running quantities are what make partial receipt possible, and they
/// are separate because the two events are separate: goods can arrive without a
/// bill, and a bill can arrive without goods. An order for ten can be received
/// six and billed four, and all three figures have to survive the gaps.
/// </summary>
public class PurchaseOrderDetail : DocumentLineBase
{
    public long PurchaseOrderDetailId { get; set; }

    public long PurchaseOrderId { get; set; }

    /// <summary>How much has arrived, across every goods receipt against this line.</summary>
    public decimal ReceivedQuantity { get; set; }

    /// <summary>How much has been billed, across every bill against this line.</summary>
    public decimal BilledQuantity { get; set; }

    public List<PurchaseOrderDetailTax> Taxes { get; set; } = [];
}
