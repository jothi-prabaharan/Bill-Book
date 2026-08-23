using System.ComponentModel.DataAnnotations;

namespace Master.Entity.Models;

public class LicenseDto
{
    public Guid LicenseId { get; set; }
    public Guid CustomerId { get; set; }
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
    [Required]
    public DateOnly NewExpiryDate { get; set; }
}
