using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Reporting.Entity.Models;

/// <summary>
/// Request parameters for the Project Time by User report (TK-259).
/// FromDate and ToDate are required (D-67). GroupBy defaults to "user" (or "project").
/// </summary>
public sealed class ProjectTimeByUserReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    public Guid? UserId { get; set; }

    public long? ProjectId { get; set; }

    public string GroupBy { get; set; } = "user"; // "user" or "project"

    public Guid? OrgId { get; set; }
}

/// <summary>
/// Logged hours line breakdown for a specific project, task, and user.
/// </summary>
public sealed class ProjectTimeItemDto
{
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string TaskName { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public decimal Hours { get; set; }
    public decimal BillableHours { get; set; }
    public decimal BilledHours { get; set; }
    public decimal NonBillableHours { get; set; }
    public decimal? UtilisationPercent { get; set; }
}

/// <summary>
/// Grouped card/section (either by user or by project) with totals and constituent items.
/// </summary>
public sealed class ProjectTimeGroupDto
{
    public string GroupKey { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string? GroupSubtext { get; set; }
    public decimal TotalHours { get; set; }
    public decimal TotalBillableHours { get; set; }
    public decimal TotalBilledHours { get; set; }
    public decimal TotalNonBillableHours { get; set; }
    public decimal? TotalUtilisationPercent { get; set; }
    public IReadOnlyList<ProjectTimeItemDto> Items { get; set; } = [];
}

/// <summary>
/// Summary metrics for the entire period across all users and projects.
/// </summary>
public sealed class ProjectTimeSummaryDto
{
    public int TotalUsers { get; set; }
    public int TotalProjects { get; set; }
    public decimal TotalHours { get; set; }
    public decimal TotalBillableHours { get; set; }
    public decimal TotalBilledHours { get; set; }
    public decimal TotalNonBillableHours { get; set; }
    public decimal? OverallUtilisationPercent { get; set; }
}

/// <summary>
/// Complete response payload for the Project Time by User report.
/// </summary>
public sealed class ProjectTimeByUserReportResponse
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string GroupBy { get; set; } = "user";
    public ProjectTimeSummaryDto Summary { get; set; } = new();
    public IReadOnlyList<ProjectTimeGroupDto> Groups { get; set; } = [];
}

/// <summary>
/// User option for lookup dropdowns.
/// </summary>
public sealed class ProjectUserLookupItemDto
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Lookup options for filter dropdowns.
/// </summary>
public sealed class ProjectTimeByUserLookupsResponse
{
    public IReadOnlyList<ProjectUserLookupItemDto> Users { get; set; } = [];
    public IReadOnlyList<ProjectLookupItemDto> Projects { get; set; } = [];
}
