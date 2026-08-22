#pragma warning disable CS8618

using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// One physical piece, tracked individually.
///
/// This is what specific identification costs against: the ring that was bought
/// for ₹48,200 is sold at ₹48,200, not at an average of every ring in the case.
/// For a jeweller that is the only honest answer, because two pieces of the same
/// SKU differ in weight and in what the metal cost the day each was made.
///
/// The HUID lives here rather than on the item, because a hallmark identifies a
/// piece, not a design — two rings of the same SKU carry two different HUIDs.
/// </summary>
public class ItemSerialRead : OrgScopedEntity
{
    public long ItemSerialId { get; set; }

    public long ItemId { get; set; }


    public string SerialNumber { get; set; }
    /// <summary>Set when the item is batch-tracked as well — a serial inside a lot.</summary>
    public long? ItemBatchId { get; set; }

    /// <summary>
    /// The BIS hallmark unique id — six alphanumerics, one per physical piece.
    /// Selling unhallmarked jewellery in a notified category is an offence, so
    /// this is held against the piece and never against the SKU.
    /// </summary>


    public string? HallmarkNumber { get; set; }

    /// <summary>
    /// The layer this piece came in on. Specific identification reads the cost
    /// straight off it, with no selection rule at all.
    /// </summary>
    public long? CostLayerId { get; set; }

    public int Status { get; set; }
    /// <summary>Where the piece physically is. A location fact; the pool is still shared.</summary>
    public long? WarehouseId { get; set; }

    /// <summary>The movement that brought it in — provenance for a disputed piece.</summary>
    public long? ReceivedMovementId { get; set; }

    /// <summary>The movement that took it out. Null while it is still in stock.</summary>
    public long? IssuedMovementId { get; set; }
}



