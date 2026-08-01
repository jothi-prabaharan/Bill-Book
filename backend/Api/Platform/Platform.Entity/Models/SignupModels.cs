using System.ComponentModel.DataAnnotations;

namespace Platform.Entity.Models;

/// <summary>Public trial signup — one form that provisions a whole tenant.</summary>
public class SignupRequest
{
    // ---- Account -----------------------------------------------------------

    [Required(ErrorMessage = "Display name is required.")]
    [MaxLength(200, ErrorMessage = "Display name cannot exceed 200 characters.")]
    public string DisplayName { get; set; } = null!;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    [MaxLength(200, ErrorMessage = "Email cannot exceed 200 characters.")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = null!;

    [MaxLength(20, ErrorMessage = "Mobile number cannot exceed 20 characters.")]
    public string? MobileNumber { get; set; }

    // ---- Company / first organization -------------------------------------

    [Required(ErrorMessage = "Company name is required.")]
    [MaxLength(200, ErrorMessage = "Company name cannot exceed 200 characters.")]
    public string CompanyName { get; set; } = null!;

    [Required(ErrorMessage = "Organization name is required.")]
    [MaxLength(200, ErrorMessage = "Organization name cannot exceed 200 characters.")]
    public string OrganizationName { get; set; } = null!;

    [Range(1, 12, ErrorMessage = "Financial year start month must be between 1 and 12.")]
    public int FinancialYearStartMonth { get; set; } = 4;

    [MaxLength(3, ErrorMessage = "Base currency must be a 3-letter code.")]
    public string? BaseCurrency { get; set; }

    // ---- Statutory (optional at signup) ------------------------------------

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

    // ---- Location ----------------------------------------------------------

    [Required(ErrorMessage = "Country is required.")]
    public int CountryId { get; set; }

    public int? StateId { get; set; }

    [MaxLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    public string? City { get; set; }

    [MaxLength(10, ErrorMessage = "Postal code cannot exceed 10 characters.")]
    public string? PostalCode { get; set; }
}

public class SignupResponse
{
    public Guid CustomerId { get; set; }

    public string CustomerCode { get; set; } = null!;

    public string Message { get; set; } = null!;
}

/// <summary>Polled by the signup screen until CanLogin is true.</summary>
public class CustomerStatusResponse
{
    public Guid CustomerId { get; set; }

    public string CustomerStatus { get; set; } = null!;

    public string DatabaseStatus { get; set; } = null!;

    public bool CanLogin { get; set; }
}

/// <summary>
/// Shape returned by GET /internal/orgs/{orgId}/context — must match the
/// OrgContext the Identity service deserializes.
/// </summary>
public class OrgContextResponse
{
    public Guid OrgId { get; set; }

    public Guid CustomerId { get; set; }

    public string OrgName { get; set; } = null!;

    public bool DatabaseReady { get; set; }

    public string LicenseStatus { get; set; } = null!;

    public DateOnly? LicenseExpiry { get; set; }

    public int MaxUsers { get; set; }

    /// <summary>
    /// The branch's financial year start month — 4 for India. Carried here
    /// because every service composing a document number needs it, and it lives
    /// in the master database where none of them can read it directly.
    /// </summary>
    public int FinancialYearStartMonth { get; set; }
}
