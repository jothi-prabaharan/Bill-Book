using Shared.Kernel.Approvals;

namespace Inventory.Entity.TableEntities;

/// <summary>
/// One level of a stock adjustment's approval chain (TK-102). Stored here, by
/// the service that owns the sheet. A submission is a <see cref="Round"/>: an
/// edit mid-chain cancels the round's open steps and they stay as history.
/// </summary>
public class InventoryApprovalStep : ApprovalStepBase
{
    public long InventoryApprovalStepId { get; set; }

    /// <summary>Which submission of the sheet this step belongs to, from 1.</summary>
    public int Round { get; set; } = 1;
}
