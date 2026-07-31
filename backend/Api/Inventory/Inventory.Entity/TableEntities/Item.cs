using System.ComponentModel.DataAnnotations;
using Inventory.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Inventory.Entity.TableEntities;

/// <summary>
/// The item master, kept vertical-neutral. Pharma and jewellery attributes live
/// in 1:0..1 extension tables rather than here, so their required fields can be
/// plain NOT NULL columns instead of a conditional check per vertical, and a
/// third vertical costs a CREATE TABLE rather than a migration on the busiest
/// table in the system.
/// </summary>
public class Item : OrgScopedEntity
{
    public long ItemId { get; set; }

    /// <summary>The SKU. From the ITEM numbering series unless typed.</summary>
    [Required(ErrorMessage = "Item code is required.")]
    [MaxLength(50, ErrorMessage = "Item code cannot exceed 50 characters.")]
    public string ItemCode { get; set; } = null!;

    [Required(ErrorMessage = "Item name is required.")]
    [MaxLength(200, ErrorMessage = "Item name cannot exceed 200 characters.")]
    public string ItemName { get; set; } = null!;

    /// <summary>What prints on the invoice. Null falls back to ItemName.</summary>
    [MaxLength(200, ErrorMessage = "Print name cannot exceed 200 characters.")]
    public string? PrintName { get; set; }

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    /// <summary>
    /// Which extension table and form tab the item carries. Stored rather than
    /// inferred from the child row, because the form needs it before anything
    /// has been saved. Immutable once stock has moved.
    /// </summary>
    public ItemProfile ItemProfile { get; set; } = ItemProfile.Standard;

    public ItemType ItemType { get; set; } = ItemType.Goods;

    public long? ItemCategoryId { get; set; }

    /// <summary>Unenforced reference to mst.HsnSacCodes — cross-database, validated in C#.</summary>
    public int? HsnSacCodeId { get; set; }

    /// <summary>
    /// Unenforced reference to acc.TaxMasters.TaxGroupId — the group, not the
    /// row, so a rate revision does not orphan the item.
    /// </summary>
    public long? TaxGroupId { get; set; }

    public TaxPreference TaxPreference { get; set; } = TaxPreference.Taxable;

    /// <summary>True for pharma: the printed MRP already includes GST.</summary>
    public bool IsPriceInclusiveOfTax { get; set; }

    // --- Units. All five must belong to UomTypeId; conversion between them is
    // derived from UnitOfMeasure.ConversionToBase and never stored twice.

    public long UomTypeId { get; set; }

    /// <summary>
    /// What stock and weighted average cost are held in, and what sets quantity
    /// precision. Immutable once any movement exists — every recorded quantity
    /// is in this unit, so changing it would reinterpret the whole history.
    /// </summary>
    public long InventoryUomId { get; set; }

    public long SalesUomId { get; set; }

    public long PurchaseUomId { get; set; }

    /// <summary>What stock and valuation reports display in. Presentation only.</summary>
    public long ReportUomId { get; set; }

    public bool TrackInventory { get; set; } = true;

    /// <summary>Immutable once any stock movement exists.</summary>
    public CostingType CostingType { get; set; } = CostingType.WeightedAverage;

    public bool IsBatchTracked { get; set; }

    /// <summary>Requires batch tracking — an expiry with nothing to attach it to means nothing.</summary>
    public bool IsExpiryTracked { get; set; }

    public bool IsSerialTracked { get; set; }

    // --- Prices, all per InventoryUomId. Canonical rather than per sales unit,
    // so switching the sales unit from kilos to grams cannot turn ₹46 per kilo
    // into ₹46 per gram with nothing on screen saying so.

    [Range(0, 999999999999.9999, ErrorMessage = "Sales price cannot be negative.")]
    public decimal? SalesPrice { get; set; }

    [Range(0, 999999999999.9999, ErrorMessage = "Purchase price cannot be negative.")]
    public decimal? PurchasePrice { get; set; }

    /// <summary>Item-level default. A batch's own MRP wins where the item is batch-tracked.</summary>
    [Range(0, 999999999999.99, ErrorMessage = "MRP cannot be negative.")]
    public decimal? Mrp { get; set; }

    /// <summary>The floor a discount may not cross, checked alongside the contact's cap.</summary>
    [Range(0, 999999999999.9999, ErrorMessage = "Minimum sale price cannot be negative.")]
    public decimal? MinSalePrice { get; set; }

    /// <summary>Informational only — weighted average cost is the real one.</summary>
    [Range(0, 999999999999.9999, ErrorMessage = "Standard cost cannot be negative.")]
    public decimal? StandardCost { get; set; }

    [Range(0, 999999999999.999, ErrorMessage = "Reorder level cannot be negative.")]
    public decimal? ReorderLevel { get; set; }

    [Range(0, 999999999999.999, ErrorMessage = "Reorder quantity cannot be negative.")]
    public decimal? ReorderQuantity { get; set; }

    [Range(0, 999999999999.999, ErrorMessage = "Minimum stock level cannot be negative.")]
    public decimal? MinStockLevel { get; set; }

    [Range(0, 999999999999.999, ErrorMessage = "Maximum stock level cannot be negative.")]
    public decimal? MaxStockLevel { get; set; }

    [Range(0, 3650, ErrorMessage = "Lead time must be between 0 and 3650 days.")]
    public int? LeadTimeDays { get; set; }

    public long? DefaultWarehouseId { get; set; }

    public bool IsSales { get; set; } = true;

    public bool IsPurchase { get; set; } = true;

    /// <summary>Pharma has non-returnable lines; so does a jeweller's custom order.</summary>
    public bool IsReturnable { get; set; } = true;

    [MaxLength(500, ErrorMessage = "Image URL cannot exceed 500 characters.")]
    public string? ImageUrl { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
