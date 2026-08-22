#pragma warning disable CS8618

using Shared.Kernel.Tenancy;
using Shared.Kernel.Validation;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// A stock location. A reporting and movement dimension only — stock is one
/// shared pool across every warehouse in the branch, and weighted average cost is
/// company-wide. Per-warehouse quantities come from aggregating movements, never
/// from a separate cost pool.
/// </summary>
public class WarehouseRead : OrgScopedEntity
{
    public long WarehouseId { get; set; }


    public string WarehouseCode { get; set; }
    public string WarehouseName { get; set; }
    public int WarehouseType { get; set; }
    /// <summary>Cold chain is not a note: breaking it makes the stock unsaleable.</summary>
    public int StorageType { get; set; }
    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? City { get; set; }

    /// <summary>
    /// Unenforced reference to mst.States. A warehouse in another state changes
    /// the place of supply for goods despatched from it.
    /// </summary>
    public int? StateId { get; set; }

    public int? CountryId { get; set; }

    public string? PostalCode { get; set; }

    /// <summary>A warehouse in another state carries its own registration.</summary>

    public string? Gstin { get; set; }

    public string? ContactPersonName { get; set; }


    public string? PhoneNumber { get; set; }


    public string? MobileNumber { get; set; }

    /// <summary>At most one per organization, enforced by a filtered unique index.</summary>
    public bool IsDefault { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }
}



