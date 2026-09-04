using System.ComponentModel.DataAnnotations;

namespace Master.Entity.Models;

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

    [MaxLength(200, ErrorMessage = "Address line 1 cannot exceed 200 characters.")]
    public string? AddressLine1 { get; set; }

    [MaxLength(200, ErrorMessage = "Address line 2 cannot exceed 200 characters.")]
    public string? AddressLine2 { get; set; }

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

    /// <summary>
    /// Same value as <see cref="CustomerStatus"/> — there is no longer a
    /// separate per-customer database to report on, only whether this
    /// customer's seeding has finished. Kept as its own field (renamed from
    /// DatabaseStatus) because the signup screen polls specifically for
    /// "Failed" here to stop polling and show an error.
    /// </summary>
    public string ProvisioningStatus { get; set; } = null!;

    public bool CanLogin { get; set; }
}

/// <summary>One row of the platform admin's customer list.</summary>
public class CustomerListItem
{
    public Guid CustomerId { get; set; }

    public string CustomerCode { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string BillingEmail { get; set; } = null!;

    public string PlanTier { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTimeOffset? CreatedAt { get; set; }
}

/// <summary>Outcome of an admin retrying a stuck customer's provisioning.</summary>
public enum RetryProvisioningOutcome
{
    Ok = 1,
    NotFound = 2,
    Failed = 3,
}

public sealed class RetryProvisioningResult
{
    public required RetryProvisioningOutcome Outcome { get; init; }

    public IReadOnlyList<string> UnseededServices { get; init; } = [];

    public static RetryProvisioningResult OkResult { get; } =
        new() { Outcome = RetryProvisioningOutcome.Ok };

    public static RetryProvisioningResult NotFoundResult { get; } =
        new() { Outcome = RetryProvisioningOutcome.NotFound };

    public static RetryProvisioningResult Failed(IReadOnlyList<string> unseeded) =>
        new() { Outcome = RetryProvisioningOutcome.Failed, UnseededServices = unseeded };
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

    /// <summary>
    /// When access to <em>this branch</em> ends — the earlier of the customer's
    /// licence expiry and the branch's own, not the licence date alone. It is
    /// the date the user is shown, so it has to be the one being enforced.
    /// </summary>
    public DateOnly? LicenseExpiry { get; set; }

    /// <summary>
    /// True when the branch's own date is what ends access, rather than the
    /// customer's licence. The two need different words on screen: one asks the
    /// customer to renew, the other tells them to talk to their head office.
    /// </summary>
    public bool ExpiryIsBranchLevel { get; set; }

    public int MaxUsers { get; set; }

    /// <summary>
    /// The branch's financial year start month — 4 for India. Carried here
    /// because every service composing a document number needs it, and it lives
    /// in the master database where none of them can read it directly.
    /// </summary>
    public int FinancialYearStartMonth { get; set; }

    /// <summary>
    /// The currency the branch keeps its books in. Here for the same reason as
    /// the financial year: every GL posting is denominated in it, and it lives
    /// in the master database that no per-customer service can read.
    /// </summary>
    public string BaseCurrency { get; set; } = null!;

    /// <summary>
    /// The 2-digit state code of the branch. Needed for GST place of supply calculations
    /// on documents, which happen in per-customer services that cannot read mst.Organizations.
    /// </summary>
    public string? StateCode { get; set; }

    /// <summary>
    /// Whether a discount reduces the taxable value.
    /// </summary>
    public bool DiscountBeforeTax { get; set; }

    /// <summary>
    /// The trade this branch is in — General, Pharma or Jewellery. Carried on
    /// every login so a per-customer service can default an item's profile and
    /// decide which screens to offer, without reading the master database.
    /// </summary>
    public string Vertical { get; set; } = "General";

    /// <summary>
    /// The branch's own GSTIN, as it must appear on a tax invoice.
    ///
    /// Here for the same reason as the state code and the base currency: the
    /// document that has to print it is raised in a per-customer service, and
    /// <c>mst.Organizations</c> is a database that service cannot read. Null on
    /// an unregistered branch, which prints as absent rather than as a
    /// placeholder.
    /// </summary>
    public string? Gstin { get; set; }

    /// <summary>The registered address, for the seller block on a printed document.</summary>
    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? City { get; set; }

    public string? PostalCode { get; set; }
}
