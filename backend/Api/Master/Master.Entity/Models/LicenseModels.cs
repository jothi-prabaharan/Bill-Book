using System.ComponentModel.DataAnnotations;

namespace Master.Entity.Models;

public class LicenseDto
{
    public Guid LicenseId { get; set; }
    public Guid CustomerId { get; set; }

    /// <summary>The app the licence is for (TK-43).</summary>
    public string App { get; set; } = string.Empty;
    public string LicenseType { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public int MaxUsers { get; set; }
    public int MaxOrganizations { get; set; }
    public bool IsActive { get; set; }
    public int GraceDays { get; set; }
}

public class RenewLicenseRequest
{
    [Required(ErrorMessage = "New expiry date is required.")]
    public DateOnly NewExpiryDate { get; set; }

    /// <summary>The app whose licence is renewed, by name. Omitted means RetailErp (TK-43).</summary>
    [RegularExpression("(?i)^(RetailErp|School|Hrms|Payroll)$", ErrorMessage = "App must be RetailErp, School, Hrms or Payroll.")]
    public string? App { get; set; }
}

/// <summary>One app on the Applications page (TK-44): whether the customer holds its licence, and which.</summary>
public class ApplicationRow
{
    public string App { get; set; } = string.Empty;

    public bool Licensed { get; set; }

    public string? LicenseType { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public bool IsActive { get; set; }
}
