using System.ComponentModel.DataAnnotations;

namespace Reporting.Entity.Models;

/// <summary>
/// Request filters for the Project Profitability report (TK-256).
/// From and To dates are required (D-67).
/// </summary>
public sealed class ProjectProfitabilityReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? From { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? To { get; set; }

    public long? CustomerId { get; set; }

    public string? Status { get; set; }

    public string? BillingMethod { get; set; }

    public long? ProjectId { get; set; }

    public Guid? OrgId { get; set; }
}

/// <summary>
/// A single project's profitability figures over the requested date range.
/// </summary>
public sealed class ProjectProfitabilityRowDto
{
    public long ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public long? CustomerId { get; set; }
    public string Customer { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string BillingMethod { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit { get; set; }
    public decimal? MarginPercent { get; set; }
    public decimal? BudgetAmount { get; set; }
    public decimal? BudgetVariance { get; set; }
}

/// <summary>
/// Aggregate summary metrics across all projects matching the filters.
/// </summary>
public sealed class ProjectProfitabilitySummaryDto
{
    public int TotalProjects { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalProfit { get; set; }
    public decimal? OverallMarginPercent { get; set; }
    public decimal TotalBudgetAmount { get; set; }
}

/// <summary>
/// Complete response payload for the Project Profitability report.
/// </summary>
public sealed class ProjectProfitabilityReportResponse
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public IReadOnlyList<ProjectProfitabilityRowDto> Rows { get; set; } = [];
    public ProjectProfitabilitySummaryDto Summary { get; set; } = new();
}

/// <summary>
/// Simple lookup item with id, code, and display name.
/// </summary>
public sealed class ProjectLookupItemDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Lookups for the Project Profitability report filters.
/// </summary>
public sealed class ProjectProfitabilityLookupsResponse
{
    public IReadOnlyList<ProjectLookupItemDto> Customers { get; set; } = [];
    public IReadOnlyList<string> Statuses { get; set; } = [];
    public IReadOnlyList<string> BillingMethods { get; set; } = [];
    public IReadOnlyList<ProjectLookupItemDto> Projects { get; set; } = [];
}
