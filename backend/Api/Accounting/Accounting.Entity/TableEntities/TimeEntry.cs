using System.ComponentModel.DataAnnotations;
using Accounting.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>
/// Hours a user worked on a project task (TK-106). For billing and job cost,
/// not attendance: HRMS keeps attendance. A user logs only their own time
/// unless they hold <c>projects.edit</c>, and at most 24 hours a day across
/// entries. An hour billed on a posted invoice is claimed by it and cannot be
/// billed again until that invoice is voided.
/// </summary>
public class TimeEntry : OrgScopedEntity
{
    public long TimeEntryId { get; set; }

    public long ProjectId { get; set; }

    public long ProjectTaskId { get; set; }

    /// <summary>Who worked. A plain Guid: users live in the master database.</summary>
    public Guid UserId { get; set; }

    public DateOnly WorkDate { get; set; }

    [Range(typeof(decimal), "0.01", "24", ErrorMessage = "Hours must be more than 0 and at most 24.")]
    public decimal Hours { get; set; }

    /// <summary>Printed on the invoice line when billed.</summary>
    [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    public string? Notes { get; set; }

    /// <summary>From the task when logged, changeable per entry.</summary>
    public bool IsBillable { get; set; } = true;

    public TimeBillingStatus BillingStatus { get; set; } = TimeBillingStatus.Unbilled;

    /// <summary>The invoice that billed it, set by the claim. No FK: invoices live in Sales.</summary>
    public long? InvoiceId { get; set; }
}
