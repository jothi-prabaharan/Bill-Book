using System.ComponentModel.DataAnnotations;
using Inventory.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Inventory.Entity.TableEntities;

/// <summary>
/// Barcodes for an item. A separate table because one item routinely carries
/// several — the manufacturer's EAN, a shop-printed label, and a pack-level code
/// — and point of sale has to resolve any of them to the same item.
/// </summary>
public class ItemBarcode : OrgScopedEntity
{
    public long ItemBarcodeId { get; set; }

    public long ItemId { get; set; }

    [Required(ErrorMessage = "Barcode is required.")]
    [MaxLength(50, ErrorMessage = "Barcode cannot exceed 50 characters.")]
    public string Barcode { get; set; } = null!;

    public BarcodeType BarcodeType { get; set; } = BarcodeType.Ean13;

    /// <summary>
    /// Which unit one scan represents. A pack-level code scans as a box; the
    /// item-level code scans as the inventory unit. Must belong to the item's
    /// unit type.
    /// </summary>
    public long? UomId { get; set; }

    public bool IsPrimary { get; set; }

    public bool IsActive { get; set; } = true;
}
