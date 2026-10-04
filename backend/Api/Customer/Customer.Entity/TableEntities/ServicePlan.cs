using System.ComponentModel.DataAnnotations;
using Customer.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Customer.Entity.TableEntities;

/// <summary>
/// A customer's recurring service (TK-140): an AC serviced every six months, a
/// water purifier's filter changed every year. Completing a service moves the
/// next due date on; the worker emails the customer ahead of it.
/// </summary>
public class ServicePlan : OrgScopedEntity
{
    public long ServicePlanId { get; set; }

    [Required(ErrorMessage = "Name the plan.")]
    [MaxLength(100, ErrorMessage = "The name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    /// <summary>The customer, in Master's <c>con</c>. Checked through Master when the plan is saved.</summary>
    public long ContactId { get; set; }

    /// <summary>The item serviced, when there is one. Unenforced across services.</summary>
    public long? ItemId { get; set; }

    /// <summary>The customer's asset serviced, when there is one. Unenforced across services.</summary>
    public long? AssetId { get; set; }

    public ServicePlanInterval Interval { get; set; }

    /// <summary>Every 1 month, every 6 months, and so on.</summary>
    [Range(1, 24, ErrorMessage = "Repeat every 1 to 24 periods.")]
    public int Every { get; set; } = 1;

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    /// <summary>How many days before the due date the customer is reminded.</summary>
    [Range(0, 90, ErrorMessage = "Remind 0 to 90 days ahead.")]
    public int LeadDays { get; set; }

    public DateOnly NextDueDate { get; set; }

    public DateOnly? LastServicedOn { get; set; }

    /// <summary>The support ticket last raised for this plan's visit (D-48).</summary>
    public long? LastTicketId { get; set; }

    [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    public string? Notes { get; set; }

    public ServicePlanStatus Status { get; set; } = ServicePlanStatus.Active;
}
