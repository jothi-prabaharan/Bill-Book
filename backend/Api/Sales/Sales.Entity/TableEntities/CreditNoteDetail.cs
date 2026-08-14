using Shared.Kernel.Documents;

namespace Sales.Entity.TableEntities;

/// <summary>
/// One line of a credit note.
///
/// <b><see cref="InvoiceDetailId"/> is required</b>, and it is the column that
/// makes the whole thing work: stock returns to <i>the cost layer it came
/// from, at its original cost</i>, and the invoice line is how that layer is
/// found. Buy, sell, credit-note must leave stock value exactly where it
/// started — that is the acceptance test, and without this column the return
/// would come back at whatever the running average happens to be today.
/// </summary>
public class CreditNoteDetail : DocumentLineBase
{
    public long CreditNoteDetailId { get; set; }

    public long CreditNoteId { get; set; }

    /// <summary>The invoice line being reversed. Required, and a real foreign key.</summary>
    public long InvoiceDetailId { get; set; }
    public List<CreditNoteDetailTax> Taxes { get; set; } = [];
}

