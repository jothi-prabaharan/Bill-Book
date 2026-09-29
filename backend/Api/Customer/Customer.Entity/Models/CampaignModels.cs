using System.ComponentModel.DataAnnotations;
using Customer.Entity.Enums;
using Shared.Kernel.Customer;

namespace Customer.Entity.Models;

public sealed class EmailTemplateDto
{
    public long EmailTemplateId { get; set; }
    public string Name { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public string HtmlBody { get; set; } = null!;
    public string? TextBody { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
}

public sealed class SaveEmailTemplateRequest
{
    [Required(ErrorMessage = "Template name is required.")]
    [MaxLength(100, ErrorMessage = "Template name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "Subject is required.")]
    [MaxLength(200, ErrorMessage = "Subject cannot exceed 200 characters.")]
    public string Subject { get; set; } = null!;

    [Required(ErrorMessage = "HTML body is required.")]
    public string HtmlBody { get; set; } = null!;

    public string? TextBody { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class AudienceRules
{
    public LeadRule? Leads { get; set; }
    public ContactRule? Contacts { get; set; }
    public PurchaseHistoryRule? PurchaseHistory { get; set; }
    public Dictionary<string, string>? CustomFields { get; set; }
    public ManualListRule? ManualInclusions { get; set; }
    public ManualListRule? ManualExclusions { get; set; }
}

public sealed class LeadRule
{
    public List<LeadStatus>? Statuses { get; set; }
    public List<LeadSource>? Sources { get; set; }
    public DateTimeOffset? CreatedFrom { get; set; }
    public DateTimeOffset? CreatedTo { get; set; }
}

public sealed class ContactRule
{
    public bool? IsCustomer { get; set; }
    public bool? IsVendor { get; set; }
    public string? State { get; set; }
    public string? City { get; set; }
    public string? Category { get; set; }
}

public sealed class PurchaseHistoryRule
{
    public DateOnly? LastInvoiceBefore { get; set; }
    public DateOnly? LastInvoiceAfter { get; set; }
    public decimal? SalesMinAmount { get; set; }
    public decimal? SalesMaxAmount { get; set; }
    public DateOnly? SalesPeriodFrom { get; set; }
    public DateOnly? SalesPeriodTo { get; set; }
    public long? ItemCategoryId { get; set; }
}

public sealed class ManualListRule
{
    public List<string>? Emails { get; set; }
    public List<long>? ContactIds { get; set; }
    public List<long>? LeadIds { get; set; }
}

public sealed class AudienceDto
{
    public long AudienceId { get; set; }
    public string Name { get; set; } = null!;
    public AudienceRules Rules { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
}

public sealed class SaveAudienceRequest
{
    [Required(ErrorMessage = "Audience name is required.")]
    [MaxLength(100, ErrorMessage = "Audience name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public AudienceRules Rules { get; set; } = new();

    public bool IsActive { get; set; } = true;
}

public sealed class AudienceRecipientPreview
{
    public string Email { get; set; } = null!;
    public string? Name { get; set; }
    public string? SourceType { get; set; } // "Lead" or "Contact"
    public long? LeadId { get; set; }
    public long? ContactId { get; set; }
}

public sealed class AudiencePreviewResponse
{
    public int TotalSubscribedRecipients { get; set; }
    public int TotalSuppressedRecipients { get; set; }
    public List<AudienceRecipientPreview> SampleRecipients { get; set; } = [];
}

public sealed class EmailConsentDto
{
    public long EmailConsentId { get; set; }
    public string Email { get; set; } = null!;
    public ConsentStatus Status { get; set; }
    public ConsentSource Source { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
}

public sealed class SaveConsentRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "A valid email address is required.")]
    [MaxLength(254, ErrorMessage = "Email cannot exceed 254 characters.")]
    public string Email { get; set; } = null!;

    public ConsentStatus Status { get; set; } = ConsentStatus.Subscribed;

    public ConsentSource Source { get; set; } = ConsentSource.Staff;
}

public sealed class UnsubscribeTokenPayload
{
    public Guid CustomerId { get; set; }
    public Guid OrgId { get; set; }
    public string Email { get; set; } = null!;
}

public sealed class CampaignDto
{
    public long CampaignId { get; set; }
    public string Name { get; set; } = null!;
    public CampaignChannel Channel { get; set; }
    public long EmailTemplateId { get; set; }
    public string? TemplateName { get; set; }
    public long AudienceId { get; set; }
    public string? AudienceName { get; set; }
    public string FromName { get; set; } = null!;
    public string? ReplyTo { get; set; }
    public CampaignStatus Status { get; set; }
    public DateTimeOffset? ScheduledAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public int TotalRecipients { get; set; }
    public int TotalSent { get; set; }
    public int TotalFailed { get; set; }
    public int TotalSuppressed { get; set; }
    public int TotalOpened { get; set; }
    public int TotalClicked { get; set; }
    public int TotalUnsubscribed { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
}

public sealed class SaveCampaignRequest
{
    [Required(ErrorMessage = "Campaign name is required.")]
    [MaxLength(150, ErrorMessage = "Campaign name cannot exceed 150 characters.")]
    public string Name { get; set; } = null!;

    public CampaignChannel Channel { get; set; } = CampaignChannel.Email;

    [Range(1, long.MaxValue, ErrorMessage = "A valid Email Template is required.")]
    public long EmailTemplateId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "A valid Audience is required.")]
    public long AudienceId { get; set; }

    [Required(ErrorMessage = "From name is required.")]
    [MaxLength(100, ErrorMessage = "From name cannot exceed 100 characters.")]
    public string FromName { get; set; } = null!;

    [MaxLength(254, ErrorMessage = "Reply-to email cannot exceed 254 characters.")]
    [EmailAddress(ErrorMessage = "Reply-to must be a valid email address.")]
    public string? ReplyTo { get; set; }
}

public sealed class ScheduleCampaignRequest
{
    [Required(ErrorMessage = "Scheduled time is required.")]
    public DateTimeOffset ScheduledAt { get; set; }
}

public sealed class CampaignRecipientDto
{
    public long CampaignRecipientId { get; set; }
    public long CampaignId { get; set; }
    public long? LeadId { get; set; }
    public long? ContactId { get; set; }
    public string Email { get; set; } = null!;
    public string? Name { get; set; }
    public RecipientStatus Status { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? OpenedAt { get; set; }
    public DateTimeOffset? ClickedAt { get; set; }
    public DateTimeOffset? UnsubscribedAt { get; set; }
    public string? FailureReason { get; set; }
}

public sealed class CampaignLinkDto
{
    public long CampaignLinkId { get; set; }
    public long CampaignId { get; set; }
    public string OriginalUrl { get; set; } = null!;
    public string TrackingToken { get; set; } = null!;
    public int ClickCount { get; set; }
    public DateTimeOffset? FirstClickedAt { get; set; }
    public DateTimeOffset? LastClickedAt { get; set; }
}

public sealed class TrackingTokenPayload
{
    public Guid CustomerId { get; set; }
    public Guid OrgId { get; set; }
    public long CampaignId { get; set; }
    public long RecipientId { get; set; }
    public long? LinkId { get; set; }
}

