using System.ComponentModel.DataAnnotations;

namespace Accounting.Entity.Models;

// The Trial Balance (TK-215): every account's balance at the start of a period,
// what was posted to it in the period and its balance at the end, with a
// subtotal for each account type and the totals that must agree. Benchmarked on
// Xero's Trial Balance, AutoCount's Trial Balance (opening, movement, closing)
// and Odoo's Trial Balance (initial balance, period, end balance). Every figure
// is in the branch's base currency.

/// <summary>
/// What the trial balance is asked for: a period, with both ends required
/// (D-67), and optionally a second period to show beside it. The branch is
/// always the token's: a request naming another branch is refused with 403.
/// </summary>
public sealed class TrialBalanceReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? From { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? To { get; set; }

    /// <summary>The first day of a period to show beside it, such as the same period a year before.</summary>
    public DateOnly? CompareFrom { get; set; }

    public DateOnly? CompareTo { get; set; }

    /// <summary>Lists active accounts with no balance and no movement; off by default.</summary>
    public bool IsZeroBalanceShown { get; set; }

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>
/// One account's opening balance, movement and closing balance. Each balance is
/// in exactly one column, the side it falls on, and both columns are positive.
/// </summary>
public sealed class TrialBalanceReportLineDto
{
    /// <summary>Zero for a worked-out line, which is not an account.</summary>
    public long AccountId { get; set; }

    [MaxLength(20, ErrorMessage = "Account code cannot exceed 20 characters.")]
    public string AccountCode { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "Account name cannot exceed 200 characters.")]
    public string AccountName { get; set; } = string.Empty;

    public int AccountTypeId { get; set; }

    /// <summary>Runs opposite its type, such as sales returns; its normal balance is the other side.</summary>
    public bool IsContra { get; set; }

    /// <summary>The profit of earlier years, carried into equity so the columns agree. Not an account.</summary>
    public bool IsComputed { get; set; }

    public decimal OpeningDebit { get; set; }

    public decimal OpeningCredit { get; set; }

    public decimal MovementDebit { get; set; }

    public decimal MovementCredit { get; set; }

    public decimal ClosingDebit { get; set; }

    public decimal ClosingCredit { get; set; }

    /// <summary>The comparison period's closing balance; null when no comparison is asked for.</summary>
    public decimal? CompareClosingDebit { get; set; }

    public decimal? CompareClosingCredit { get; set; }
}

/// <summary>Columns added up, for an account type or for the whole trial balance.</summary>
public sealed class TrialBalanceReportTotalsDto
{
    public decimal OpeningDebit { get; set; }

    public decimal OpeningCredit { get; set; }

    public decimal MovementDebit { get; set; }

    public decimal MovementCredit { get; set; }

    public decimal ClosingDebit { get; set; }

    public decimal ClosingCredit { get; set; }

    public decimal? CompareClosingDebit { get; set; }

    public decimal? CompareClosingCredit { get; set; }
}

/// <summary>
/// The accounts of one type, with their subtotal. The type's name is
/// <c>mst.AccountTypes</c>', in the master database, and the page names it.
/// </summary>
public sealed class TrialBalanceReportGroupDto
{
    public int AccountTypeId { get; set; }

    public List<TrialBalanceReportLineDto> Lines { get; set; } = [];

    public TrialBalanceReportTotalsDto Totals { get; set; } = new();
}

/// <summary>A trial balance for a period.</summary>
public sealed class TrialBalanceReportResultDto
{
    public DateOnly From { get; set; }

    public DateOnly To { get; set; }

    public DateOnly? CompareFrom { get; set; }

    public DateOnly? CompareTo { get; set; }

    /// <summary>The first day of the financial year the period starts in: income and expense accounts open from here.</summary>
    public DateOnly FinancialYearStart { get; set; }

    public DateOnly? CompareFinancialYearStart { get; set; }

    /// <summary>Asset, liability, equity, income and expense, in that order, those with nothing to show left out.</summary>
    public List<TrialBalanceReportGroupDto> Groups { get; set; } = [];

    public TrialBalanceReportTotalsDto Totals { get; set; } = new();

    /// <summary>Opening debits less credits. Zero when the books are sound.</summary>
    public decimal OpeningDifference { get; set; }

    public decimal MovementDifference { get; set; }

    public decimal ClosingDifference { get; set; }

    public decimal? CompareClosingDifference { get; set; }

    /// <summary>Every pair of columns agreeing, the comparison's included.</summary>
    public bool IsBalanced { get; set; }
}
