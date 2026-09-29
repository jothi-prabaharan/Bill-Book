using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Customer.Entity.TableEntities;

public class CampaignLink : OrgScopedEntity
{
    public long CampaignLinkId { get; set; }

    public long CampaignId { get; set; }

    [Required(ErrorMessage = "Original URL is required.")]
    [MaxLength(2048, ErrorMessage = "Original URL cannot exceed 2048 characters.")]
    public string OriginalUrl { get; set; } = null!;

    [Required(ErrorMessage = "Tracking token is required.")]
    [MaxLength(64, ErrorMessage = "Tracking token cannot exceed 64 characters.")]
    public string TrackingToken { get; set; } = null!;

    public int ClickCount { get; set; }

    public DateTimeOffset? FirstClickedAt { get; set; }

    public DateTimeOffset? LastClickedAt { get; set; }
}
