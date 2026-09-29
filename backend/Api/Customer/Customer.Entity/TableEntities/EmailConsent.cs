using System.ComponentModel.DataAnnotations;
using Customer.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Customer.Entity.TableEntities;

public class EmailConsent : OrgScopedEntity
{
    public long EmailConsentId { get; set; }

    [Required(ErrorMessage = "Email is required.")]
    [MaxLength(254, ErrorMessage = "Email cannot exceed 254 characters.")]
    public string Email { get; set; } = null!;

    public ConsentStatus Status { get; set; } = ConsentStatus.Subscribed;

    public ConsentSource Source { get; set; } = ConsentSource.Staff;

    public DateTimeOffset ChangedAt { get; set; }
}
