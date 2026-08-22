using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Documents;

namespace Purchase.Entity.TableEntities;

/// <summary>
/// One line of a goods receipt.
///
/// <b>Only <see cref="AcceptedQuantity"/> becomes stock.</b> A delivery can
/// arrive damaged or short, and a rejected carton is not inventory — it is a
/// dispute. Splitting the arrival into accepted and rejected here, rather than
/// recording ten and adjusting two out afterwards, is what keeps the stock
/// ledger from carrying a receipt that never happened.
/// </summary>
public class GoodsReceiptDetail : DocumentLineBase
{
    public long GoodsReceiptDetailId { get; set; }

    public long GoodsReceiptId { get; set; }

    /// <summary>The order line this fulfils, when the receipt came against an order.</summary>
    public long? PurchaseOrderDetailId { get; set; }

    /// <summary>What was taken into stock. This, not <c>Quantity</c>, is what moves.</summary>
    public decimal AcceptedQuantity { get; set; }

    /// <summary>What was refused. Never reaches inventory.</summary>
    public decimal RejectedQuantity { get; set; }

    /// <summary>
    /// Why it was refused. <b>Required when <see cref="RejectedQuantity"/> is
    /// non-zero</b>, enforced by check constraint — a rejection with no reason
    /// is an argument with the vendor that nobody can reconstruct.
    /// </summary>
    [MaxLength(300, ErrorMessage = "Rejection reason cannot exceed 300 characters.")]
    public string? RejectionReason { get; set; }

    public List<GoodsReceiptDetailTax> Taxes { get; set; } = [];
}
