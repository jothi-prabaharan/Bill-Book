using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Entities;
using Shared.Kernel.Validation;

namespace Platform.Entity.TableEntities;

/// <summary>
/// A place the organization trades from — a shop, a godown, a second showroom.
///
/// In the master database rather than the customer's, next to the Organization
/// it belongs to, because that is the only place the two can hold a real foreign
/// key. Everything downstream — <c>inv.Warehouses</c>, <c>acc.JournalDetails</c>,
/// <c>acc.JournalLedger</c>, <c>acc.NumberingSeries</c> — stores the id as an
/// unenforced cross-database reference, the same way Contacts stores a CountryId.
///
/// A branch is a <b>reporting and numbering dimension, never a stock boundary</b>.
/// Stock is one shared pool across every branch; splitting it here is what makes
/// weighted average cost differ per location and the totals stop reconciling.
/// </summary>
public class Branch : AuditableEntity
{
    public long BranchId { get; set; }

    public Guid OrgId { get; set; }

    /// <summary>
    /// Short code, copied onto a numbering series rather than read from it —
    /// composing a document number must never reach into the master database.
    /// </summary>
    [Required(ErrorMessage = "Branch code is required.")]
    [MaxLength(10, ErrorMessage = "Branch code cannot exceed 10 characters.")]
    public string BranchCode { get; set; } = null!;

    [Required(ErrorMessage = "Branch name is required.")]
    [MaxLength(100, ErrorMessage = "Branch name cannot exceed 100 characters.")]
    public string BranchName { get; set; } = null!;

    /// <summary>Exactly one per organization — where a document lands with nothing chosen.</summary>
    public bool IsHeadOffice { get; set; }

    /// <summary>
    /// Set only when this branch holds a registration of its own. A branch in
    /// another state needs one; a second shop in the same city does not, and
    /// leaving it null means the organization's GSTIN applies.
    /// </summary>
    [MaxLength(15, ErrorMessage = "GSTIN cannot exceed 15 characters.")]
    public string? Gstin { get; set; }

    [MaxLength(200, ErrorMessage = "Address line 1 cannot exceed 200 characters.")]
    public string? AddressLine1 { get; set; }

    [MaxLength(200, ErrorMessage = "Address line 2 cannot exceed 200 characters.")]
    public string? AddressLine2 { get; set; }

    [MaxLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    public string? City { get; set; }

    /// <summary>Unenforced id into mst.States — a different database, so no FK.</summary>
    public int? StateId { get; set; }

    [MaxLength(10, ErrorMessage = "Postal code cannot exceed 10 characters.")]
    public string? PostalCode { get; set; }

    /// <summary>Unenforced id into mst.Countries.</summary>
    public int? CountryId { get; set; }

    [MaxLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
    [Landline]
    public string? PhoneNumber { get; set; }

    [MaxLength(20, ErrorMessage = "Mobile number cannot exceed 20 characters.")]
    [Mobile]
    public string? MobileNumber { get; set; }

    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    [MaxLength(200, ErrorMessage = "Email cannot exceed 200 characters.")]
    public string? Email { get; set; }

    /// <summary>Drag order on the list, matching every other master.</summary>
    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
