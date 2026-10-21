using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Cash Flow Statement – Direct (TK-201): its own request and answer,
// benchmarked on Xero's direct-method statement (receipts from customers,
// payments to suppliers and employees, then investing and financing, down to
// cash at the beginning and end), Odoo's (a comparison period beside it) and
// Ind AS 7 (the effect of exchange-rate changes shown apart from the flows).
// Every figure is in the branch's base currency; an inflow is positive.

/// <summary>
/// What the report is asked for. The from and to dates are required (D-67);
/// a comparison period, when given, needs both of its dates. The branch is
/// always the token's: a request naming another branch is refused with 403.
/// </summary>
public sealed class CashFlowReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>The first day of a period to show beside it, such as the same period a year before.</summary>
    public DateOnly? CompareFromDate { get; set; }

    public DateOnly? CompareToDate { get; set; }

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>The cash one account on the other side of the cash movements accounts for, within a line.</summary>
public sealed class CashFlowAccountDto
{
    public long AccountId { get; set; }

    [MaxLength(20, ErrorMessage = "Account code cannot exceed 20 characters.")]
    public string AccountCode { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "Account name cannot exceed 200 characters.")]
    public string AccountName { get; set; } = string.Empty;

    /// <summary>Cash in less cash out.</summary>
    public decimal Amount { get; set; }

    /// <summary>The comparison period's amount; null when none is asked for.</summary>
    public decimal? CompareAmount { get; set; }
}

/// <summary>One line of the statement, with the accounts that make it up.</summary>
public sealed class CashFlowLineDto
{
    public CashFlowLine Line { get; set; }

    [MaxLength(100, ErrorMessage = "Title cannot exceed 100 characters.")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Cash in less cash out.</summary>
    public decimal Amount { get; set; }

    public decimal? CompareAmount { get; set; }

    public List<CashFlowAccountDto> Accounts { get; set; } = [];
}

/// <summary>Operating, investing or financing activities, with their lines and the net cash from them.</summary>
public sealed class CashFlowSectionDto
{
    public CashFlowActivity Activity { get; set; }

    [MaxLength(100, ErrorMessage = "Title cannot exceed 100 characters.")]
    public string Title { get; set; } = string.Empty;

    public List<CashFlowLineDto> Lines { get; set; } = [];

    /// <summary>Net cash from the activity.</summary>
    public decimal Total { get; set; }

    public decimal? CompareTotal { get; set; }
}

/// <summary>From the net change to the closing cash, and whether the two ends meet.</summary>
public sealed class CashFlowTotalsDto
{
    /// <summary>Net cash from the three activities together.</summary>
    public decimal NetChange { get; set; }

    /// <summary>The cash and bank balances on the day before the period, with opening balances entered within it.</summary>
    public decimal OpeningCash { get; set; }

    /// <summary>The cash and bank balances at the end of the period, read from the ledger.</summary>
    public decimal ClosingCash { get; set; }

    /// <summary>Closing cash less opening cash, the net change and the exchange-rate effect: zero when they reconcile.</summary>
    public decimal Difference { get; set; }

    public decimal? CompareNetChange { get; set; }

    public decimal? CompareOpeningCash { get; set; }

    public decimal? CompareClosingCash { get; set; }

    public decimal? CompareDifference { get; set; }
}

/// <summary>The statement: its activities in order, the exchange-rate effect, the cash at each end, and the periods.</summary>
public sealed class CashFlowReportResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public DateOnly? CompareFromDate { get; set; }

    public DateOnly? CompareToDate { get; set; }

    public List<CashFlowSectionDto> Sections { get; set; } = [];

    /// <summary>Revaluation of foreign-currency cash and bank accounts, apart from the activities.</summary>
    public CashFlowLineDto ExchangeRateEffect { get; set; } = new();

    public CashFlowTotalsDto Totals { get; set; } = new();

    /// <summary>Opening cash plus the net change and the exchange-rate effect equals closing cash, on every period shown.</summary>
    public bool IsReconciled { get; set; }
}
