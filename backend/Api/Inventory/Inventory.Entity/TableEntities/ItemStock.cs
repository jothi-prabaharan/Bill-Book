using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Inventory.Entity.TableEntities;

/// <summary>
/// The live stock position for an item: how much is on hand and what it cost.
///
/// <b>One row per item, not per warehouse.</b> Stock is a single shared pool
/// across every branch and every location — a warehouse is where a movement
/// happened, not a separate balance. Splitting this row per location is what
/// makes one SKU carry two weighted average costs and the totals stop
/// reconciling.
///
/// This is the target of the synchronous point-of-sale decrement: a conditional
/// <c>UPDATE … WHERE QuantityOnHand &gt;= @qty</c> that either moves the row or
/// affects nothing. Never read-then-write, or two tills sell the last unit.
///
/// <see cref="ItemId"/> is the key and the foreign key both, so a second row for
/// one item is structurally impossible rather than merely unlikely.
/// </summary>
public class ItemStock : OrgScopedEntity
{
    public long ItemId { get; set; }

    /// <summary>Always in the item's <c>InventoryUomId</c>. That is what makes this one number.</summary>
    [Range(0, 999999999999.999, ErrorMessage = "Quantity on hand cannot be negative.")]
    public decimal QuantityOnHand { get; set; }

    /// <summary>
    /// Cost of one inventory unit, company-wide. Moved only by a receipt:
    /// <c>(oldQty × oldWac + recvQty × recvCost) ÷ (oldQty + recvQty)</c>.
    /// An issue moves quantity and leaves this alone.
    ///
    /// Six decimal places rather than the usual four because it is a derived
    /// average that then multiplies a quantity — rounding it at the money scale
    /// puts the error into every COGS posting that follows.
    /// </summary>
    [Range(0, 999999999999.999999, ErrorMessage = "Weighted average cost cannot be negative.")]
    public decimal WeightedAverageCost { get; set; }

    /// <summary>When stock last moved. Read by reports; never a substitute for the movement rows.</summary>
    public DateTimeOffset? LastMovementAt { get; set; }
}
