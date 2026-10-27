using System.ComponentModel.DataAnnotations;

namespace Reporting.Entity.Models;

/// <summary>
/// Request parameters for the Project Budget vs Actual report (TK-257).
/// AsOfDate is required (D-67).
/// </summary>
public sealed class ProjectBudgetVsActualReportRequest
{
    [Required(ErrorMessage = "As of date is required.")]
    public DateOnly? AsOfDate { get; set; }

    public string? Status { get; set; }

    public long? CustomerId { get; set; }

    public long? ProjectId { get; set; }

    public Guid? OrgId { get; set; }
}

/// <summary>
/// Budget vs actual hours for an individual task within a project.
/// </summary>
public sealed class ProjectTaskBudgetDto
{
    public long ProjectTaskId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public decimal? BudgetHours { get; set; }
    public decimal ActualHours { get; set; }
    public decimal? HoursVariance { get; set; }
    public decimal? HoursUsedPercent { get; set; }
    public bool IsOverBudget { get; set; }
}

/// <summary>
/// A project with its cost and hours budgets vs actuals, plus its constituent tasks.
/// </summary>
public sealed class ProjectBudgetVsActualProjectDto
{
    public long ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public long? CustomerId { get; set; }
    public string Customer { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? BudgetAmount { get; set; }
    public decimal ActualCost { get; set; }
    public decimal? CostVariance { get; set; }
    public decimal? CostUsedPercent { get; set; }
    public bool IsCostOverBudget { get; set; }
    public decimal? BudgetHours { get; set; }
    public decimal ActualHours { get; set; }
    public decimal? HoursVariance { get; set; }
    public decimal? HoursUsedPercent { get; set; }
    public bool IsHoursOverBudget { get; set; }
    public IReadOnlyList<ProjectTaskBudgetDto> Tasks { get; set; } = [];
}

/// <summary>
/// Summary totals across all projects in the report.
/// </summary>
public sealed class ProjectBudgetVsActualSummaryDto
{
    public int TotalProjects { get; set; }
    public decimal TotalBudgetAmount { get; set; }
    public decimal TotalActualCost { get; set; }
    public decimal TotalCostVariance { get; set; }
    public decimal? TotalCostUsedPercent { get; set; }
    public decimal TotalBudgetHours { get; set; }
    public decimal TotalActualHours { get; set; }
    public decimal TotalHoursVariance { get; set; }
    public decimal? TotalHoursUsedPercent { get; set; }
    public int OverBudgetProjectsCount { get; set; }
}

/// <summary>
/// Response payload for Project Budget vs Actual report.
/// </summary>
public sealed class ProjectBudgetVsActualReportResponse
{
    public DateOnly AsOfDate { get; set; }
    public IReadOnlyList<ProjectBudgetVsActualProjectDto> Projects { get; set; } = [];
    public ProjectBudgetVsActualSummaryDto Summary { get; set; } = new();
}

/// <summary>
/// Lookups response for filter dropdowns.
/// </summary>
public sealed class ProjectBudgetLookupsResponse
{
    public IReadOnlyList<ProjectLookupItemDto> Customers { get; set; } = [];
    public IReadOnlyList<string> Statuses { get; set; } = [];
    public IReadOnlyList<ProjectLookupItemDto> Projects { get; set; } = [];
}
