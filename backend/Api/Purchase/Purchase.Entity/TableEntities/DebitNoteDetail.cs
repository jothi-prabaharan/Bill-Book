using Shared.Kernel.Documents;

namespace Purchase.Entity.TableEntities;

/// <summary>
/// One line of a debit note.
///
/// <see cref="BillDetailId"/> is <b>required</b>, and that is the whole point of
/// the line: it is how returned stock walks back to the cost layer it was
/// received on, so the return leaves inventory at what it actually cost rather
/// than at today's weighted average.
/// </summary>
public class DebitNoteDetail : DocumentLineBase
{
    public long DebitNoteDetailId { get; set; }

    public long DebitNoteId { get; set; }

    /// <summary>The bill line being reversed. Required — see the type summary.</summary>
    public long BillDetailId { get; set; }

    public List<DebitNoteDetailTax> Taxes { get; set; } = [];
}
