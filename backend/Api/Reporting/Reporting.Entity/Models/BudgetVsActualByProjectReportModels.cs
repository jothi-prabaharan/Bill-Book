using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// Budget vs Actual by Project (TK-207): Budget vs Actual's layout (TK-204) once
// per project, for a budget kept by project. Benchmarked on Xero's Budget
// Variance filtered by a tracking category, Odoo's budget analysis by analytic
// account and AutoCount's project budget. Every figure is in the branch's base
// currency, and only postings that carry a project count.

/// <summary>
/// What the report is asked for: a budget kept by project, an as-at date within
/// its year, which months up to that date, and optionally one project. The
/// first three are required (D-67). The branch is always the token's: a request
/// naming another branch is refused with 403.
/// </summary>
public sealed class BudgetVsActualByProjectReportRequest
{
    [Required(ErrorMessage = "Budget is required.")]
    public long? BudgetId { get; set; }

    [Required(ErrorMessage = "As at date is required.")]
    public DateOnly? AsOfDate { get; set; }

    [Required(ErrorMessage = "Period is required.")]
    public BudgetPeriod? Period { get; set; }

    /// <summary>One project only; every project with a budget or an actual when blank.</summary>
    public long? ProjectId { get; set; }

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>A project the report can be narrowed to.</summary>
public sealed class BudgetVsActualProjectOptionDto
{
    public long ProjectId { get; set; }

    [MaxLength(20, ErrorMessage = "Project code cannot exceed 20 characters.")]
    public string ProjectCode { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Project name cannot exceed 150 characters.")]
    public string ProjectName { get; set; } = string.Empty;

    public ProjectStatus Status { get; set; }
}

/// <summary>One project's budget against its postings, laid out as Budget vs Actual lays out the branch.</summary>
public sealed class BudgetVsActualProjectDto
{
    public long ProjectId { get; set; }

    [MaxLength(20, ErrorMessage = "Project code cannot exceed 20 characters.")]
    public string ProjectCode { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Project name cannot exceed 150 characters.")]
    public string ProjectName { get; set; } = string.Empty;

    public ProjectStatus Status { get; set; }

    /// <summary>Revenue, cost of sales, other income and expenses, in that order.</summary>
    public List<BudgetVsActualSectionDto> Sections { get; set; } = [];

    public BudgetVsActualProfitDto GrossProfit { get; set; } = new();

    public BudgetVsActualProfitDto NetProfit { get; set; } = new();
}

/// <summary>Each project's budget against its postings for the months covered, and the total across them.</summary>
public sealed class BudgetVsActualByProjectReportResultDto
{
    public BudgetVsActualBudgetDto Budget { get; set; } = new();

    public DateOnly AsOfDate { get; set; }

    public BudgetPeriod Period { get; set; }

    /// <summary>The first and last month of the budget covered, 1 to 12.</summary>
    public int PeriodFrom { get; set; }

    public int PeriodTo { get; set; }

    /// <summary>The first day of the first month covered.</summary>
    public DateOnly FromDate { get; set; }

    /// <summary>The last day of the last month covered.</summary>
    public DateOnly ToDate { get; set; }

    /// <summary>The one project asked for, when one was.</summary>
    public long? ProjectId { get; set; }

    /// <summary>Every project with a budget or an actual in the months covered, by code.</summary>
    public List<BudgetVsActualProjectDto> Projects { get; set; } = [];

    /// <summary>Gross profit across the projects shown.</summary>
    public BudgetVsActualProfitDto GrossProfit { get; set; } = new();

    /// <summary>Net profit across the projects shown.</summary>
    public BudgetVsActualProfitDto NetProfit { get; set; } = new();
}
