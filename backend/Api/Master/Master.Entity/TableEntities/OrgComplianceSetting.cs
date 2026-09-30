using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

/// <summary>
/// Per-branch statutory filing applicability and tracking (TK-119).
/// Schema mst, in the master database.
/// </summary>
public class OrgComplianceSetting : AuditableEntity
{
    public long OrgComplianceSettingId { get; set; }

    public Guid OrgId { get; set; }

    public int ComplianceDueDateId { get; set; }

    /// <summary>Null means default rule based on branch's GSTIN / TAN / CIN applies; true/false explicitly overrides.</summary>
    public bool? IsEnabled { get; set; }

    [Range(1, 30, ErrorMessage = "Reminder days before must be between 1 and 30.")]
    public int ReminderDaysBefore { get; set; } = 3;

    [MaxLength(20, ErrorMessage = "Last filed period cannot exceed 20 characters.")]
    public string? LastFiledPeriod { get; set; }

    public DateTimeOffset? LastFiledAt { get; set; }
}
