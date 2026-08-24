using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Customer;
using Shared.Kernel.Tenancy;

namespace Customer.Entity.TableEntities;

public class Lead : OrgScopedEntity
{
    public long LeadId { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
    public string Name { get; set; } = null!;

    [MaxLength(200, ErrorMessage = "Company name cannot exceed 200 characters.")]
    public string? CompanyName { get; set; }

    [MaxLength(50, ErrorMessage = "Phone cannot exceed 50 characters.")]
    public string? Phone { get; set; }

    [MaxLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
    public string? Email { get; set; }

    public LeadSource Source { get; set; } = LeadSource.Other;

    public LeadStatus Status { get; set; } = LeadStatus.New;

    public long? ConvertedContactId { get; set; }

    public DateTimeOffset? ConvertedAt { get; set; }
}
