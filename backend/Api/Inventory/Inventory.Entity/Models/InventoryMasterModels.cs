using System.ComponentModel.DataAnnotations;

namespace Inventory.Entity.Models;

public class ItemCategoryListItem
{
    public long ItemCategoryId { get; set; }

    public string CategoryCode { get; set; } = null!;

    public string CategoryName { get; set; } = null!;

    public long? ParentCategoryId { get; set; }

    public string? DefaultItemProfile { get; set; }

    public string? DefaultCostingType { get; set; }

    public long? DefaultUomTypeId { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }

    /// <summary>1 for a top-level category. Three is the maximum.</summary>
    public int Depth { get; set; }

    public int ItemCount { get; set; }
}

public class SaveItemCategoryRequest
{
    [Required(ErrorMessage = "Category code is required.")]
    [MaxLength(20, ErrorMessage = "Category code cannot exceed 20 characters.")]
    public string CategoryCode { get; set; } = null!;

    [Required(ErrorMessage = "Category name is required.")]
    [MaxLength(100, ErrorMessage = "Category name cannot exceed 100 characters.")]
    public string CategoryName { get; set; } = null!;

    public long? ParentCategoryId { get; set; }

    public string? DefaultItemProfile { get; set; }

    public string? DefaultCostingType { get; set; }

    public long? DefaultUomTypeId { get; set; }

    public bool IsActive { get; set; } = true;
}

public class MetalPurityListItem
{
    public long MetalPurityId { get; set; }

    public string MetalType { get; set; } = null!;

    public string? PuritySystemName { get; set; }

    public string PurityName { get; set; } = null!;

    public decimal PurityFactor { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsSystem { get; set; }

    public bool IsActive { get; set; }
}

public class SaveMetalPurityRequest
{
    [Required(ErrorMessage = "Metal type is required.")]
    public string MetalType { get; set; } = "Gold";

    [Required(ErrorMessage = "Purity name is required.")]
    [MaxLength(20, ErrorMessage = "Purity name cannot exceed 20 characters.")]
    public string PurityName { get; set; } = null!;

    [Range(0.0001, 1.0, ErrorMessage = "Purity factor must be between 0.0001 and 1.")]
    public decimal PurityFactor { get; set; }

    public bool IsActive { get; set; } = true;
}

public class WarehouseListItem
{
    public long WarehouseId { get; set; }

    public string WarehouseCode { get; set; } = null!;

    public string WarehouseName { get; set; } = null!;

    public string WarehouseType { get; set; } = null!;

    public string StorageType { get; set; } = null!;

    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? City { get; set; }

    public int? StateId { get; set; }

    public int? CountryId { get; set; }

    public string? PostalCode { get; set; }

    public string? Gstin { get; set; }

    public string? ContactPersonName { get; set; }

    public string? PhoneNumber { get; set; }

    public string? MobileNumber { get; set; }

    public bool IsDefault { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }
}

public class SaveWarehouseRequest
{
    [MaxLength(20, ErrorMessage = "Warehouse code cannot exceed 20 characters.")]
    public string? WarehouseCode { get; set; }

    [Required(ErrorMessage = "Warehouse name is required.")]
    [MaxLength(100, ErrorMessage = "Warehouse name cannot exceed 100 characters.")]
    public string WarehouseName { get; set; } = null!;

    [Required(ErrorMessage = "Warehouse type is required.")]
    public string WarehouseType { get; set; } = "Store";

    [Required(ErrorMessage = "Storage type is required.")]
    public string StorageType { get; set; } = "Ambient";

    [MaxLength(200, ErrorMessage = "Address line 1 cannot exceed 200 characters.")]
    public string? AddressLine1 { get; set; }

    [MaxLength(200, ErrorMessage = "Address line 2 cannot exceed 200 characters.")]
    public string? AddressLine2 { get; set; }

    [MaxLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    public string? City { get; set; }

    public int? StateId { get; set; }

    public int? CountryId { get; set; }

    [MaxLength(10, ErrorMessage = "Postal code cannot exceed 10 characters.")]
    public string? PostalCode { get; set; }

    [MaxLength(15, ErrorMessage = "GSTIN must be 15 characters.")]
    public string? Gstin { get; set; }

    [MaxLength(100, ErrorMessage = "Contact person cannot exceed 100 characters.")]
    public string? ContactPersonName { get; set; }

    [MaxLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
    public string? PhoneNumber { get; set; }

    [MaxLength(20, ErrorMessage = "Mobile number cannot exceed 20 characters.")]
    public string? MobileNumber { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// A drag-and-drop drop, described by its neighbours rather than a computed
/// position, so the server picks the value.
/// </summary>
public class ReorderRequest
{
    public long MovedId { get; set; }

    public long? PreviousId { get; set; }

    public long? NextId { get; set; }
}

public enum SaveMasterOutcome
{
    Ok = 0,
    NotFound = 1,
    DuplicateCode = 2,
    DuplicateName = 3,
    InUse = 4,
    SystemRowUndeletable = 5,
    /// <summary>A category cannot be moved inside its own subtree.</summary>
    CategoryCycle = 6,
    /// <summary>Three levels is the limit.</summary>
    CategoryTooDeep = 7,
    HasChildren = 8,
    InvalidValue = 9,
    /// <summary>The only default warehouse cannot be deactivated.</summary>
    DefaultWarehouseLocked = 10,
}
