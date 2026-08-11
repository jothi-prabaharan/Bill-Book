using System.ComponentModel.DataAnnotations;
using Inventory.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Inventory.Entity.TableEntities;

/// <summary>
/// A sheet of stock corrections posted together — <c>STA</c>.
///
/// Stock could already be adjusted one movement at a time, and that is what this
/// replaces for anything larger than a single correction. A physical count of
/// twenty items is one event: it happened on one date, for one reason, and one
/// person authorised the whole of it. Twenty loose movements record the same
/// quantities while losing every one of those facts, and leave the ledger
/// showing twenty unexplained adjustments instead of one count.
///
/// <b>It does not post to the ledger itself.</b> Each line records an ordinary
/// stock movement carrying this document's id, and the movement-to-ledger
/// mapping already files a sourced movement under its document rather than under
/// itself. So the accounting falls out of machinery that was already there and
/// already tested, and there is exactly one place in the product that decides
/// what a stock movement means in the general ledger.
///
/// <b>The approver is <c>PostedBy</c>.</b> There is no separate approver column,
/// because there is nothing for it to say that the audit columns do not:
/// <c>CreatedBy</c> is whoever keyed the count and <c>PostedBy</c> is whoever
/// authorised it, and comparing the two is the segregation-of-duties check. A
/// column that repeats another column is the same mistake as a BranchId beside
/// an OrgId.
/// </summary>
public class StockAdjustment : OrgScopedEntity
{
    public long StockAdjustmentId { get; set; }

    /// <summary>
    /// From the <c>STA</c> series, taken <b>at post, not at draft</b>. A number
    /// taken when the sheet is opened is a number lost every time a count is
    /// abandoned half-way, and the series has to run without gaps.
    /// </summary>
    [MaxLength(30, ErrorMessage = "Adjustment number cannot exceed 30 characters.")]
    public string? AdjustmentNo { get; set; }

    /// <summary>
    /// The date the stock actually moved — the date of the count, not the date
    /// it was typed up. It is what the movements and the ledger rows are dated
    /// with, so a count done on the 31st and keyed on the 2nd still lands in the
    /// month it belongs to.
    /// </summary>
    public DateOnly AdjustmentDate { get; set; }

    /// <summary>
    /// Whether quantities were keyed as differences or as counted totals. It
    /// changes what the person types and what the screen shows, not what is
    /// posted — both end as a movement of the difference.
    /// </summary>
    public StockAdjustmentKind Kind { get; set; } = StockAdjustmentKind.Adjustment;

    public StockAdjustmentReason Reason { get; set; } = StockAdjustmentReason.CountCorrection;

    /// <summary>
    /// Where it was counted. A location dimension only, exactly as on a
    /// movement — stock is one pool per branch, so this never partitions a
    /// quantity, it only records where somebody was standing.
    /// </summary>
    public long? WarehouseId { get; set; }

    [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    public string? Notes { get; set; }

    public StockAdjustmentStatus Status { get; set; } = StockAdjustmentStatus.Draft;

    public DateTimeOffset? PostedAt { get; set; }

    /// <summary>Who authorised it. A plain Guid — users live in the master database.</summary>
    public Guid? PostedBy { get; set; }

    /// <summary>
    /// The adjustment that undid this one. Set on the original when a reversal
    /// posts.
    /// </summary>
    public long? ReversedByStockAdjustmentId { get; set; }

    /// <summary>
    /// The adjustment this one undoes. Set on the reversal. The pair is linked
    /// from both ends for the same reason a journal reversal is: from either
    /// document you can see the other without searching for it.
    /// </summary>
    public long? ReversesStockAdjustmentId { get; set; }
}
