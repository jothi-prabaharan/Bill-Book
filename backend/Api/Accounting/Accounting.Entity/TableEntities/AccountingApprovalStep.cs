using Shared.Kernel.Approvals;

namespace Accounting.Entity.TableEntities;

/// <summary>
/// One level of an Accounting document's approval chain (TK-101): a spend
/// money payment or a manual journal, told apart by
/// <see cref="ApprovalStepBase.RequestKind"/>.
///
/// Stored here, by the service that owns the document, as every service
/// stores its own. A submission is a <see cref="Round"/>: editing a document
/// mid-chain cancels the round's open steps and they stay as history, and
/// submitting again starts the next round.
/// </summary>
public class AccountingApprovalStep : ApprovalStepBase
{
    public long AccountingApprovalStepId { get; set; }

    /// <summary>Which submission of the document this step belongs to, from 1.</summary>
    public int Round { get; set; } = 1;
}
