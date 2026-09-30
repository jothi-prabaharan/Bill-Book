using System.ComponentModel.DataAnnotations;
using Customer.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Customer.Entity.TableEntities;

public class AutomationEnrolment : OrgScopedEntity
{
    public long AutomationEnrolmentId { get; set; }

    public long AutomationId { get; set; }

    public long? LeadId { get; set; }

    public long? ContactId { get; set; }

    [Required(ErrorMessage = "Email is required.")]
    [MaxLength(254, ErrorMessage = "Email cannot exceed 254 characters.")]
    public string Email { get; set; } = null!;

    public int CurrentStep { get; set; }

    public DateTimeOffset NextRunAt { get; set; }

    public AutomationEnrolmentStatus Status { get; set; } = AutomationEnrolmentStatus.Active;

    [MaxLength(100, ErrorMessage = "Exit reason cannot exceed 100 characters.")]
    public string? ExitReason { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}
