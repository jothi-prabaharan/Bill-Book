#pragma warning disable CS8618

using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// The item master, kept vertical-neutral. Pharma and jewellery attributes live
/// in 1:0..1 extension tables rather than here, so their required fields can be
/// plain NOT NULL columns instead of a conditional check per vertical, and a
/// third vertical costs a CREATE TABLE rather than a migration on the busiest
/// table in the system.
/// </summary>
public class ItemRead : OrgScopedEntity
{
    public long ItemId { get; set; }

    /// <summary>The SKU. From the ITEM numbering series unless typed.</summary>


    public string ItemCode { get; set; }
    public string ItemName { get; set; }
    /// <summary>What prints on the invoice. Null falls back to ItemName.</summary>

    public string? PrintName { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Which extension table and form tab the item carries. Stored rather than
    /// inferred from the child row, because the form needs it before anything
    /// has been saved. Immutable once stock has moved.
    /// </summary>
    public int ItemProfile { get; set; }
    public int ItemType { get; set; }
    public long? ItemCategoryId { get; set; }

    /// <summary>Unenforced reference to mst.HsnSacCodes — cross-database, validated in C#.</summary>
    public int? HsnSacCodeId { get; set; }

    /// <summary>
    /// Unenforced reference to acc.TaxMasters.TaxGroupId — the group, not the
    /// row, so a rate revision does not orphan the item.
    /// </summary>
    public long? TaxGroupId { get; set; }

    public int TaxPreference { get; set; }
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

    public bool TrackInventory { get; set; }
    /// <summary>Immutable once any stock movement exists.</summary>
    public int CostingType { get; set; }
    public bool IsBatchTracked { get; set; }

    /// <summary>Requires batch tracking — an expiry with nothing to attach it to means nothing.</summary>
    public bool IsExpiryTracked { get; set; }

    public bool IsSerialTracked { get; set; }

    // --- Prices, all per InventoryUomId. Canonical rather than per sales unit,
    // so switching the sales unit from kilos to grams cannot turn ₹46 per kilo
    // into ₹46 per gram with nothing on screen saying so.

    public decimal? SalesPrice { get; set; }

    public decimal? PurchasePrice { get; set; }

    /// <summary>Item-level default. A batch's own MRP wins where the item is batch-tracked.</summary>

    public decimal? Mrp { get; set; }

    /// <summary>The floor a discount may not cross, checked alongside the contact's cap.</summary>

    public decimal? MinSalePrice { get; set; }

    /// <summary>Informational only — weighted average cost is the real one.</summary>

    public decimal? StandardCost { get; set; }

    public decimal? ReorderLevel { get; set; }

    public decimal? ReorderQuantity { get; set; }

    public decimal? MinStockLevel { get; set; }

    public decimal? MaxStockLevel { get; set; }

    public int? LeadTimeDays { get; set; }

    public long? DefaultWarehouseId { get; set; }

    public bool IsSales { get; set; }
    public bool IsPurchase { get; set; }
    /// <summary>Pharma has non-returnable lines; so does a jeweller's custom order.</summary>
    public bool IsReturnable { get; set; }
    public string? ImageUrl { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }

    // CreatedAt is inherited from OrgScopedEntity → AuditableEntity — already available.
}



