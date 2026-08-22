using Sales.Entity.Enums;
using Shared.Kernel.Documents;

namespace Sales.Entity.TableEntities;

/// <summary>
/// A credit note — <c>CRN</c>. The way back out: a sales return, a price
/// correction, a goodwill adjustment.
///
/// <b><see cref="InvoiceId"/> is required.</b> A credit note against nothing is
/// legitimate in the abstract — a return whose sale predates the system — but not
/// here, and deliberately: GST requires a credit note to name the invoice it
/// corrects, and stock returning to the cost layer it came from needs the invoice
/// line to find that layer. A return with no invoice behind it is handled as an
/// opening-balance adjustment instead, which is what it actually is.
///
/// It debits <b>Sales Returns</b> — a contra Income account, so the report
/// subtracts it rather than adding a negative — and credits Accounts Receivable.
/// </summary>
public class CreditNote : DocumentHeaderBase
{
    public long CreditNoteId { get; set; }

    /// <summary>The invoice being corrected. Required, and a real foreign key.</summary>
    public long InvoiceId { get; set; }

    /// <summary>
    /// What is being corrected. On the document because GSTR-1 asks for it, not
    /// because a report wanted a grouping — and only
    /// <see cref="CreditNoteReason.SalesReturn"/> moves stock.
    /// </summary>
    public CreditNoteReason ReasonCode { get; set; } = CreditNoteReason.SalesReturn;
    public List<CreditNoteDetail> Lines { get; set; } = [];
}

