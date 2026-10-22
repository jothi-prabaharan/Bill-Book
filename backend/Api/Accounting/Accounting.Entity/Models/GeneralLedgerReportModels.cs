using System.ComponentModel.DataAnnotations;
using Accounting.Entity.Enums;

namespace Accounting.Entity.Models;

// The General ledger (TK-216): one account's postings over a period, with the
// balance it opened at, a running balance down the page and the balance it closed
// at, and a summary of every account for the same period. Benchmarked on Xero's
// General Ledger Detail and Summary, AutoCount's General Ledger and Odoo's
// General Ledger (grouped by account, with an initial balance). Every figure is in
// the branch's base currency, and every balance is on the account's normal side:
// positive when it stands where the account normally does.

/// <summary>
/// What one account's ledger is asked for: a period, with both ends required
/// (D-67), and optionally one contact or one project. The account is the route's.
/// The branch is always the token's: a request naming another branch is refused
/// with 403.
/// </summary>
public sealed class GeneralLedgerReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? From { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? To { get; set; }

    /// <summary>Only the postings that name this contact, with the opening balance of the same postings.</summary>
    public long? ContactId { get; set; }

    /// <summary>Only the postings tagged with this project, with the opening balance of the same postings.</summary>
    public long? ProjectId { get; set; }

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>One posting, with everything needed to get back to the document that wrote it.</summary>
public sealed class GeneralLedgerReportRowDto
{
    public long LedgerId { get; set; }

    public DateOnly LedgerDate { get; set; }

    /// <summary>
    /// The document's own number, written by the poster; for a row written before
    /// the ledger carried one, the document type and id joined, such as INV-42.
    /// </summary>
    [MaxLength(60, ErrorMessage = "Document number cannot exceed 60 characters.")]
    public string DocumentNo { get; set; } = string.Empty;

    /// <summary>The document type: INV, BIL, JRN, STA.</summary>
    [MaxLength(10, ErrorMessage = "Source cannot exceed 10 characters.")]
    public string TransactionTypeCode { get; set; } = string.Empty;

    public long TransactionId { get; set; }

    public long TransactionDetailId { get; set; }

    /// <summary>The manual journal behind this row, when one wrote it.</summary>
    public long? JournalId { get; set; }

    [MaxLength(30, ErrorMessage = "Journal number cannot exceed 30 characters.")]
    public string? JournalNo { get; set; }

    [MaxLength(200, ErrorMessage = "Sub-account name cannot exceed 200 characters.")]
    public string? SubAccountName { get; set; }

    public long? ContactId { get; set; }

    /// <summary>The contact's name, from Contacts; null when the row names none or it could not be read.</summary>
    [MaxLength(200, ErrorMessage = "Contact name cannot exceed 200 characters.")]
    public string? ContactName { get; set; }

    public long? ProjectId { get; set; }

    [MaxLength(150, ErrorMessage = "Project name cannot exceed 150 characters.")]
    public string? ProjectName { get; set; }

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    /// <summary>In the document's own currency.</summary>
    public decimal DebitAmount { get; set; }

    public decimal CreditAmount { get; set; }

    /// <summary>In the branch's base currency, which every balance is in.</summary>
    public decimal DebitAmountBase { get; set; }

    public decimal CreditAmountBase { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string CurrencyCode { get; set; } = string.Empty;

    public decimal ExchangeRate { get; set; }

    /// <summary>The account's balance after this row, on its normal side.</summary>
    public decimal RunningBalance { get; set; }
}

/// <summary>One account over a period.</summary>
public sealed class GeneralLedgerReportResultDto
{
    public long AccountId { get; set; }

    [MaxLength(20, ErrorMessage = "Account code cannot exceed 20 characters.")]
    public string AccountCode { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "Account name cannot exceed 200 characters.")]
    public string AccountName { get; set; } = string.Empty;

    public int AccountTypeId { get; set; }

    /// <summary>Runs opposite its type, such as sales returns; its normal side is the other one.</summary>
    public bool IsContra { get; set; }

    /// <summary>Where the account grows, and the side every balance on this report is shown on.</summary>
    public LedgerSide NormalSide { get; set; }

    public DateOnly From { get; set; }

    public DateOnly To { get; set; }

    /// <summary>The first day of the financial year the period starts in: an income or expense account opens from here.</summary>
    public DateOnly FinancialYearStart { get; set; }

    public long? ContactId { get; set; }

    public long? ProjectId { get; set; }

    /// <summary>
    /// What the account stood at when the period opened, on its normal side. A
    /// balance-sheet account opens at everything before the period; an income or
    /// expense account at what was posted since its financial year began.
    /// </summary>
    public decimal OpeningBalance { get; set; }

    /// <summary>The opening balance plus what grew the account in the period less what reduced it.</summary>
    public decimal ClosingBalance { get; set; }

    public decimal TotalDebit { get; set; }

    public decimal TotalCredit { get; set; }

    public List<GeneralLedgerReportRowDto> Rows { get; set; } = [];
}

/// <summary>What the summary of every account is asked for: a period, both ends required.</summary>
public sealed class GeneralLedgerSummaryRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? From { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? To { get; set; }

    /// <summary>Lists active accounts with no balance and no postings; off by default.</summary>
    public bool IsZeroBalanceShown { get; set; }

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>One account's opening balance, the period's debits and credits, and its closing balance.</summary>
public sealed class GeneralLedgerSummaryLineDto
{
    public long AccountId { get; set; }

    [MaxLength(20, ErrorMessage = "Account code cannot exceed 20 characters.")]
    public string AccountCode { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "Account name cannot exceed 200 characters.")]
    public string AccountName { get; set; } = string.Empty;

    public int AccountTypeId { get; set; }

    public bool IsContra { get; set; }

    public LedgerSide NormalSide { get; set; }

    /// <summary>On the account's normal side, as in its ledger.</summary>
    public decimal OpeningBalance { get; set; }

    public decimal TotalDebit { get; set; }

    public decimal TotalCredit { get; set; }

    public decimal ClosingBalance { get; set; }
}

/// <summary>Every account that moved or holds a balance, for a period.</summary>
public sealed class GeneralLedgerSummaryResultDto
{
    public DateOnly From { get; set; }

    public DateOnly To { get; set; }

    public DateOnly FinancialYearStart { get; set; }

    public List<GeneralLedgerSummaryLineDto> Lines { get; set; } = [];

    /// <summary>Added up over the lines; the two agree on books that balance.</summary>
    public decimal TotalDebit { get; set; }

    public decimal TotalCredit { get; set; }
}
