using System.ComponentModel.DataAnnotations;
using Inventory.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Inventory.Entity.TableEntities;

/// <summary>
/// Jewellery attributes, one row per item at most. ItemId is both the key and
/// the foreign key, so a second row for the same item is impossible.
///
/// The weights here are the <b>nominal design</b> values used to default a new
/// piece. Each physical piece's actual gross, net and stone weights — and its
/// HUID — live on its serial row, because two chains off the same design come
/// off the bench at 12.480 g and 12.655 g and costing must use the real one.
/// </summary>
public class ItemJewelleryDetails : OrgScopedEntity
{
    /// <summary>Primary key and foreign key both.</summary>
    public long ItemId { get; set; }

    public MetalType MetalType { get; set; } = MetalType.Gold;

    public long MetalPurityId { get; set; }

    [Range(0.001, 999999.999, ErrorMessage = "Gross weight must be greater than zero.")]
    public decimal GrossWeight { get; set; }

    /// <summary>Gross less stones. The metal rate applies to this, not to gross.</summary>
    [Range(0.001, 999999.999, ErrorMessage = "Net weight must be greater than zero.")]
    public decimal NetWeight { get; set; }

    [Range(0, 999999.999, ErrorMessage = "Stone weight cannot be negative.")]
    public decimal StoneWeight { get; set; }

    /// <summary>Stones are priced as an amount, never by the metal rate.</summary>
    [Range(0, 999999999999.99, ErrorMessage = "Stone charge cannot be negative.")]
    public decimal StoneCharge { get; set; }

    [Range(0, 100, ErrorMessage = "Wastage must be between 0 and 100 percent.")]
    public decimal WastagePercent { get; set; }

    public MakingChargeType MakingChargeType { get; set; } = MakingChargeType.Percentage;

    [Range(0, 999999999999.9999, ErrorMessage = "Making charge cannot be negative.")]
    public decimal MakingChargeValue { get; set; }

    /// <summary>Whether pieces of this design carry a BIS hallmark and therefore a HUID.</summary>
    public bool IsHallmarked { get; set; }
}
