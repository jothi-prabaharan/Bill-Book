using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Reconciliation Report (TK-202): its own request and answer, benchmarked on
// Xero's Bank Reconciliation Summary (the balance in the books, plus outstanding
// payments, less outstanding receipts, plus statement lines not yet in the books,
// against the statement balance), AutoCount's bank reconciliation statement
// (unpresented payments and uncleared receipts listed by date and number) and
// Odoo's (the last statement's balance and the difference). Every figure is in
// the bank account's own currency.

/// <summary>
/// What the report is asked for: one bank account at the end of one day. Both
/// are required (D-67). The branch is always the token's: a request naming
/// another branch is refused with 403.
/// </summary>
public sealed class BankReconciliationReportRequest
{
    [Required(ErrorMessage = "Bank account is required.")]
    public long? BankAccountId { get; set; }

    [Required(ErrorMessage = "As at date is required.")]
    public DateOnly? AsOfDate { get; set; }

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>A bank account the report can be run for.</summary>
public sealed class BankReconciliationAccountDto
{
    public long BankAccountId { get; set; }

    [MaxLength(100, ErrorMessage = "Account name cannot exceed 100 characters.")]
    public string AccountName { get; set; } = string.Empty;

    [MaxLength(30, ErrorMessage = "Account number cannot exceed 30 characters.")]
    public string AccountNumber { get; set; } = string.Empty;

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string CurrencyCode { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}

/// <summary>One outstanding document or one statement line not in the books.</summary>
public sealed class BankReconciliationItemDto
{
    /// <summary>The document's type (<c>SPM</c>, <c>RCM</c> or <c>TRM</c>); null for a statement line.</summary>
    [MaxLength(10, ErrorMessage = "Transaction type cannot exceed 10 characters.")]
    public string? TransactionTypeCode { get; set; }

    /// <summary>The document's id; null for a statement line.</summary>
    public long? TransactionId { get; set; }

    /// <summary>The statement line's id; null for a document.</summary>
    public long? BankStatementLineId { get; set; }

    public DateOnly TransactionDate { get; set; }

    [MaxLength(30, ErrorMessage = "Transaction number cannot exceed 30 characters.")]
    public string? TransactionNo { get; set; }

    [MaxLength(100, ErrorMessage = "Reference cannot exceed 100 characters.")]
    public string? Reference { get; set; }

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    public decimal AmountIn { get; set; }

    public decimal AmountOut { get; set; }
}

/// <summary>One group of items with its total.</summary>
public sealed class BankReconciliationGroupDto
{
    public BankReconciliationGroup Group { get; set; }

    [MaxLength(100, ErrorMessage = "Title cannot exceed 100 characters.")]
    public string Title { get; set; } = string.Empty;

    public List<BankReconciliationItemDto> Items { get; set; } = [];

    /// <summary>
    /// What the group adds to the books to reach the statement: the payments,
    /// the receipts taken away, and the statement lines' money in less money out.
    /// </summary>
    public decimal Adjustment { get; set; }
}

/// <summary>The reconciliation of one bank account at the end of one day.</summary>
public sealed class BankReconciliationReportResultDto
{
    public DateOnly AsOfDate { get; set; }

    public long BankAccountId { get; set; }

    [MaxLength(100, ErrorMessage = "Account name cannot exceed 100 characters.")]
    public string AccountName { get; set; } = string.Empty;

    [MaxLength(30, ErrorMessage = "Account number cannot exceed 30 characters.")]
    public string AccountNumber { get; set; } = string.Empty;

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>The account's balance in the ledger at the end of the day, in its currency.</summary>
    public decimal BookBalance { get; set; }

    /// <summary>The same balance in base currency, as the Balance Sheet carries it.</summary>
    public decimal BookBalanceBase { get; set; }

    /// <summary>Outstanding payments, outstanding receipts, and statement lines not in the books, in that order.</summary>
    public List<BankReconciliationGroupDto> Groups { get; set; } = [];

    /// <summary>The books plus every group's adjustment: what the statement should say.</summary>
    public decimal ExpectedStatementBalance { get; set; }

    /// <summary>What the imported statements say at the end of the day; null when none gives a balance on or before it.</summary>
    public decimal? StatementBalance { get; set; }

    /// <summary>The last date an imported statement states its balance on, on or before the as-at date.</summary>
    public DateOnly? StatementBalanceDate { get; set; }

    /// <summary>The statement balance less the expected one; null without a statement balance.</summary>
    public decimal? Difference { get; set; }

    /// <summary>A statement balance is known and equals the expected one.</summary>
    public bool IsReconciled { get; set; }
}
