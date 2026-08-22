using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Documents;

namespace Purchase.Entity.TableEntities;

/// <summary>
/// A vendor bill — <c>BIL</c>. What is owed, and the document input tax credit is
/// claimed on.
///
/// <b>The most common entry point in the module.</b> A bill keyed directly, with
/// no order and no receipt behind it, is the ordinary case — a service, a
/// utility, a trader who never raises paperwork. Both links below are nullable
/// for that reason, and the posting differs accordingly: against a receipt it
/// clears GRNI and moves no stock, because the stock already moved; with no
/// receipt it moves the stock itself.
/// </summary>
public class Bill : DocumentHeaderBase
{
    public long BillId { get; set; }

    // ---- Where it came from. Each a real foreign key, which is the main gain
    // from a table per document type.

    public long? PurchaseOrderId { get; set; }

    public long? GoodsReceiptId { get; set; }

    /// <summary>
    /// <b>The vendor's number, and the column with no sales equivalent.</b>
    ///
    /// On a sale we issue the number. On a purchase the vendor does, and input
    /// tax credit is claimed against <i>theirs</i> — GSTR-2B reconciles on it. So
    /// a posted bill carries two numbers meaning different things:
    /// <c>DocumentNo</c> is ours, for internal reference; this is theirs, and it
    /// is statutory.
    ///
    /// Required, and unique per vendor per financial year. One vendor cannot bill
    /// the same number twice in a year, and catching that at entry is what stops
    /// a duplicate ITC claim — which is a demand notice rather than a typo.
    /// </summary>
    [Required(ErrorMessage = "The vendor's bill number is required.")]
    [MaxLength(50, ErrorMessage = "Vendor bill number cannot exceed 50 characters.")]
    public string VendorBillNo { get; set; } = null!;

    /// <summary>
    /// The date the vendor dated their bill. Required, and not the same as
    /// <c>DocumentDate</c> — the tax period the credit falls in is theirs.
    /// </summary>
    public DateOnly VendorBillDate { get; set; }

    /// <summary>
    /// The Indian financial year <see cref="VendorBillDate"/> falls in, named by
    /// its starting calendar year — April 2026 to March 2027 is 2026.
    ///
    /// <b>A stored computed column, written by Postgres, never by C#.</b> It
    /// exists only to be the fourth column of the unique index above: a vendor
    /// resets their numbering each April, so the same number legitimately recurs
    /// in a later year and an index without this would refuse the second one.
    /// Computing it in C# would put the rule that decides a uniqueness
    /// constraint outside the constraint, where a bulk insert or a direct write
    /// could disagree with it.
    ///
    /// <b>April is hardcoded here</b>, unlike <c>Numbering:FinancialYearStartMonth</c>
    /// which is configurable. That is deliberate rather than overlooked: this
    /// column exists for input tax credit, and the GST year is statutorily
    /// April–March. A branch that keeps its books on a different year does not
    /// get a different GST year.
    /// </summary>
    public int VendorBillFinancialYear { get; set; }

    /// <summary>From <c>acc.PaymentTerms</c>. Unenforced id — Accounting owns it.</summary>
    public long? PaymentTermId { get; set; }

    /// <summary>
    /// Derived from the payment term at post, and <b>required</b> — every bill is
    /// payable on a date, and payables aging is only as good as this column.
    /// </summary>
    public DateOnly DueDate { get; set; }

    /// <summary>
    /// Freight, insurance and duty to be spread across the lines, raising what
    /// the stock actually cost.
    ///
    /// The header holds the total and <c>BillDetail.ApportionedLandedCost</c>
    /// holds each line's share. <b>How it is spread — by value, weight or
    /// quantity — is not yet decided</b>, so nothing computes this yet; the
    /// columns exist so the decision does not need a migration behind it.
    /// </summary>
    public decimal LandedCostAmount { get; set; }

    public List<BillDetail> Lines { get; set; } = [];
}
