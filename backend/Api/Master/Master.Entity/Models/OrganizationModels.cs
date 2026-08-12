using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Validation;

namespace Master.Entity.Models;

/// <summary>A branch, as the branch list shows it.</summary>
public class OrganizationListItem
{
    public Guid OrgId { get; set; }

    public string OrgCode { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string BaseCurrency { get; set; } = null!;

    public int FinancialYearStartMonth { get; set; }

    public string? Gstin { get; set; }

    public string? Pan { get; set; }

    public string? Tan { get; set; }

    public string? Tin { get; set; }

    public string? Cin { get; set; }

    public string? UdyamNumber { get; set; }

    public string? LogoUrl { get; set; }

    /// <summary>When this branch's own access ends, or null if only the licence governs.</summary>
    public DateOnly? ExpiryDate { get; set; }

    /// <summary>True when it is running on a branch trial rather than the licence.</summary>
    public bool IsTrial { get; set; }

    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? City { get; set; }

    public int? StateId { get; set; }

    public string? PostalCode { get; set; }

    public int CountryId { get; set; }

    public string? PhoneNumber { get; set; }

    public string? MobileNumber { get; set; }

    public string? Email { get; set; }

    public string? Website { get; set; }

    public bool AllowFreeTextLines { get; set; } = true;

    public string DiscountLevel { get; set; } = "Line";

    public bool DiscountBeforeTax { get; set; } = true;

    /// <summary>Provisioning, Active, Suspended — a branch is only usable once Active.</summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// True for the first branch on the account. It cannot be deactivated,
    /// because an account with no usable branch has nowhere to sign in to.
    /// </summary>
    public bool IsFirst { get; set; }
}

public class SaveOrganizationRequest
{
    [Required(ErrorMessage = "Branch code is required.")]
    [MaxLength(10, ErrorMessage = "Branch code cannot exceed 10 characters.")]
    public string OrgCode { get; set; } = null!;

    [Required(ErrorMessage = "Branch name is required.")]
    [MaxLength(200, ErrorMessage = "Branch name cannot exceed 200 characters.")]
    public string Name { get; set; } = null!;

    /// <summary>Immutable once set — every posting in the branch converts to it.</summary>
    [Required(ErrorMessage = "Base currency is required.")]
    [MaxLength(3, ErrorMessage = "Base currency must be a 3-letter code.")]
    public string BaseCurrency { get; set; } = "INR";

    [Range(1, 12, ErrorMessage = "Financial year start month must be between 1 and 12.")]
    public int FinancialYearStartMonth { get; set; } = 4;

    [MaxLength(15, ErrorMessage = "GSTIN cannot exceed 15 characters.")]
    public string? Gstin { get; set; }

    [MaxLength(10, ErrorMessage = "PAN cannot exceed 10 characters.")]
    public string? Pan { get; set; }

    [MaxLength(10, ErrorMessage = "TAN cannot exceed 10 characters.")]
    public string? Tan { get; set; }

    [MaxLength(15, ErrorMessage = "TIN cannot exceed 15 characters.")]
    public string? Tin { get; set; }

    [MaxLength(21, ErrorMessage = "CIN cannot exceed 21 characters.")]
    public string? Cin { get; set; }

    [MaxLength(20, ErrorMessage = "Udyam number cannot exceed 20 characters.")]
    public string? UdyamNumber { get; set; }

    [MaxLength(500, ErrorMessage = "Logo URL cannot exceed 500 characters.")]
    public string? LogoUrl { get; set; }

    [MaxLength(200, ErrorMessage = "Address line 1 cannot exceed 200 characters.")]
    public string? AddressLine1 { get; set; }

    [MaxLength(200, ErrorMessage = "Address line 2 cannot exceed 200 characters.")]
    public string? AddressLine2 { get; set; }

    [MaxLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    public string? City { get; set; }

    public int? StateId { get; set; }

    [MaxLength(10, ErrorMessage = "Postal code cannot exceed 10 characters.")]
    public string? PostalCode { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Country is required.")]
    public int CountryId { get; set; }

    [MaxLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
    [Landline]
    public string? PhoneNumber { get; set; }

    [MaxLength(20, ErrorMessage = "Mobile number cannot exceed 20 characters.")]
    [Mobile]
    public string? MobileNumber { get; set; }

    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    [MaxLength(200, ErrorMessage = "Email cannot exceed 200 characters.")]
    public string? Email { get; set; }

    [MaxLength(200, ErrorMessage = "Website cannot exceed 200 characters.")]
    public string? Website { get; set; }

    /// <summary>Frozen once the branch has traded, like the base currency.</summary>
    public bool AllowFreeTextLines { get; set; } = true;

    /// <summary>Line, Header or Both. Frozen once the branch has traded.</summary>
    [Required(ErrorMessage = "Discount level is required.")]
    [MaxLength(10, ErrorMessage = "Discount level must be Line, Header or Both.")]
    public string DiscountLevel { get; set; } = "Line";

    /// <summary>Frozen once the branch has traded.</summary>
    public bool DiscountBeforeTax { get; set; } = true;
}

public enum SaveOrganizationOutcome
{
    Ok = 0,
    NotFound = 1,
    DuplicateCode = 2,
    DuplicateName = 3,
    /// <summary>The GSTIN's first two digits do not match the chosen state's code.</summary>
    GstinStateMismatch = 4,
    /// <summary>
    /// The licence allows fewer branches than this would make. No longer
    /// returned on create — a branch beyond the entitlement is created on a
    /// trial instead — but kept so an older client reading the number does not
    /// silently reinterpret it as something else.
    /// </summary>
    LicenceLimitReached = 5,
    /// <summary>The customer's database is not ready, so a branch cannot be seeded into it.</summary>
    DatabaseNotReady = 6,
    /// <summary>Created, but one or more services could not write its master data.</summary>
    SeedingFailed = 7,
    /// <summary>
    /// Created, and running on its own 30-day trial because the account's
    /// licence does not cover it. Success, not a failure — the caller says so
    /// rather than treating it as an error.
    /// </summary>
    CreatedOnTrial = 11,

    /// <summary>The first branch on an account cannot be deactivated.</summary>
    FirstOrganizationLocked = 8,
    /// <summary>Base currency is fixed once the branch exists — every posting converts to it.</summary>
    BaseCurrencyLocked = 9,

    /// <summary>
    /// A document setting changed after the branch had already traded. These
    /// decide how a document's tax and discount are computed, so changing one
    /// leaves earlier documents computed one way and later ones another.
    /// </summary>
    DocumentSettingsLocked = 12,

    /// <summary>Discount level was not Line, Header or Both.</summary>
    InvalidDiscountLevel = 13,
    InvalidValue = 10,
}

public sealed record SaveOrganizationResult(
    SaveOrganizationOutcome Outcome, Guid? OrgId, IReadOnlyList<string> UnseededServices);
