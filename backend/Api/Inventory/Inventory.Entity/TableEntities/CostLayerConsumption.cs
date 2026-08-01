using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Inventory.Entity.TableEntities;

/// <summary>
/// How much of one layer one issue took, and at what cost.
///
/// An issue of 30 units against three layers writes three rows. Their costs sum
/// to the COGS of that issue, and — because each row names the layer — a
/// disputed gross margin can be walked back to the purchases it came from.
///
/// Without this table an issue would only know its total cost, and a backdated
/// receipt could never be unwound: there would be nothing recording which
/// allocations to undo.
/// </summary>
public class CostLayerConsumption : OrgScopedEntity
{
    public long CostLayerConsumptionId { get; set; }

    public long CostLayerId { get; set; }

    /// <summary>The issue that consumed it.</summary>
    public long StockMovementId { get; set; }

    [Range(0.000001, 999999999999.999, ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; }

    /// <summary>
    /// The layer's cost, copied at the moment of consumption. Snapshotted for the
    /// same reason a movement snapshots its conversion factor: the allocation has
    /// to keep meaning what it meant, whatever happens to the layer later.
    /// </summary>
    [Range(0, 999999999999.999999, ErrorMessage = "Unit cost cannot be negative.")]
    public decimal UnitCost { get; set; }

    /// <summary><c>Quantity × UnitCost</c>. This is the COGS contribution of this allocation.</summary>
    [Range(0, 999999999999.99, ErrorMessage = "Total cost cannot be negative.")]
    public decimal TotalCost { get; set; }
}
