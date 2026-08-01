using System.ComponentModel.DataAnnotations;
using Platform.Entity.Enums;
using Shared.Kernel.Entities;
using Shared.Kernel.Validation;

namespace Platform.Entity.TableEntities;

/// <summary>
/// A branch — one place the business trades from, and one complete set of books.
///
/// The Customer is the head office: the account, the billing relationship, and
/// the owner of one physical database. Every Organization beneath it is a branch
/// sharing that database, separated by <c>OrgId</c>.
///
/// <b>A branch is a hard data boundary, not a reporting tag.</b> <c>OrgId</c> is
/// the EF query filter and the Postgres row-level security policy on every
/// per-customer table, so each branch keeps its own items, contacts, stock,
/// chart of accounts and numbering. Nothing crosses between them.
/// </summary>
public class Organization : AuditableEntity
{
    public Guid OrgId { get; set; }

    /// <summary>The head office this branch belongs to.</summary>
    public Guid CustomerId { get; set; }

    /// <summary>
    /// Short code for the branch, unique within the head office. Copied onto a
    /// numbering series so a generated number can name where it was written —
    /// <c>INV/2526/CHN/00042</c>.
    /// </summary>
    [Required(ErrorMessage = "Branch code is required.")]
    [MaxLength(10, ErrorMessage = "Branch code cannot exceed 10 characters.")]
    public string OrgCode { get; set; } = null!;

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
    public string Name { get; set; } = null!;

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

    public TenantStatus Status { get; set; } = TenantStatus.Provisioning;

    [MaxLength(200, ErrorMessage = "Address line 1 cannot exceed 200 characters.")]
    public string? AddressLine1 { get; set; }

    [MaxLength(200, ErrorMessage = "Address line 2 cannot exceed 200 characters.")]
    public string? AddressLine2 { get; set; }

    [MaxLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    public string? City { get; set; }

    public int? StateId { get; set; }

    [MaxLength(10, ErrorMessage = "Postal code cannot exceed 10 characters.")]
    public string? PostalCode { get; set; }

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
}
