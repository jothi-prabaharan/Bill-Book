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
}
