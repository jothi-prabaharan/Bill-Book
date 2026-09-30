using Shared.Kernel.Audit;
using System.ComponentModel.DataAnnotations;
using Customer.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Customer.Entity.TableEntities;

[NotAudited]
public class CampaignRecipient : OrgScopedEntity
{
    public long CampaignRecipientId { get; set; }

    public long CampaignId { get; set; }

    public long? LeadId { get; set; }

    public long? ContactId { get; set; }

    public long? ContactPersonId { get; set; }

    [Required(ErrorMessage = "Email is required.")]
    [MaxLength(254, ErrorMessage = "Email cannot exceed 254 characters.")]
    public string Email { get; set; } = null!;

    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
    public string? Name { get; set; }

    public RecipientStatus Status { get; set; } = RecipientStatus.Queued;

    public DateTimeOffset? SentAt { get; set; }

    public DateTimeOffset? OpenedAt { get; set; }

    public DateTimeOffset? ClickedAt { get; set; }

    public DateTimeOffset? UnsubscribedAt { get; set; }

    [MaxLength(500, ErrorMessage = "Failure reason cannot exceed 500 characters.")]
    public string? FailureReason { get; set; }
}
