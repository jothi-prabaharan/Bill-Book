using Shared.Kernel.Tenancy;
using System.ComponentModel.DataAnnotations;

namespace Customer.Entity.TableEntities;

public enum ServicePlanStatus
{
    Active = 1,
    Paused = 2,
    Ended = 3
}

public enum ServicePlanInterval
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Yearly = 4
}

public class ServicePlan : OrgScopedEntity
{
    public long ServicePlanId { get; set; }

    public long ContactId { get; set; }

    public long? ItemId { get; set; }
    public long? AssetId { get; set; }

    public ServicePlanInterval Interval { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    
    public int LeadDays { get; set; }
    public DateOnly NextDueDate { get; set; }

    public ServicePlanStatus Status { get; set; }
}
