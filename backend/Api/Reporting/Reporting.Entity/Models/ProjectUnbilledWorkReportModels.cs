using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Reporting.Entity.Models;

/// <summary>
/// Request parameters for the Project Unbilled Work report (TK-258).
/// AsOfDate is required (D-67).
/// </summary>
public sealed class ProjectUnbilledWorkReportRequest
{
    [Required(ErrorMessage = "As of date is required.")]
    public DateOnly? AsOfDate { get; set; }

    public long? CustomerId { get; set; }

    public long? ProjectId { get; set; }

    public Guid? OrgId { get; set; }
}

/// <summary>
/// An individual unbilled line item (time entry, recharge expense, or milestone) for drill-down.
/// </summary>
public sealed class ProjectUnbilledLineDto
{
    public string LineType { get; set; } = string.Empty; // "Time", "Expense", "Milestone"
    public long Id { get; set; }
    public DateOnly? Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitRate { get; set; }
    public decimal? MarkupPercent { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>
/// An unbilled work project row showing totals for time, expenses, and milestones with drill-down lines.
/// </summary>
public sealed class ProjectUnbilledProjectDto
{
    public long ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public long? CustomerId { get; set; }
    public string Customer { get; set; } = string.Empty;
    public string BillingMethod { get; set; } = string.Empty;
    public string? RateBasis { get; set; }
    public decimal? HourlyRate { get; set; }
    public decimal UnbilledHours { get; set; }
    public decimal TimeValue { get; set; }
    public decimal Expenses { get; set; }
    public decimal Milestones { get; set; }
    public decimal Total { get; set; }
    public int LineCount { get; set; }
    public IReadOnlyList<ProjectUnbilledLineDto> Lines { get; set; } = [];
}

/// <summary>
/// Summary totals across all projects in the unbilled work report.
/// </summary>
public sealed class ProjectUnbilledSummaryDto
{
    public int TotalProjects { get; set; }
    public decimal TotalUnbilledHours { get; set; }
    public decimal TotalTimeValue { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal TotalMilestones { get; set; }
    public decimal GrandTotal { get; set; }
}

/// <summary>
/// Complete response payload for the Project Unbilled Work report.
/// </summary>
public sealed class ProjectUnbilledWorkReportResponse
{
    public DateOnly AsOfDate { get; set; }
    public ProjectUnbilledSummaryDto Summary { get; set; } = new();
    public IReadOnlyList<ProjectUnbilledProjectDto> Projects { get; set; } = [];
}

/// <summary>
/// Filter lookup options for the Project Unbilled Work report.
/// </summary>
public sealed class ProjectUnbilledLookupsResponse
{
    public IReadOnlyList<ProjectLookupItemDto> Customers { get; set; } = [];
    public IReadOnlyList<ProjectLookupItemDto> Projects { get; set; } = [];
}
