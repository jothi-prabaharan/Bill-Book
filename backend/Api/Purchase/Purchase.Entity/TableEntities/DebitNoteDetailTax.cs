using Shared.Kernel.Documents;

namespace Purchase.Entity.TableEntities;

/// <summary>One tax component on one debit note line. Input GST being reversed.</summary>
public class DebitNoteDetailTax : DocumentLineTaxBase
{
    public long DebitNoteDetailTaxId { get; set; }

    public long DebitNoteDetailId { get; set; }
}
