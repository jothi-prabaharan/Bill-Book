using Shared.Kernel.Documents;

namespace Sales.Entity.TableEntities;

/// <summary>
/// One tax component on one credit note line.
///
/// The rates here are the <b>invoice's</b> rates, not today's — GST is reversed
/// on what was charged, and a note raised after a rate revision that used the new
/// rate would leave the return short or over by the difference.
/// </summary>
public class CreditNoteDetailTax : DocumentLineTaxBase
{
    public long CreditNoteDetailTaxId { get; set; }

    public long CreditNoteDetailId { get; set; }
}
