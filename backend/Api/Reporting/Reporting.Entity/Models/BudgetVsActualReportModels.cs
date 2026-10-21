using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// Budget vs Actual (TK-204): its own request and answer, benchmarked on Xero's
// Budget Variance (budget, actual, variance and variance % per account, laid
// out as the Profit & Loss with gross and net profit, for a month, a quarter or
// the year to date), Odoo's budget analysis (each budget line against what the
// books show) and AutoCount's budget vs actual (favourable and adverse). Every
// figure is in the branch's base currency.

/// <summary>
/// What the report is asked for: a budget, an as-at date within its year, and
/// which months up to that date. All three are required (D-67). The branch is
/// always the token's: a request naming another branch is refused with 403.
/// </summary>
public sealed class BudgetVsActualReportRequest
{
    [Required(ErrorMessage = "Budget is required.")]
    public long? BudgetId { get; set; }

    [Required(ErrorMessage = "As at date is required.")]
    public DateOnly? AsOfDate { get; set; }

    [Required(ErrorMessage = "Period is required.")]
    public BudgetPeriod? Period { get; set; }

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>A budget the report can be run against.</summary>
public sealed class BudgetVsActualBudgetDto
{
    public long BudgetId { get; set; }

    [MaxLength(100, ErrorMessage = "Budget name cannot exceed 100 characters.")]
    public string Name { get; set; } = string.Empty;

    public DateOnly FinancialYearStart { get; set; }

    /// <summary>The last day of the budget's financial year.</summary>
    public DateOnly FinancialYearEnd { get; set; }

    public BudgetStatus Status { get; set; }

    /// <summary>The branch's own budget for its year: the one the warnings and the catalog report use.</summary>
    public bool IsBranchDefault { get; set; }

    public bool ByProject { get; set; }
}

/// <summary>One account's budget and actual for the months covered.</summary>
public sealed class BudgetVsActualLineDto
{
    public long AccountId { get; set; }

    [MaxLength(20, ErrorMessage = "Account code cannot exceed 20 characters.")]
    public string AccountCode { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "Account name cannot exceed 200 characters.")]
    public string AccountName { get; set; } = string.Empty;

    /// <summary>Runs opposite its type, such as sales returns; its budget and actual reduce the section.</summary>
    public bool IsContra { get; set; }

    public decimal Budget { get; set; }

    public decimal Actual { get; set; }

    /// <summary>Positive when favourable: income above budget, or spending below it.</summary>
    public decimal Variance { get; set; }

    /// <summary>The variance as a percentage of the budget, to two places; null without a budget.</summary>
    public decimal? VariancePercent { get; set; }
}

/// <summary>A section of the Profit &amp; Loss layout, with its lines and totals.</summary>
public sealed class BudgetVsActualSectionDto
{
    public ProfitAndLossSection Section { get; set; }

    [MaxLength(100, ErrorMessage = "Title cannot exceed 100 characters.")]
    public string Title { get; set; } = string.Empty;

    public List<BudgetVsActualLineDto> Lines { get; set; } = [];

    public decimal Budget { get; set; }

    public decimal Actual { get; set; }

    public decimal Variance { get; set; }

    public decimal? VariancePercent { get; set; }
}

/// <summary>Gross or net profit: budget against actual, a higher profit being favourable.</summary>
public sealed class BudgetVsActualProfitDto
{
    public decimal Budget { get; set; }

    public decimal Actual { get; set; }

    public decimal Variance { get; set; }

    public decimal? VariancePercent { get; set; }
}

/// <summary>The budget against the books for the months covered.</summary>
public sealed class BudgetVsActualReportResultDto
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

    /// <summary>Revenue, cost of sales, other income and expenses, in that order.</summary>
    public List<BudgetVsActualSectionDto> Sections { get; set; } = [];

    public BudgetVsActualProfitDto GrossProfit { get; set; } = new();

    public BudgetVsActualProfitDto NetProfit { get; set; } = new();
}
