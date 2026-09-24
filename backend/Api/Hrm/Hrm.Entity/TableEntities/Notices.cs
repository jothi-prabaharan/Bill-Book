using System.ComponentModel.DataAnnotations;
using Hrm.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Hrm.Entity.TableEntities;

/// <summary>A notice to everyone, or to one department, location or grade (H1, TK-48).</summary>
public class Announcement : OrgScopedEntity
{
    public long AnnouncementId { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
    public string Title { get; set; } = null!;

    [Required(ErrorMessage = "Body is required.")]
    [MaxLength(4000, ErrorMessage = "Body cannot exceed 4000 characters.")]
    public string Body { get; set; } = null!;

    public DateOnly PublishDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public AnnouncementAudience Audience { get; set; } = AnnouncementAudience.Everyone;

    /// <summary>The department, location or grade named by <see cref="Audience"/>; null for everyone.</summary>
    public long? AudienceRefId { get; set; }

    public bool IsPinned { get; set; }
}

/// <summary>A policy employees read, and may have to acknowledge.</summary>
public class PolicyDocument : OrgScopedEntity
{
    public long PolicyDocumentId { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
    public string Title { get; set; } = null!;

    [Required(ErrorMessage = "The file is required.")]
    [MaxLength(500, ErrorMessage = "File key cannot exceed 500 characters.")]
    public string AttachmentKey { get; set; } = null!;

    public DateOnly EffectiveDate { get; set; }

    public bool IsAcknowledgementRequired { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>That one employee has read one policy. One per employee per policy.</summary>
public class PolicyAcknowledgement : OrgScopedEntity
{
    public long PolicyAcknowledgementId { get; set; }

    public long PolicyDocumentId { get; set; }

    public long EmployeeId { get; set; }

    public DateTimeOffset AcknowledgedAt { get; set; }
}
