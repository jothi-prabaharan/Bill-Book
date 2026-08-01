using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Validation;

namespace Platform.Entity.Models;

public class BranchListItem
{
    public long BranchId { get; set; }

    public string BranchCode { get; set; } = null!;

    public string BranchName { get; set; } = null!;

    public bool IsHeadOffice { get; set; }

    public string? Gstin { get; set; }

    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? City { get; set; }

    public int? StateId { get; set; }

    public string? PostalCode { get; set; }

    public int? CountryId { get; set; }

    public string? PhoneNumber { get; set; }

    public string? MobileNumber { get; set; }

    public string? Email { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }
}

public class SaveBranchRequest
{
    /// <summary>Required — a branch code is short enough to type and is quoted on documents.</summary>
    [Required(ErrorMessage = "Branch code is required.")]
    [MaxLength(10, ErrorMessage = "Branch code cannot exceed 10 characters.")]
    public string BranchCode { get; set; } = null!;

    [Required(ErrorMessage = "Branch name is required.")]
    [MaxLength(100, ErrorMessage = "Branch name cannot exceed 100 characters.")]
    public string BranchName { get; set; } = null!;

    public bool IsHeadOffice { get; set; }

    [MaxLength(15, ErrorMessage = "GSTIN cannot exceed 15 characters.")]
    public string? Gstin { get; set; }

    [MaxLength(200, ErrorMessage = "Address line 1 cannot exceed 200 characters.")]
    public string? AddressLine1 { get; set; }

    [MaxLength(200, ErrorMessage = "Address line 2 cannot exceed 200 characters.")]
    public string? AddressLine2 { get; set; }

    [MaxLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    public string? City { get; set; }

    public int? StateId { get; set; }

    [MaxLength(10, ErrorMessage = "Postal code cannot exceed 10 characters.")]
    public string? PostalCode { get; set; }

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

    public bool IsActive { get; set; } = true;
}

public enum SaveBranchOutcome
{
    Ok = 0,
    NotFound = 1,
    /// <summary>The caller's org_id claim does not match the org in the route.</summary>
    Forbidden = 2,
    DuplicateCode = 3,
    DuplicateName = 4,
    /// <summary>The GSTIN's first two digits do not match the chosen state's code.</summary>
    GstinStateMismatch = 5,
    /// <summary>Deactivating or demoting the only head office.</summary>
    HeadOfficeRequired = 6,
    OrganizationNotFound = 7,
}

public sealed record SaveBranchResult(SaveBranchOutcome Outcome, long? BranchId);
