using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Account Transaction report (TK-218): every posting in a period, account by
// account, with its contact, currency, tax rate and a running balance on the
// account's normal side: the detailed general ledger across many accounts at
// once. Benchmarked on Xero's Account Transactions, AutoCount's Ledger
// Transaction Listing and Odoo's General Ledger with partner and tax columns.
// Amounts are in the document's own currency and in the branch's base currency.

/// <summary>
/// What the report is asked for: a period with both ends required (D-67), and
/// optionally some accounts, one contact, one tax rate and one source. The result
/// comes a page of postings at a time. The branch is always the token's: a request
/// naming another branch is refused with 403.
/// </summary>
public sealed class AccountTransactionRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>The accounts wanted, up to <c>AccountTransactionLimits.MaxAccounts</c>; every account that has a posting when blank.</summary>
    public List<long>? AccountIds { get; set; }

    /// <summary>Only the postings that name this contact.</summary>
    public long? ContactId { get; set; }

    /// <summary>Only the tax postings of this tax rate, whichever revision of it was in force on the day (the tax group id).</summary>
    public long? TaxGroupId { get; set; }

    /// <summary>The document type that posted it, such as INV or BIL; all when blank.</summary>
    [MaxLength(10, ErrorMessage = "Source cannot exceed 10 characters.")]
    public string? Source { get; set; }

    /// <summary>The page of postings wanted, from 1.</summary>
    [Range(1, 100000, ErrorMessage = "Page number must be 1 or more.")]
    public int PageNumber { get; set; } = 1;

    /// <summary>How many postings a page holds.</summary>
    [Range(1, 500, ErrorMessage = "Page size must be between 1 and 500.")]
    public int PageSize { get; set; } = 100;

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>What the request is held to beyond its annotations.</summary>
public static class AccountTransactionLimits
{
    /// <summary>The most accounts one request may name.</summary>
    public const int MaxAccounts = 100;
}

/// <summary>One posting to an account.</summary>
public sealed class AccountTransactionRowDto
{
    public long LedgerId { get; set; }

    public DateOnly Date { get; set; }

    /// <summary>The document's own number; for a posting written before the ledger carried one, its type and id joined, such as INV-42.</summary>
    [MaxLength(60, ErrorMessage = "Document number cannot exceed 60 characters.")]
    public string DocumentNo { get; set; } = string.Empty;

    [MaxLength(10, ErrorMessage = "Source cannot exceed 10 characters.")]
    public string SourceCode { get; set; } = string.Empty;

    public long TransactionId { get; set; }

    /// <summary>The manual journal behind it, when there is one.</summary>
    public long? JournalId { get; set; }

    /// <summary>The account the other side of the document was posted to; "Various" when it was posted to several, null when there is none.</summary>
    [MaxLength(200, ErrorMessage = "Account name cannot exceed 200 characters.")]
    public string? RelatedAccount { get; set; }

    public long? ContactId { get; set; }

    [MaxLength(30, ErrorMessage = "Contact code cannot exceed 30 characters.")]
    public string? ContactCode { get; set; }

    [MaxLength(200, ErrorMessage = "Contact name cannot exceed 200 characters.")]
    public string? ContactName { get; set; }

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    /// <summary>A manual journal's own reference; null for every other posting.</summary>
    [MaxLength(200, ErrorMessage = "Reference cannot exceed 200 characters.")]
    public string? Reference { get; set; }

    /// <summary>The rate this leg was taxed at, on its date; null on every leg that is not a tax leg.</summary>
    public decimal? TaxRate { get; set; }

    [MaxLength(50, ErrorMessage = "Tax name cannot exceed 50 characters.")]
    public string? TaxName { get; set; }

    /// <summary>The contact's licence number, a drug licence or a food registration, when it holds one that was valid on the day.</summary>
    [MaxLength(100, ErrorMessage = "Permit number cannot exceed 100 characters.")]
    public string? PermitNo { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>The rate the document was booked at; never looked up again.</summary>
    public decimal ExchangeRate { get; set; }

    /// <summary>In the document's own currency.</summary>
    public decimal DebitSource { get; set; }

    public decimal CreditSource { get; set; }

    /// <summary>In the branch's base currency.</summary>
    public decimal Debit { get; set; }

    public decimal Credit { get; set; }

    /// <summary>The account's balance after this posting, on its normal side.</summary>
    public decimal RunningBalance { get; set; }
}

/// <summary>One account: its balances for the whole period, and the postings of this page.</summary>
public sealed class AccountTransactionAccountDto
{
    public long AccountId { get; set; }

    [MaxLength(20, ErrorMessage = "Account code cannot exceed 20 characters.")]
    public string AccountCode { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "Account name cannot exceed 200 characters.")]
    public string AccountName { get; set; } = string.Empty;

    public int AccountTypeId { get; set; }

    public bool IsContra { get; set; }

    /// <summary>Where the account grows, and the side its balances here are on.</summary>
    public LedgerSide NormalSide { get; set; }

    /// <summary>
    /// On the normal side. A balance-sheet account opens at everything before the
    /// period; an income or expense account at what was posted since its financial
    /// year began, as on the General ledger and the Trial balance.
    /// </summary>
    public decimal OpeningBalance { get; set; }

    /// <summary>On the normal side: the opening balance plus the period's movement.</summary>
    public decimal ClosingBalance { get; set; }

    /// <summary>Base currency, over every posting of the period, not only those on this page.</summary>
    public decimal TotalDebit { get; set; }

    public decimal TotalCredit { get; set; }

    /// <summary>How many postings the account has in the period.</summary>
    public int PostingCount { get; set; }

    /// <summary>The account's earlier postings are on a page before this one, so its opening row is there.</summary>
    public bool HasEarlierRows { get; set; }

    /// <summary>The account's later postings are on a page after this one, so its closing row is there.</summary>
    public bool HasLaterRows { get; set; }

    public List<AccountTransactionRowDto> Rows { get; set; } = [];
}

/// <summary>A page of the period's postings, grouped by account, with the totals of all of them.</summary>
public sealed class AccountTransactionResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    /// <summary>The first day of the financial year the period starts in.</summary>
    public DateOnly FinancialYearStart { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    /// <summary>Every posting the filters match, not only those on this page.</summary>
    public int TotalPostings { get; set; }

    /// <summary>The accounts those postings are to, and any account asked for by name that had none.</summary>
    public int TotalAccounts { get; set; }

    /// <summary>Base currency, over every posting the filters match.</summary>
    public decimal TotalDebit { get; set; }

    public decimal TotalCredit { get; set; }

    public List<AccountTransactionAccountDto> Accounts { get; set; } = [];
}
