using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// Budget Monthly Trend (TK-214): Budget vs Actual's layout (TK-204) month by
// month across the budget's year, so the shape of the year shows and not one
// window of it. Benchmarked on Xero's Budget Variance by month, Odoo's budget
// analysis by period and AutoCount's monthly budget vs actual. Every figure is
// in the branch's base currency.

/// <summary>
/// What the report is asked for: a budget and an as-at date within its year.
/// Both are required (D-67). Months after the as-at date's month show their
/// budget and no actual yet. The branch is always the token's: a request naming
/// another branch is refused with 403. Which of budget, actual and variance to
/// show is the page's choice and is not asked of the server.
/// </summary>
public sealed class BudgetMonthlyTrendReportRequest
{
    [Required(ErrorMessage = "Budget is required.")]
    public long? BudgetId { get; set; }

    [Required(ErrorMessage = "As at date is required.")]
    public DateOnly? AsOfDate { get; set; }

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>
/// One cell: a budget against what was posted. For a month after the as-at
/// date there is no actual yet, so the actual, the variance and its percentage
/// are null and only the budget is given.
/// </summary>
public sealed class BudgetMonthlyTrendCellDto
{
    public decimal Budget { get; set; }

    public decimal? Actual { get; set; }

    /// <summary>Positive when favourable: income above budget, or spending below it.</summary>
    public decimal? Variance { get; set; }

    /// <summary>The variance as a percentage of the budget, to two places; null without a budget or an actual.</summary>
    public decimal? VariancePercent { get; set; }
}

/// <summary>One month of the budget's year.</summary>
public sealed class BudgetMonthlyTrendMonthDto
{
    /// <summary>The budget's period, 1 to 12.</summary>
    public int Period { get; set; }

    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    /// <summary>After the as-at date's month: its budget is shown and no actual yet.</summary>
    public bool IsFuture { get; set; }
}

/// <summary>One account across the year.</summary>
public sealed class BudgetMonthlyTrendLineDto
{
    public long AccountId { get; set; }

    [MaxLength(20, ErrorMessage = "Account code cannot exceed 20 characters.")]
    public string AccountCode { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "Account name cannot exceed 200 characters.")]
    public string AccountName { get; set; } = string.Empty;

    /// <summary>Runs opposite its type, such as sales returns; its budget and actual reduce the section.</summary>
    public bool IsContra { get; set; }

    /// <summary>Twelve cells, one per month, in the budget's year order.</summary>
    public List<BudgetMonthlyTrendCellDto> Months { get; set; } = [];

    /// <summary>The months up to and including the as-at date's: Budget vs Actual's year to date.</summary>
    public BudgetMonthlyTrendCellDto YearToDate { get; set; } = new();

    /// <summary>The budget for the whole year, future months included.</summary>
    public decimal YearBudget { get; set; }
}

/// <summary>A section of the Profit &amp; Loss layout, with its lines and totals.</summary>
public sealed class BudgetMonthlyTrendSectionDto
{
    public ProfitAndLossSection Section { get; set; }

    [MaxLength(100, ErrorMessage = "Title cannot exceed 100 characters.")]
    public string Title { get; set; } = string.Empty;

    public List<BudgetMonthlyTrendLineDto> Lines { get; set; } = [];

    public List<BudgetMonthlyTrendCellDto> Months { get; set; } = [];

    public BudgetMonthlyTrendCellDto YearToDate { get; set; } = new();

    public decimal YearBudget { get; set; }
}

/// <summary>Gross or net profit across the year: a higher profit being favourable.</summary>
public sealed class BudgetMonthlyTrendProfitDto
{
    public List<BudgetMonthlyTrendCellDto> Months { get; set; } = [];

    public BudgetMonthlyTrendCellDto YearToDate { get; set; } = new();

    public decimal YearBudget { get; set; }
}

/// <summary>A budget against the books, month by month across its year.</summary>
public sealed class BudgetMonthlyTrendReportResultDto
{
    public BudgetVsActualBudgetDto Budget { get; set; } = new();

    public DateOnly AsOfDate { get; set; }

    /// <summary>The budget's period holding the as-at date, 1 to 12: the last month with an actual.</summary>
    public int AsOfPeriod { get; set; }

    /// <summary>The first day of the budget's year.</summary>
    public DateOnly FromDate { get; set; }

    /// <summary>The last day of the as-at date's month: where the year to date ends.</summary>
    public DateOnly ThroughDate { get; set; }

    /// <summary>The twelve months of the budget's year.</summary>
    public List<BudgetMonthlyTrendMonthDto> Months { get; set; } = [];

    /// <summary>Revenue, cost of sales, other income and expenses, in that order.</summary>
    public List<BudgetMonthlyTrendSectionDto> Sections { get; set; } = [];

    public BudgetMonthlyTrendProfitDto GrossProfit { get; set; } = new();

    public BudgetMonthlyTrendProfitDto NetProfit { get; set; } = new();
}
