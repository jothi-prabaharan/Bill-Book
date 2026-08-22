using Purchase.Entity.Enums;
using Shared.Kernel.Documents;

namespace Purchase.Entity.TableEntities;

/// <summary>
/// A purchase order — <c>POR</c>. What was asked of a vendor.
///
/// <b>It posts nothing and it touches no stock — not even a reservation.</b>
/// This is the first of the five ways purchase is not a mirror of sales, and the
/// one a copied service gets wrong: a sales order reserves stock because the
/// goods are on the shelf and can be promised twice. A purchase order has
/// nothing to hold back. The goods are not there; that is the point of ordering
/// them.
/// </summary>
public class PurchaseOrder : DocumentHeaderBase
{
    public long PurchaseOrderId { get; set; }

    /// <summary>When the vendor is expected to deliver.</summary>
    public DateOnly? ExpectedDate { get; set; }

    /// <summary>
    /// How far it has been received. Its own column rather than arithmetic over
    /// the receipts, because an order closed short by agreement and an order
    /// fully received are different facts that produce the same sums.
    /// </summary>
    public PurchaseFulfilmentStatus FulfilmentStatus { get; set; } =
        PurchaseFulfilmentStatus.Open;

    public List<PurchaseOrderDetail> Lines { get; set; } = [];
}
