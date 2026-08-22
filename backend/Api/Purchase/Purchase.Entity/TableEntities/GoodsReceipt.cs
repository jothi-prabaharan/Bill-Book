using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Documents;

namespace Purchase.Entity.TableEntities;

/// <summary>
/// A goods receipt — <c>GRN</c>. The goods arrived.
///
/// <b>This is where stock moves on the purchase side</b>, and it is the second
/// way purchase is not a mirror of sales. A sale moves stock on the delivery
/// challan, which follows the order and precedes nothing in particular. A
/// purchase moves stock at the receipt, which usually arrives <i>before</i> the
/// bill — goods on the shelf and the invoice in the post is the ordinary case,
/// not an edge one.
///
/// It posts <c>Dr Inventory / Cr Goods Received Not Invoiced</c>. GRNI is a
/// clearing account: this document opens the obligation and the bill closes it,
/// so a balance sitting in it is goods held and not yet billed.
/// </summary>
public class GoodsReceipt : DocumentHeaderBase
{
    public long GoodsReceiptId { get; set; }

    /// <summary>The order this came against, when there was one. A real foreign key.</summary>
    public long? PurchaseOrderId { get; set; }

    /// <summary>
    /// The vendor's own delivery note number, as printed on the paperwork that
    /// came with the goods. Theirs, not ours — <c>DocumentNo</c> is ours — and
    /// kept because a storekeeper reconciles against the docket in their hand.
    /// </summary>
    [MaxLength(50, ErrorMessage = "Vendor delivery note number cannot exceed 50 characters.")]
    public string? VendorDeliveryNoteNo { get; set; }

    public DateOnly? VendorDeliveryNoteDate { get; set; }

    /// <summary>Who took delivery. A plain Guid — users live in the master database.</summary>
    public Guid? ReceivedBy { get; set; }

    public List<GoodsReceiptDetail> Lines { get; set; } = [];
}
