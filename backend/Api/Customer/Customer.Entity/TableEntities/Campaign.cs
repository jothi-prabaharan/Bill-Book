using System.ComponentModel.DataAnnotations;
using Customer.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Customer.Entity.TableEntities;

public class Campaign : OrgScopedEntity
{
    public long CampaignId { get; set; }

    [Required(ErrorMessage = "Campaign name is required.")]
    [MaxLength(150, ErrorMessage = "Campaign name cannot exceed 150 characters.")]
    public string Name { get; set; } = null!;

    public CampaignChannel Channel { get; set; } = CampaignChannel.Email;

    public long EmailTemplateId { get; set; }

    [MaxLength(200, ErrorMessage = "Subject snapshot cannot exceed 200 characters.")]
    public string? SubjectSnapshot { get; set; }

    public string? HtmlBodySnapshot { get; set; }

    public string? TextBodySnapshot { get; set; }

    public long AudienceId { get; set; }

    [Required(ErrorMessage = "From name is required.")]
    [MaxLength(100, ErrorMessage = "From name cannot exceed 100 characters.")]
    public string FromName { get; set; } = null!;

    [MaxLength(254, ErrorMessage = "Reply-to email cannot exceed 254 characters.")]
    public string? ReplyTo { get; set; }

    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;

    public DateTimeOffset? ScheduledAt { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    public int TotalRecipients { get; set; }

    public int TotalSent { get; set; }

    public int TotalFailed { get; set; }

    public int TotalSuppressed { get; set; }

    public int TotalOpened { get; set; }

    public int TotalClicked { get; set; }

    public int TotalUnsubscribed { get; set; }
}
