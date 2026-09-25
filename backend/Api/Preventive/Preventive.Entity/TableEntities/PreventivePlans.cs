using System.ComponentModel.DataAnnotations;
using Preventive.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Preventive.Entity.TableEntities;

/// <summary>A recurring maintenance job for an asset or a space (S7, TK-67).</summary>
public class PreventivePlan : OrgScopedEntity
{
    public long PreventivePlanId { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
    public string Name { get; set; } = null!;

    /// <summary>Unenforced: <c>fac.FacilityAssets</c>. At least one of this and <see cref="SpaceId"/>.</summary>
    public long? FacilityAssetId { get; set; }

    /// <summary>Unenforced: <c>fac.Spaces</c>.</summary>
    public long? SpaceId { get; set; }

    public Frequency Frequency { get; set; } = Frequency.Monthly;

    [Range(1, 100, ErrorMessage = "The interval must be between 1 and 100.")]
    public int Interval { get; set; } = 1;

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    /// <summary>The due date of the next occurrence to generate. Advanced by a guarded update, which is the claim.</summary>
    public DateOnly NextDueDate { get; set; }

    /// <summary>Raise the work order this many days before the due date.</summary>
    [Range(0, 365, ErrorMessage = "Lead days must be between 0 and 365.")]
    public int LeadDays { get; set; }

    /// <summary>Unenforced: <c>hrm.Employees</c>, checked through Hrm.</summary>
    public long? DefaultAssigneeEmployeeId { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// One due date of a plan. Unique on plan and due date, which is what makes
/// generation idempotent: a second run finds the row and adds nothing.
/// </summary>
public class PreventiveOccurrence : OrgScopedEntity
{
    public long PreventiveOccurrenceId { get; set; }

    public long PreventivePlanId { get; set; }

    public DateOnly DueDate { get; set; }

    /// <summary>Unenforced: <c>wrk.WorkOrders</c>.</summary>
    public long? WorkOrderId { get; set; }

    [MaxLength(30, ErrorMessage = "Work order number cannot exceed 30 characters.")]
    public string? WorkOrderNo { get; set; }

    public OccurrenceStatus OccurrenceStatus { get; set; } = OccurrenceStatus.Scheduled;
}
