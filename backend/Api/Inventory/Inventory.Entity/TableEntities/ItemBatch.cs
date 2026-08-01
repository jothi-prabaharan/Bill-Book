using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Inventory.Entity.TableEntities;

/// <summary>
/// A lot of one item received together — same manufacture run, same expiry, and
/// usually the same printed MRP.
///
/// The expiry is what earns this a table. A chemist does not hold "500 strips of
/// paracetamol"; they hold three lots expiring on three different dates, and
/// which one goes out of the door first is a legal question before it is a
/// costing one. FEFO reads <see cref="ExpiryDate"/> and nothing else.
/// </summary>
public class ItemBatch : OrgScopedEntity
{
    public long ItemBatchId { get; set; }

    public long ItemId { get; set; }

    /// <summary>As printed on the pack. Unique per item — the same number on two lots is unresolvable.</summary>
    [Required(ErrorMessage = "Batch number is required.")]
    [MaxLength(50, ErrorMessage = "Batch number cannot exceed 50 characters.")]
    public string BatchNumber { get; set; } = null!;

    public DateOnly? ManufactureDate { get; set; }

    /// <summary>
    /// Required in practice for anything expiry-tracked — checked in C#, because
    /// the rule lives on the item and a constraint cannot see across the join.
    /// </summary>
    public DateOnly? ExpiryDate { get; set; }

    /// <summary>
    /// The MRP printed on this lot. Wins over the item's own: a price rise
    /// reprints the pack, and the older lot must keep selling at what is on it.
    /// </summary>
    [Range(0, 999999999999.99, ErrorMessage = "MRP cannot be negative.")]
    public decimal? Mrp { get; set; }

    /// <summary>Free text — the manufacturer's own lot reference, when it differs.</summary>
    [MaxLength(50, ErrorMessage = "Supplier batch reference cannot exceed 50 characters.")]
    public string? SupplierBatchNumber { get; set; }

    public bool IsActive { get; set; } = true;
}
