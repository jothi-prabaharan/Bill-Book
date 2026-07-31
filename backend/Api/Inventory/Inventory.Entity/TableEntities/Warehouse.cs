using System.ComponentModel.DataAnnotations;
using Inventory.Entity.Enums;
using Shared.Kernel.Tenancy;
using Shared.Kernel.Validation;

namespace Inventory.Entity.TableEntities;

/// <summary>
/// A stock location. A reporting and movement dimension only — stock is one
/// shared pool across every warehouse and branch, and weighted average cost is
/// company-wide. Per-warehouse quantities come from aggregating movements, never
/// from a separate cost pool.
/// </summary>
public class Warehouse : OrgScopedEntity
{
    public long WarehouseId { get; set; }

    [Required(ErrorMessage = "Warehouse code is required.")]
    [MaxLength(20, ErrorMessage = "Warehouse code cannot exceed 20 characters.")]
    public string WarehouseCode { get; set; } = null!;

    [Required(ErrorMessage = "Warehouse name is required.")]
    [MaxLength(100, ErrorMessage = "Warehouse name cannot exceed 100 characters.")]
    public string WarehouseName { get; set; } = null!;

    public WarehouseType WarehouseType { get; set; } = WarehouseType.Store;

    /// <summary>Reporting dimension. Unenforced — branches live in the master database.</summary>
    public long? BranchId { get; set; }

    /// <summary>Cold chain is not a note: breaking it makes the stock unsaleable.</summary>
    public StorageCondition StorageType { get; set; } = StorageCondition.Ambient;

    [MaxLength(200, ErrorMessage = "Address line 1 cannot exceed 200 characters.")]
    public string? AddressLine1 { get; set; }

    [MaxLength(200, ErrorMessage = "Address line 2 cannot exceed 200 characters.")]
    public string? AddressLine2 { get; set; }

    [MaxLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    public string? City { get; set; }

    /// <summary>
    /// Unenforced reference to mst.States. A warehouse in another state changes
    /// the place of supply for goods despatched from it.
    /// </summary>
    public int? StateId { get; set; }

    public int? CountryId { get; set; }

    [MaxLength(10, ErrorMessage = "Postal code cannot exceed 10 characters.")]
    public string? PostalCode { get; set; }

    /// <summary>A warehouse in another state carries its own registration.</summary>
    [MaxLength(15, ErrorMessage = "GSTIN must be 15 characters.")]
    public string? Gstin { get; set; }

    [MaxLength(100, ErrorMessage = "Contact person cannot exceed 100 characters.")]
    public string? ContactPersonName { get; set; }

    [MaxLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
    [Landline]
    public string? PhoneNumber { get; set; }

    [MaxLength(20, ErrorMessage = "Mobile number cannot exceed 20 characters.")]
    [Mobile]
    public string? MobileNumber { get; set; }

    /// <summary>At most one per organization, enforced by a filtered unique index.</summary>
    public bool IsDefault { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
