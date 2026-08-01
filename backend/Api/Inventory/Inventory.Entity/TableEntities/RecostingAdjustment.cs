using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Inventory.Entity.TableEntities;

/// <summary>
/// What one recosting run did to one issue's cost of goods sold.
///
/// A receipt dated before issues that have already consumed layers invalidates
/// every allocation after it — under FIFO the backdated stock should have gone
/// out first, and it did not. The allocations are unwound and replayed, and the
/// difference between what each issue cost before and what it costs now is a
/// row here.
///
/// This is the record an accountant needs: not that COGS changed, but by how
/// much, on which sale, and because of which receipt. The matching
/// <c>Dr/Cr COGS</c> journal is Accounting's to write from these rows.
/// </summary>
public class RecostingAdjustment : OrgScopedEntity
{
    public long RecostingAdjustmentId { get; set; }

    /// <summary>Groups every adjustment made by one run, so a run can be read as a whole.</summary>
    public Guid RecostingBatchId { get; set; }

    public long ItemId { get; set; }

    /// <summary>The issue whose cost changed.</summary>
    public long StockMovementId { get; set; }

    /// <summary>The backdated receipt that caused the restatement.</summary>
    public long TriggerStockMovementId { get; set; }

    /// <summary>What the issue was costed at before this run.</summary>
    [Range(0, 999999999999.99, ErrorMessage = "Previous cost cannot be negative.")]
    public decimal PreviousCost { get; set; }

    [Range(0, 999999999999.99, ErrorMessage = "New cost cannot be negative.")]
    public decimal NewCost { get; set; }

    /// <summary>
    /// <c>NewCost - PreviousCost</c>. Signed, deliberately: this is a correction
    /// rather than a movement, and it genuinely runs both ways.
    /// </summary>
    public decimal Delta { get; set; }

    public DateTimeOffset RunAt { get; set; }
}
