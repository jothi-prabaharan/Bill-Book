using System.ComponentModel.DataAnnotations;
using Customer.Entity.Enums;

namespace Customer.Entity.Models;

/// <summary>A service plan as the screen sends it (TK-140).</summary>
public class SaveServicePlanRequest
{
    [Required(ErrorMessage = "Name the plan.")]
    [MaxLength(100, ErrorMessage = "The name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    [Range(1, long.MaxValue, ErrorMessage = "Choose the customer.")]
    public long ContactId { get; set; }

    public long? ItemId { get; set; }

    public long? AssetId { get; set; }

    [EnumDataType(typeof(ServicePlanInterval), ErrorMessage = "Choose how often it is due.")]
    public ServicePlanInterval Interval { get; set; } = ServicePlanInterval.Monthly;

    [Range(1, 24, ErrorMessage = "Repeat every 1 to 24 periods.")]
    public int Every { get; set; } = 1;

    /// <summary>When the plan starts. The first service falls due on it unless <see cref="NextDueDate"/> says otherwise.</summary>
    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    [Range(0, 90, ErrorMessage = "Remind 0 to 90 days ahead.")]
    public int LeadDays { get; set; } = 7;

    public DateOnly? NextDueDate { get; set; }

    [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    public string? Notes { get; set; }
}

public class ServicePlanView
{
    public long ServicePlanId { get; set; }
    public string Name { get; set; } = null!;
    public long ContactId { get; set; }
    public long? ItemId { get; set; }
    public long? AssetId { get; set; }
    public string Interval { get; set; } = null!;
    public int Every { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int LeadDays { get; set; }
    public DateOnly NextDueDate { get; set; }
    public DateOnly? LastServicedOn { get; set; }
    public long? LastTicketId { get; set; }
    public string? LastTicketStatus { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = null!;

    /// <summary>Days until the next service; negative when it is late.</summary>
    public int DaysToDue { get; set; }
}

public class CompleteServiceRequest
{
    public DateOnly CompletedOn { get; set; }
}

public enum ServicePlanOutcome
{
    Ok = 0,
    NotFound = 1,
    Invalid = 2,
    Conflict = 3,
}

public sealed record ServicePlanResult(ServicePlanOutcome Outcome, long Id = 0, string? Detail = null);

/// <summary>The worker's call: which customers to remind today, as messages ready to send.</summary>
public class RunServiceRemindersRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    public DateOnly? AsOf { get; set; }
}

/// <summary>One reminder, addressed and worded. The worker only sends it.</summary>
public class ServiceReminderMessage
{
    /// <summary>Fixed by branch, plan and due date, so a rerun sends nothing twice.</summary>
    public string MessageId { get; set; } = null!;
    public long ServicePlanId { get; set; }
    public string ToEmail { get; set; } = null!;
    public string? ToName { get; set; }
    public string Subject { get; set; } = null!;
    public string HtmlBody { get; set; } = null!;
    public string? TextBody { get; set; }
}

public class RunServiceRemindersResponse
{
    public List<ServiceReminderMessage> Messages { get; set; } = [];

    /// <summary>Due, but the customer has no email address.</summary>
    public int NoEmail { get; set; }

    /// <summary>Due, but the customer unsubscribed.</summary>
    public int Unsubscribed { get; set; }

    /// <summary>Master could not be read, so nobody is reminded this run.</summary>
    public bool Stopped { get; set; }
}
