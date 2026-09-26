using Shared.Kernel.Approvals;

namespace Purchase.Entity.TableEntities;

/// <summary>
/// One level of a purchase document's approval chain (TK-100): a purchase
/// order, bill or debit note, told apart by <see cref="ApprovalStepBase.RequestKind"/>.
///
/// Stored here, by the service that owns the document, as every service
/// stores its own (design, decision 8). A submission is a <see cref="Round"/>:
/// editing a document mid-chain cancels the round's open steps and they stay as
/// history, and submitting again starts the next round, so an approval given
/// in an old round is never mistaken for one in the new.
/// </summary>
public class PurchaseApprovalStep : ApprovalStepBase
{
    public long PurchaseApprovalStepId { get; set; }

    /// <summary>Which submission of the document this step belongs to, from 1.</summary>
    public int Round { get; set; } = 1;
}
