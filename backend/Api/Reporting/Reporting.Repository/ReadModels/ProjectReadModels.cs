#pragma warning disable CS8618
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

// -----------------------------------------------------------------------------
// Project accounting (acc) read models, for the project reports (TK-107).
// Enum columns are stored by name in acc, so they are read as their names.
// -----------------------------------------------------------------------------

/// <summary><c>acc.Projects</c>, read-only.</summary>
public class ProjectRead : OrgScopedEntity
{
    public long ProjectId { get; set; }
    public string ProjectCode { get; set; }
    public string ProjectName { get; set; }
    public long? ContactId { get; set; }

    /// <summary><c>FixedFee</c>, <c>TimeAndMaterials</c> or <c>NonBillable</c>.</summary>
    public string BillingMethod { get; set; }

    /// <summary><c>ProjectRate</c>, <c>TaskRate</c> or <c>UserRate</c>; null on a project that bills no hours.</summary>
    public string? RateBasis { get; set; }

    public decimal? HourlyRate { get; set; }
    public decimal? BudgetAmount { get; set; }

    /// <summary><c>Active</c>, <c>OnHold</c>, <c>Completed</c> or <c>Cancelled</c>.</summary>
    public string Status { get; set; }
}

/// <summary><c>acc.ProjectTasks</c>, read-only.</summary>
public class ProjectTaskRead : OrgScopedEntity
{
    public long ProjectTaskId { get; set; }
    public long ProjectId { get; set; }
    public string TaskName { get; set; }
    public decimal? HourlyRate { get; set; }
    public decimal? BudgetHours { get; set; }
}

/// <summary><c>acc.ProjectMembers</c>, read-only.</summary>
public class ProjectMemberRead : OrgScopedEntity
{
    public long ProjectMemberId { get; set; }
    public long ProjectId { get; set; }
    public Guid UserId { get; set; }
    public decimal? HourlyRate { get; set; }
}

/// <summary><c>acc.ProjectMilestones</c>, read-only.</summary>
public class ProjectMilestoneRead : OrgScopedEntity
{
    public long ProjectMilestoneId { get; set; }
    public long ProjectId { get; set; }
    public decimal Amount { get; set; }

    /// <summary>The invoice that billed it; null while unbilled.</summary>
    public long? InvoiceId { get; set; }
}

/// <summary><c>acc.TimeEntries</c>, read-only.</summary>
public class TimeEntryRead : OrgScopedEntity
{
    public long TimeEntryId { get; set; }
    public long ProjectId { get; set; }
    public long ProjectTaskId { get; set; }
    public Guid UserId { get; set; }
    public DateOnly WorkDate { get; set; }
    public decimal Hours { get; set; }
    public bool IsBillable { get; set; }

    /// <summary><c>Unbilled</c>, <c>Billed</c> or <c>WrittenOff</c>.</summary>
    public string BillingStatus { get; set; }
}
