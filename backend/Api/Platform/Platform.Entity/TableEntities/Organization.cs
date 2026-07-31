using System.ComponentModel.DataAnnotations;
using Platform.Entity.Enums;
using Shared.Kernel.Entities;
using Shared.Kernel.Validation;

namespace Platform.Entity.TableEntities;

/// <summary>A set of books. Many per Customer, sharing that Customer's database, separated by OrgId.</summary>
public class Organization : AuditableEntity
{
    public Guid OrgId { get; set; }

    public Guid CustomerId { get; set; }

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
