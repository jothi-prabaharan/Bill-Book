using Purchase.Entity.Enums;
using Shared.Kernel.Documents;

namespace Purchase.Entity.TableEntities;

/// <summary>
/// A debit note — <c>DBN</c>. The way back out: goods returned to the vendor, a
/// price the vendor billed wrong, a shortage.
///
/// <b><see cref="BillId"/> is required</b>, for the same two reasons the credit
/// note requires its invoice: GST wants a debit note to name the document it
/// corrects, and stock going back needs the bill line to find the cost layer it
/// arrived on. Returning it against nothing would value the return at whatever
/// the running average happens to be today.
///
/// It posts <c>Dr Accounts Payable / Cr Purchase Returns</c> — a contra
/// <i>Expense</i> account, so the report subtracts rather than adding a negative
/// — with the Input GST reversed, because credit cannot be claimed on goods that
/// went back.
/// </summary>
public class DebitNote : DocumentHeaderBase
{
    public long DebitNoteId { get; set; }

    /// <summary>The bill being corrected. Required, and a real foreign key.</summary>
    public long BillId { get; set; }

    /// <summary>
    /// What is being corrected. Only <see cref="DebitNoteReason.PurchaseReturn"/>
    /// moves stock; the rest are money-only corrections.
    /// </summary>
    public DebitNoteReason ReasonCode { get; set; } = DebitNoteReason.PurchaseReturn;

    public List<DebitNoteDetail> Lines { get; set; } = [];
}
