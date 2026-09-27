using System.ComponentModel.DataAnnotations;

namespace Accounting.Entity.Models;

/// <summary>
/// A project as the form saves it (TK-104). Enums travel by name, parsed on
/// the server, because this service reads enums as numbers elsewhere and a
/// name is what the screen holds.
/// </summary>
public class SaveProjectRequest
{
    [Required(ErrorMessage = "Project name is required.")]
    [MaxLength(150, ErrorMessage = "Project name cannot exceed 150 characters.")]
    public string ProjectName { get; set; } = null!;

    [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description { get; set; }

    /// <summary>The client. Null for an internal project.</summary>
    public long? ContactId { get; set; }

    /// <summary>FixedFee, TimeAndMaterials or NonBillable.</summary>
    [Required(ErrorMessage = "Choose how the project is billed.")]
    public string BillingMethod { get; set; } = "TimeAndMaterials";

    /// <summary>ProjectRate, TaskRate or UserRate, for time and materials.</summary>
    public string? RateBasis { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "The hourly rate cannot be negative.")]
    public decimal? HourlyRate { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "The fixed fee cannot be negative.")]
    public decimal? FixedFee { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "The budget cannot be negative.")]
    public decimal? BudgetAmount { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    /// <summary>Active, OnHold, Completed or Cancelled. Active for a new project.</summary>
    public string Status { get; set; } = "Active";

    /// <summary>The client's billing currency. The branch's own when left empty.</summary>
    [MaxLength(3, ErrorMessage = "Currency code must be a 3-letter code.")]
    public string? CurrencyCode { get; set; }

    public List<SaveProjectTaskRequest> Tasks { get; set; } = [];

    public List<SaveProjectMemberRequest> Members { get; set; } = [];

    public List<SaveProjectMilestoneRequest> Milestones { get; set; } = [];
}

public class SaveProjectTaskRequest
{
    /// <summary>The task being edited; null for a new one. A task left out is deactivated, never deleted.</summary>
    public long? ProjectTaskId { get; set; }

    [Required(ErrorMessage = "Task name is required.")]
    [MaxLength(150, ErrorMessage = "Task name cannot exceed 150 characters.")]
    public string TaskName { get; set; } = null!;

    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "The hourly rate cannot be negative.")]
    public decimal? HourlyRate { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "Budget hours cannot be negative.")]
    public decimal? BudgetHours { get; set; }

    public bool IsBillable { get; set; } = true;

    public bool IsActive { get; set; } = true;
}

public class SaveProjectMemberRequest
{
    public Guid UserId { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "The hourly rate cannot be negative.")]
    public decimal? HourlyRate { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "The cost rate cannot be negative.")]
    public decimal? CostRate { get; set; }
}

public class SaveProjectMilestoneRequest
{
    /// <summary>The milestone being edited; null for a new one. A billed milestone is never changed or removed.</summary>
    public long? ProjectMilestoneId { get; set; }

    [Required(ErrorMessage = "Milestone name is required.")]
    [MaxLength(150, ErrorMessage = "Milestone name cannot exceed 150 characters.")]
    public string Name { get; set; } = null!;

    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "The amount cannot be negative.")]
    public decimal Amount { get; set; }

    public DateOnly? DueDate { get; set; }
}

public class ProjectListItem
{
    public long ProjectId { get; set; }

    public string ProjectCode { get; set; } = null!;

    public string ProjectName { get; set; } = null!;

    public long? ContactId { get; set; }

    public string BillingMethod { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public decimal? BudgetAmount { get; set; }

    public string CurrencyCode { get; set; } = null!;
}

public class ProjectView : ProjectListItem
{
    public string? Description { get; set; }

    public string? RateBasis { get; set; }

    public decimal? HourlyRate { get; set; }

    public decimal? FixedFee { get; set; }

    public List<ProjectTaskView> Tasks { get; set; } = [];

    public List<ProjectMemberView> Members { get; set; } = [];

    public List<ProjectMilestoneView> Milestones { get; set; } = [];
}

public class ProjectTaskView
{
    public long ProjectTaskId { get; set; }

    public string TaskName { get; set; } = null!;

    public decimal? HourlyRate { get; set; }

    public decimal? BudgetHours { get; set; }

    public bool IsBillable { get; set; }

    public bool IsActive { get; set; }
}

public class ProjectMemberView
{
    public Guid UserId { get; set; }

    public decimal? HourlyRate { get; set; }

    public decimal? CostRate { get; set; }
}

public class ProjectMilestoneView
{
    public long ProjectMilestoneId { get; set; }

    public string Name { get; set; } = null!;

    public decimal Amount { get; set; }

    public DateOnly? DueDate { get; set; }

    public long? InvoiceId { get; set; }
}

/// <summary>One ledger row tagged with a project, in base currency.</summary>
public class ProjectLedgerRow
{
    public long LedgerId { get; set; }

    public DateOnly LedgerDate { get; set; }

    public string AccountCode { get; set; } = null!;

    public string AccountName { get; set; } = null!;

    public string TransactionTypeCode { get; set; } = null!;

    public long TransactionId { get; set; }

    public string? DocumentNo { get; set; }

    public string? Description { get; set; }

    public decimal Debit { get; set; }

    public decimal Credit { get; set; }
}

public enum SaveProjectOutcome
{
    Ok = 0,
    NotFound = 1,

    /// <summary>A billing method, rate basis or status that is not one of the names.</summary>
    InvalidValue = 2,

    /// <summary>The PRJ series is missing, so no code could be taken.</summary>
    SeriesMissing = 3,

    /// <summary>A user named twice among the members, or the base currency could not be read.</summary>
    Refused = 4,
}

public sealed record SaveProjectResult(SaveProjectOutcome Outcome, long ProjectId = 0, string? Detail = null);

/// <summary>An entry on the timesheet (TK-106).</summary>
public class SaveTimeEntryRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose a project.")]
    public long ProjectId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose a task.")]
    public long ProjectTaskId { get; set; }

    /// <summary>Who worked. The signed-in user when left empty; someone else only with <c>projects.edit</c>.</summary>
    public Guid? UserId { get; set; }

    public DateOnly WorkDate { get; set; }

    [Range(typeof(decimal), "0.01", "24", ErrorMessage = "Hours must be more than 0 and at most 24.")]
    public decimal Hours { get; set; }

    [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    public string? Notes { get; set; }

    /// <summary>Whether the hours are to be billed. The task's own setting when left empty.</summary>
    public bool? IsBillable { get; set; }
}

public class TimeEntryView
{
    public long TimeEntryId { get; set; }

    public long ProjectId { get; set; }

    public string ProjectCode { get; set; } = null!;

    public string ProjectName { get; set; } = null!;

    public long ProjectTaskId { get; set; }

    public string TaskName { get; set; } = null!;

    public Guid UserId { get; set; }

    public DateOnly WorkDate { get; set; }

    public decimal Hours { get; set; }

    public string? Notes { get; set; }

    public bool IsBillable { get; set; }

    public string BillingStatus { get; set; } = null!;

    public long? InvoiceId { get; set; }
}

public enum TimeEntryOutcome
{
    Ok = 0,
    NotFound = 1,

    /// <summary>The project or task will not take time, or the day would pass 24 hours.</summary>
    Refused = 2,

    /// <summary>Another user's time, without <c>projects.edit</c>.</summary>
    NotYours = 3,

    /// <summary>Billed on an invoice, so fixed until that invoice is voided.</summary>
    Billed = 4,
}

public sealed record TimeEntryResult(TimeEntryOutcome Outcome, long TimeEntryId = 0, string? Detail = null);

/// <summary>What a project has left to bill (TK-106): the invoice's "Add project items" list.</summary>
public class ProjectUnbilledView
{
    public long ProjectId { get; set; }

    public long? ContactId { get; set; }

    public string BillingMethod { get; set; } = null!;

    public List<UnbilledTimeItem> Time { get; set; } = [];

    public List<UnbilledExpenseItem> Expenses { get; set; } = [];

    public List<UnbilledMilestoneItem> Milestones { get; set; } = [];
}

public class UnbilledTimeItem
{
    public long TimeEntryId { get; set; }

    public DateOnly WorkDate { get; set; }

    public Guid UserId { get; set; }

    public long ProjectTaskId { get; set; }

    public string TaskName { get; set; } = null!;

    public decimal Hours { get; set; }

    public string? Notes { get; set; }

    /// <summary>The hourly rate the project's rate basis gives this entry. Zero when none is set.</summary>
    public decimal Rate { get; set; }

    public decimal Amount { get; set; }
}

public class UnbilledExpenseItem
{
    public long SpendMoneyDetailId { get; set; }

    public DateOnly TransactionDate { get; set; }

    public string? TransactionNo { get; set; }

    public string? Memo { get; set; }

    /// <summary>What was paid, in base currency.</summary>
    public decimal Amount { get; set; }

    public decimal? MarkupPercent { get; set; }

    /// <summary>What it is charged on at: the amount plus the markup, to two decimals.</summary>
    public decimal BillableAmount { get; set; }
}

public class UnbilledMilestoneItem
{
    public long ProjectMilestoneId { get; set; }

    public string Name { get; set; } = null!;

    public decimal Amount { get; set; }

    public DateOnly? DueDate { get; set; }
}
