using System.ComponentModel.DataAnnotations;

namespace Inventory.Entity.Models;

public class ItemListItem
{
    public long ItemId { get; set; }

    public string ItemCode { get; set; } = null!;

    public string ItemName { get; set; } = null!;

    public string ItemProfile { get; set; } = null!;

    public string ItemType { get; set; } = null!;

    public long? ItemCategoryId { get; set; }

    public string? CategoryName { get; set; }

    public string CostingType { get; set; } = null!;

    public bool TrackInventory { get; set; }

    /// <summary>The inventory unit's code — what quantities on this row are in.</summary>
    public string InventoryUomCode { get; set; } = null!;

    public decimal? SalesPrice { get; set; }

    public decimal? Mrp { get; set; }

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }
}

public class ItemDetail : ItemListItem
{
    public string? PrintName { get; set; }

    public string? Description { get; set; }

    public int? HsnSacCodeId { get; set; }

    public long? TaxGroupId { get; set; }

    public string TaxPreference { get; set; } = null!;

    public bool IsPriceInclusiveOfTax { get; set; }

    public long UomTypeId { get; set; }

    public long InventoryUomId { get; set; }

    public long SalesUomId { get; set; }

    public long PurchaseUomId { get; set; }

    public long ReportUomId { get; set; }

    public bool IsBatchTracked { get; set; }

    public bool IsExpiryTracked { get; set; }

    public bool IsSerialTracked { get; set; }

    public decimal? PurchasePrice { get; set; }

    public decimal? MinSalePrice { get; set; }

    public decimal? StandardCost { get; set; }

    public decimal? ReorderLevel { get; set; }

    public decimal? ReorderQuantity { get; set; }

    public decimal? MinStockLevel { get; set; }

    public decimal? MaxStockLevel { get; set; }

    public int? LeadTimeDays { get; set; }

    public long? DefaultWarehouseId { get; set; }

    public bool IsSales { get; set; }

    public bool IsPurchase { get; set; }

    public bool IsReturnable { get; set; }

    public string? ImageUrl { get; set; }

    /// <summary>
    /// True once stock has moved. The form renders the unit type, inventory
    /// unit, costing type and tracking flags read-only when it is.
    /// </summary>
    public bool HasStockMovements { get; set; }

    public ItemJewelleryModel? Jewellery { get; set; }

    public ItemPharmaModel? Pharma { get; set; }

    public List<ItemBarcodeModel> Barcodes { get; set; } = [];
}

public class ItemJewelleryModel
{
    [Required(ErrorMessage = "Metal type is required.")]
    public string MetalType { get; set; } = "Gold";

    public long MetalPurityId { get; set; }

    [Range(0.001, 999999.999, ErrorMessage = "Gross weight must be greater than zero.")]
    public decimal GrossWeight { get; set; }

    [Range(0.001, 999999.999, ErrorMessage = "Net weight must be greater than zero.")]
    public decimal NetWeight { get; set; }

    [Range(0, 999999.999, ErrorMessage = "Stone weight cannot be negative.")]
    public decimal StoneWeight { get; set; }

    [Range(0, 999999999999.99, ErrorMessage = "Stone charge cannot be negative.")]
    public decimal StoneCharge { get; set; }

    [Range(0, 100, ErrorMessage = "Wastage must be between 0 and 100 percent.")]
    public decimal WastagePercent { get; set; }

    [Required(ErrorMessage = "Making charge type is required.")]
    public string MakingChargeType { get; set; } = "Percentage";

    [Range(0, 999999999999.9999, ErrorMessage = "Making charge cannot be negative.")]
    public decimal MakingChargeValue { get; set; }

    public bool IsHallmarked { get; set; }
}

public class ItemPharmaModel
{
    [Required(ErrorMessage = "Generic name is required.")]
    [MaxLength(200, ErrorMessage = "Generic name cannot exceed 200 characters.")]
    public string GenericName { get; set; } = null!;

    [MaxLength(50, ErrorMessage = "Strength cannot exceed 50 characters.")]
    public string? Strength { get; set; }

    [Required(ErrorMessage = "Dosage form is required.")]
    public string DosageForm { get; set; } = "Tablet";

    [Required(ErrorMessage = "Pack size is required.")]
    [MaxLength(50, ErrorMessage = "Pack size cannot exceed 50 characters.")]
    public string PackSize { get; set; } = null!;

    [Required(ErrorMessage = "Manufacturer is required.")]
    [MaxLength(200, ErrorMessage = "Manufacturer cannot exceed 200 characters.")]
    public string ManufacturerName { get; set; } = null!;

    [MaxLength(200, ErrorMessage = "Marketed by cannot exceed 200 characters.")]
    public string? MarketedBy { get; set; }

    [Required(ErrorMessage = "Drug schedule is required.")]
    public string DrugSchedule { get; set; } = "None";

    public bool IsPrescriptionRequired { get; set; }

    public bool IsNarcotic { get; set; }

    [Required(ErrorMessage = "Storage condition is required.")]
    public string StorageCondition { get; set; } = "Ambient";

    [Range(1, 3650, ErrorMessage = "Shelf life must be between 1 and 3650 days.")]
    public int? ShelfLifeDays { get; set; }

    [Range(0, 3650, ErrorMessage = "Minimum expiry days must be between 0 and 3650.")]
    public int MinExpiryDaysOnReceipt { get; set; }

    [Range(0, 3650, ErrorMessage = "Expiry alert days must be between 0 and 3650.")]
    public int ExpiryAlertDays { get; set; } = 90;
}

public class ItemBarcodeModel
{
    /// <summary>Zero on a row being added.</summary>
    public long ItemBarcodeId { get; set; }

    [Required(ErrorMessage = "Barcode is required.")]
    [MaxLength(50, ErrorMessage = "Barcode cannot exceed 50 characters.")]
    public string Barcode { get; set; } = null!;

    [Required(ErrorMessage = "Barcode type is required.")]
    public string BarcodeType { get; set; } = "Ean13";

    public long? UomId { get; set; }

    public bool IsPrimary { get; set; }

    public bool IsActive { get; set; } = true;
}

public class SaveItemRequest
{
    /// <summary>Leave empty to take the next code from the ITEM numbering series.</summary>
    [MaxLength(50, ErrorMessage = "Item code cannot exceed 50 characters.")]
    public string? ItemCode { get; set; }

    [Required(ErrorMessage = "Item name is required.")]
    [MaxLength(200, ErrorMessage = "Item name cannot exceed 200 characters.")]
    public string ItemName { get; set; } = null!;

    [MaxLength(200, ErrorMessage = "Print name cannot exceed 200 characters.")]
    public string? PrintName { get; set; }

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Item profile is required.")]
    public string ItemProfile { get; set; } = "Standard";

    [Required(ErrorMessage = "Item type is required.")]
    public string ItemType { get; set; } = "Goods";

    public long? ItemCategoryId { get; set; }

    public int? HsnSacCodeId { get; set; }

    public long? TaxGroupId { get; set; }

    [Required(ErrorMessage = "Tax preference is required.")]
    public string TaxPreference { get; set; } = "Taxable";

    public bool IsPriceInclusiveOfTax { get; set; }

    public long UomTypeId { get; set; }

    public long InventoryUomId { get; set; }

    public long SalesUomId { get; set; }

    public long PurchaseUomId { get; set; }

    public long ReportUomId { get; set; }

    public bool TrackInventory { get; set; } = true;

    [Required(ErrorMessage = "Costing type is required.")]
    public string CostingType { get; set; } = "WeightedAverage";

    public bool IsBatchTracked { get; set; }

    public bool IsExpiryTracked { get; set; }

    public bool IsSerialTracked { get; set; }

    public decimal? SalesPrice { get; set; }

    public decimal? PurchasePrice { get; set; }

    public decimal? Mrp { get; set; }

    public decimal? MinSalePrice { get; set; }

    public decimal? StandardCost { get; set; }

    public decimal? ReorderLevel { get; set; }

    public decimal? ReorderQuantity { get; set; }

    public decimal? MinStockLevel { get; set; }

    public decimal? MaxStockLevel { get; set; }

    public int? LeadTimeDays { get; set; }

    public long? DefaultWarehouseId { get; set; }

    public bool IsSales { get; set; } = true;

    public bool IsPurchase { get; set; } = true;

    public bool IsReturnable { get; set; } = true;

    [MaxLength(500, ErrorMessage = "Image URL cannot exceed 500 characters.")]
    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public ItemJewelleryModel? Jewellery { get; set; }

    public ItemPharmaModel? Pharma { get; set; }

    public List<ItemBarcodeModel> Barcodes { get; set; } = [];
}

public enum SaveItemOutcome
{
    Ok = 0,
    NotFound = 1,
    DuplicateCode = 2,
    DuplicateBarcode = 3,
    /// <summary>One of the four unit slots is not of the item's unit type.</summary>
    UnitTypeMismatch = 4,
    UnknownUnit = 5,
    /// <summary>The profile says Jewellery or Pharma but the matching block is missing.</summary>
    ProfileDetailsMissing = 6,
    /// <summary>A block was sent that does not match the profile.</summary>
    ProfileDetailsUnexpected = 7,
    /// <summary>Unit type, inventory unit, costing type and tracking flags are frozen once stock has moved.</summary>
    LockedAfterMovement = 8,
    /// <summary>Fefo needs batch and expiry; SpecificIdentification needs serials.</summary>
    CostingTrackingMismatch = 9,
    ServiceCannotBeStocked = 10,
    InvalidValue = 11,
    UnknownCategory = 12,
}

public sealed record SaveItemResult(SaveItemOutcome Outcome, long? ItemId, string? ItemCode);
