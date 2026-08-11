using System.ComponentModel.DataAnnotations;
using Inventory.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Inventory.Entity.TableEntities;

/// <summary>
/// One item on an adjustment sheet.
///
/// Carries its own <c>OrgId</c>, query filter and RLS policy rather than being
/// scoped through its header. Scoping a detail table through its parent means no
/// EF query filter at all and an RLS policy that has to subquery the header,
/// which is strictly weaker than the two lines every other table gets for
/// nothing.
/// </summary>
public class StockAdjustmentLine : OrgScopedEntity
{
    public long StockAdjustmentLineId { get; set; }

    public long StockAdjustmentId { get; set; }

    /// <summary>
    /// Position on the sheet, and the line's identity in the ledger: it becomes
    /// the movement's <c>SourceLineId</c>, which is what makes each line's
    /// posting individually replaceable when a cost is later restated.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Line number must be greater than zero.")]
    public int LineNumber { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose an item.")]
    public long ItemId { get; set; }

    /// <summary>The unit the person worked in. Must belong to the item's unit type.</summary>
    public long? UomId { get; set; }

    /// <summary>
    /// What the system believed was on hand when the line was keyed. **Only on a
    /// physical count**, and a snapshot rather than a live read: the whole point
    /// of a count is the difference between the shelf and the books at one
    /// moment, and re-deriving it later would compare against a figure that has
    /// moved since.
    /// </summary>
    public decimal? SystemQuantity { get; set; }

    /// <summary>What was actually on the shelf. Only on a physical count.</summary>
    [Range(0, 999999999999.999, ErrorMessage = "Counted quantity cannot be negative.")]
    public decimal? CountedQuantity { get; set; }

    /// <summary>
    /// How much stock moves, always positive — <see cref="Direction"/> carries
    /// the sign, exactly as on a movement. On a count this is the size of the
    /// difference; on an adjustment it is what was keyed.
    /// </summary>
    [Range(0.000001, 999999999999.999, ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; }

    public StockDirection Direction { get; set; }

    /// <summary>
    /// Cost per entered unit, for stock coming <b>in</b> only. Stock going out
    /// is valued at what it already cost — the costing engine settles that — so
    /// a cost keyed here would be ignored, and is refused rather than ignored.
    /// </summary>
    [Range(0, 999999999999.999999, ErrorMessage = "Unit cost cannot be negative.")]
    public decimal? UnitCost { get; set; }

    /// <summary>An existing lot, or a lot to find or create. Required when the item is batch-tracked.</summary>
    [MaxLength(50, ErrorMessage = "Batch number cannot exceed 50 characters.")]
    public string? BatchNumber { get; set; }

    public DateOnly? BatchExpiryDate { get; set; }

    /// <summary>
    /// The movement this line wrote, set when the sheet posts. It is what proves
    /// a line reached the item's history, and what the reversal reads to put the
    /// stock back the way it came.
    /// </summary>
    public long? StockMovementId { get; set; }

    [MaxLength(300, ErrorMessage = "Line notes cannot exceed 300 characters.")]
    public string? Notes { get; set; }
}
