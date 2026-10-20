using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Profit & Loss (TK-200): its own request and answer, benchmarked on Xero's
// (a period, a comparison period, accounts with no movement hidden), AutoCount's
// (sales less returns, less cost of sales, to gross profit; other income and
// expenses to net profit) and Odoo's (gross and net profit with their margins).
// Every figure is in the branch's base currency.

/// <summary>
/// What the report is asked for. The from and to dates are required (D-67);
/// a comparison period, when given, needs both of its dates. The branch is
/// always the token's: a request naming another branch is refused with 403.
/// </summary>
public sealed class ProfitAndLossReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>The first day of a period to show beside it, such as the same period a year before.</summary>
    public DateOnly? CompareFromDate { get; set; }

    public DateOnly? CompareToDate { get; set; }

    /// <summary>Lists accounts with no movement in either period; off by default.</summary>
    public bool IsZeroBalanceShown { get; set; }

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>One account's movement in the period, positive on its section's own side.</summary>
public sealed class ProfitAndLossLineDto
{
    public long AccountId { get; set; }

    [MaxLength(20, ErrorMessage = "Account code cannot exceed 20 characters.")]
    public string AccountCode { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "Account name cannot exceed 200 characters.")]
    public string AccountName { get; set; } = string.Empty;

    /// <summary>Runs opposite its type, such as sales returns or purchase returns; its amount reduces the section.</summary>
    public bool IsContra { get; set; }

    public decimal Amount { get; set; }

    /// <summary>The comparison period's amount; null when none is asked for.</summary>
    public decimal? CompareAmount { get; set; }
}

/// <summary>Revenue, cost of sales, other income or expenses, with their lines and total.</summary>
public sealed class ProfitAndLossSectionDto
{
    public ProfitAndLossSection Section { get; set; }

    [MaxLength(100, ErrorMessage = "Title cannot exceed 100 characters.")]
    public string Title { get; set; } = string.Empty;

    public List<ProfitAndLossLineDto> Lines { get; set; } = [];

    public decimal Total { get; set; }

    public decimal? CompareTotal { get; set; }
}

/// <summary>The profit lines: what the sections add up to.</summary>
public sealed class ProfitAndLossTotalsDto
{
    public decimal Revenue { get; set; }

    public decimal CostOfSales { get; set; }

    /// <summary>Revenue less cost of sales.</summary>
    public decimal GrossProfit { get; set; }

    public decimal OtherIncome { get; set; }

    public decimal Expenses { get; set; }

    /// <summary>Gross profit plus other income less expenses.</summary>
    public decimal NetProfit { get; set; }

    /// <summary>Gross profit as a percentage of revenue, to two places; null without revenue.</summary>
    public decimal? GrossMarginPercent { get; set; }

    /// <summary>Net profit as a percentage of revenue, to two places; null without revenue.</summary>
    public decimal? NetMarginPercent { get; set; }

    public decimal? CompareRevenue { get; set; }

    public decimal? CompareCostOfSales { get; set; }

    public decimal? CompareGrossProfit { get; set; }

    public decimal? CompareOtherIncome { get; set; }

    public decimal? CompareExpenses { get; set; }

    public decimal? CompareNetProfit { get; set; }

    public decimal? CompareGrossMarginPercent { get; set; }

    public decimal? CompareNetMarginPercent { get; set; }
}

/// <summary>The profit and loss: its sections in order, the profit lines, and the periods they are for.</summary>
public sealed class ProfitAndLossReportResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public DateOnly? CompareFromDate { get; set; }

    public DateOnly? CompareToDate { get; set; }

    public List<ProfitAndLossSectionDto> Sections { get; set; } = [];

    public ProfitAndLossTotalsDto Totals { get; set; } = new();
}
