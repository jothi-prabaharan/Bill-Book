using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Documents;

namespace Sales.Entity.TableEntities;

/// <summary>
/// An invoice — <c>INV</c> — or a POS sale — <c>POS</c>. The document the product
/// is bought for, and the first one where accounting, stock, tax and numbering
/// all run at once.
///
/// <b>A POS sale is a row in this table, not a table of its own.</b> It is the
/// same document rung up on a different screen: same legs, same stock issue, same
/// GST. Two tables for one document means two places to fix a GST bug, and the
/// one that gets fixed is whichever the person happened to be looking at.
/// <see cref="DocumentHeaderBase.TransactionTypeCode"/> holds which it is, and
/// the POS columns below are null on an ordinary invoice.
/// </summary>
public class Invoice : DocumentHeaderBase
{
    public long InvoiceId { get; set; }

    // ---- Where it came from. Each a real foreign key, which is the main gain
    // from a table per document type.

    public long? QuoteId { get; set; }

    public long? SalesOrderId { get; set; }

    public long? DeliveryChallanId { get; set; }

    /// <summary>From <c>acc.PaymentTerms</c>. Unenforced id — Accounting owns it.</summary>
    public long? PaymentTermId { get; set; }

    /// <summary>Derived from the payment term at post. Required on an <c>INV</c>; a POS sale is paid.</summary>
    public DateOnly? DueDate { get; set; }

    // ---- POS only. Null on an INV, and required on a POS row.

    /// <summary>Which till rang it up.</summary>
    public long? TillId { get; set; }

    /// <summary>Who was on the till. A plain Guid — users live in the master database.</summary>
    public Guid? CashierUserId { get; set; }

    [MaxLength(20, ErrorMessage = "Payment mode cannot exceed 20 characters.")]
    public string? PaymentMode { get; set; }

    /// <summary>What the customer handed over.</summary>
    public decimal? TenderedAmount { get; set; }

    /// <summary>What was given back. Kept because a till reconciles against it at close.</summary>
    public decimal? ChangeAmount { get; set; }
}
