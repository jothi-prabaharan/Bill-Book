using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Balance Sheet (TK-199): its own request and answer, benchmarked on Xero's
// balance sheet (an as-at date, a comparison column, accounts with no balance
// hidden by default), AutoCount's (assets against liabilities and equity, with
// the year's profit carried into equity) and Odoo's (current-year and earlier
// years' earnings shown apart). Every figure is in the branch's base currency.

/// <summary>
/// What the report is asked for. The as-at date is required; the comparison
/// date adds a second column. The branch is always the token's: a request
/// naming another branch is refused with 403.
/// </summary>
public sealed class BalanceSheetReportRequest
{
    /// <summary>The balances as they stood at the end of this day.</summary>
    [Required(ErrorMessage = "As at date is required.")]
    public DateOnly? AsOfDate { get; set; }

    /// <summary>A second date to show beside it, such as the same day a year before.</summary>
    public DateOnly? CompareAsOfDate { get; set; }

    /// <summary>Lists accounts whose balance is zero on every date shown; off by default.</summary>
    public bool IsZeroBalanceShown { get; set; }

    /// <summary>
    /// The branch, when the caller names one. It must be the branch in the
    /// token; the query filter scopes the rows to that branch either way.
    /// </summary>
    public Guid? OrgId { get; set; }
}

/// <summary>One line of a section: an account, or a figure the report works out.</summary>
public sealed class BalanceSheetLineDto
{
    /// <summary>Null for a worked-out line (the year's profit, earlier years' profit).</summary>
    public long? AccountId { get; set; }

    [MaxLength(20, ErrorMessage = "Account code cannot exceed 20 characters.")]
    public string? AccountCode { get; set; }

    [MaxLength(200, ErrorMessage = "Account name cannot exceed 200 characters.")]
    public string AccountName { get; set; } = string.Empty;

    /// <summary>An account running opposite its type, such as accumulated depreciation; its balance is negative within the section.</summary>
    public bool IsContra { get; set; }

    /// <summary>A figure worked out from the income and expense accounts rather than an account's own balance.</summary>
    public bool IsComputed { get; set; }

    /// <summary>The balance at the as-at date, positive on the section's own side.</summary>
    public decimal Balance { get; set; }

    /// <summary>The balance at the comparison date; null when none is asked for.</summary>
    public decimal? CompareBalance { get; set; }
}

/// <summary>Assets, liabilities or equity, with their lines and total.</summary>
public sealed class BalanceSheetSectionDto
{
    public BalanceSheetSection Section { get; set; }

    [MaxLength(100, ErrorMessage = "Title cannot exceed 100 characters.")]
    public string Title { get; set; } = string.Empty;

    public List<BalanceSheetLineDto> Lines { get; set; } = [];

    public decimal Total { get; set; }

    public decimal? CompareTotal { get; set; }
}

/// <summary>The totals beneath the sections, and whether the two sides agree.</summary>
public sealed class BalanceSheetTotalsDto
{
    public decimal TotalAssets { get; set; }

    public decimal TotalLiabilities { get; set; }

    public decimal TotalEquity { get; set; }

    public decimal TotalLiabilitiesAndEquity { get; set; }

    /// <summary>Assets less liabilities and equity: zero for a ledger that balances.</summary>
    public decimal Difference { get; set; }

    public decimal? CompareTotalAssets { get; set; }

    public decimal? CompareTotalLiabilities { get; set; }

    public decimal? CompareTotalEquity { get; set; }

    public decimal? CompareTotalLiabilitiesAndEquity { get; set; }

    public decimal? CompareDifference { get; set; }
}

/// <summary>The balance sheet: its sections in order, the totals, and the dates they are for.</summary>
public sealed class BalanceSheetReportResultDto
{
    public DateOnly AsOfDate { get; set; }

    public DateOnly? CompareAsOfDate { get; set; }

    /// <summary>The first day of the financial year the as-at date falls in, where the year's profit starts.</summary>
    public DateOnly FinancialYearStart { get; set; }

    public DateOnly? CompareFinancialYearStart { get; set; }

    public List<BalanceSheetSectionDto> Sections { get; set; } = [];

    public BalanceSheetTotalsDto Totals { get; set; } = new();

    /// <summary>True when assets equal liabilities plus equity on every date shown.</summary>
    public bool IsBalanced { get; set; }
}
