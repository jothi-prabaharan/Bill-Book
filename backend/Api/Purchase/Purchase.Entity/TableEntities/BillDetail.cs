using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Documents;

namespace Purchase.Entity.TableEntities;

/// <summary>
/// One line of a vendor bill.
///
/// <b>This is where <c>LineType</c> does real work</b>, and it is the fifth way
/// purchase is not a mirror of sales. A sale has one kind of line in practice.
/// A bill has three, and they go to three different places: <c>Stock</c> to
/// inventory or GRNI, <c>Expense</c> to the account named on the line, and
/// <c>Capital</c> to the fixed asset category's account — where it also creates
/// the register row. Nothing else in the product puts an asset on the books.
/// </summary>
public class BillDetail : DocumentLineBase
{
    public long BillDetailId { get; set; }

    public long BillId { get; set; }

    /// <summary>The receipt line being billed, when a receipt preceded this.</summary>
    public long? GoodsReceiptDetailId { get; set; }

    /// <summary>The order line being billed, when an order preceded this.</summary>
    public long? PurchaseOrderDetailId { get; set; }

    /// <summary>
    /// This line's share of the bill's <c>LandedCostAmount</c>. Written when the
    /// apportionment basis is decided; zero until then.
    /// </summary>
    public decimal ApportionedLandedCost { get; set; }

    /// <summary>
    /// How much has gone back to the vendor, across every debit note against this
    /// line. A running column rather than a sum over debit notes, because the
    /// guard that stops a line being returned twice has to hold inside one
    /// transaction.
    /// </summary>
    public decimal ReturnedQuantity { get; set; }

    public List<BillDetailTax> Taxes { get; set; } = [];

    /// <summary>
    /// Whether this line's GST is claimable (TK-115, D-31). A line that is not
    /// posts its tax into its own cost instead of Input GST.
    /// </summary>
    public ItcEligibility ItcEligibility { get; set; } = ItcEligibility.Eligible;

    // ---- Re-billing (TK-106). A cost bought for a client's job, charged on to
    // them with a markup, on an invoice that claims it at post.

    /// <summary>Whether the line is to be charged on to the project's client.</summary>
    public bool IsBillable { get; set; }

    /// <summary>The markup added when it is charged on, as a percentage.</summary>
    [Range(typeof(decimal), "0", "1000", ErrorMessage = "Markup must be between 0 and 1000 percent.")]
    public decimal? MarkupPercent { get; set; }

    /// <summary>The invoice that charged it on, set by the claim. No FK: invoices live in Sales.</summary>
    public long? BilledInvoiceId { get; set; }
}
