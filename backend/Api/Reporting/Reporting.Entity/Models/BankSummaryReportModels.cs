using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Bank Summary (TK-219): for each bank and cash account, what it held when a
// period began, what came in, what went out, any revaluation of a foreign
// currency account, and what it holds at the end. Benchmarked on Xero's Bank
// Summary (opening balance, cash received, cash spent, closing balance, with the
// foreign currency revaluation), AutoCount's bank book summary and Odoo's Bank
// and Cash journal summary. Figures are in the branch's base currency; a foreign
// currency account also shows its own.

/// <summary>
/// What the report is asked for: a period, both ends required (D-67), and
/// optionally one bank account. The branch is always the token's: a request naming
/// another branch is refused with 403.
/// </summary>
public sealed class BankSummaryReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>Only this bank account; every one when blank.</summary>
    public long? BankAccountId { get; set; }

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>One bank or cash account over the period.</summary>
public sealed class BankSummaryAccountDto
{
    public long BankAccountId { get; set; }

    [MaxLength(100, ErrorMessage = "Account name cannot exceed 100 characters.")]
    public string AccountName { get; set; } = string.Empty;

    [MaxLength(30, ErrorMessage = "Account number cannot exceed 30 characters.")]
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>Savings, current, overdraft, credit card, wallet, cash and so on.</summary>
    public BankAccountType AccountType { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string CurrencyCode { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    /// <summary>The account is kept in a currency other than the branch's, so it also has its own-currency figures.</summary>
    public bool IsForeign { get; set; }

    /// <summary>The account's ledger balance before the period, in base currency.</summary>
    public decimal OpeningBalance { get; set; }

    /// <summary>Debits in the period, revaluation excluded: money that came in, and the receiving end of a transfer.</summary>
    public decimal Received { get; set; }

    /// <summary>Credits in the period, revaluation excluded: money that went out, and the paying end of a transfer.</summary>
    public decimal Spent { get; set; }

    /// <summary>
    /// The net of the period's postings on documents that also post to Unrealized
    /// FX Gain/Loss: positive when the account is worth more in base currency.
    /// </summary>
    public decimal Revaluation { get; set; }

    /// <summary>Opening plus received, less spent, plus the revaluation: the ledger balance at the end of the period.</summary>
    public decimal ClosingBalance { get; set; }

    /// <summary>The same four figures in the account's own currency; null for an account in the base currency.</summary>
    public decimal? OwnOpeningBalance { get; set; }

    public decimal? OwnReceived { get; set; }

    public decimal? OwnSpent { get; set; }

    public decimal? OwnClosingBalance { get; set; }
}

/// <summary>The accounts of the period, with the totals in base currency.</summary>
public sealed class BankSummaryReportResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    /// <summary>The branch's base currency, which every figure but the own-currency ones is in.</summary>
    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    public List<BankSummaryAccountDto> Accounts { get; set; } = [];

    public decimal TotalOpeningBalance { get; set; }

    public decimal TotalReceived { get; set; }

    public decimal TotalSpent { get; set; }

    public decimal TotalRevaluation { get; set; }

    public decimal TotalClosingBalance { get; set; }
}
