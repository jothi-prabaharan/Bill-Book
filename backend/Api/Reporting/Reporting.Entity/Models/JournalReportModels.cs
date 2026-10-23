using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Journal Report (TK-217): every journal posted in a period with its lines,
// who created and posted it and its status: the audit view of what was posted by
// hand and by the system. Benchmarked on Xero's Journal Report, AutoCount's
// Journal Entry Listing and Odoo's Journal Audit. Every journal is a document:
// a manual journal, or the posting a sales invoice, a bill, a payment or a stock
// adjustment made to the ledger. Amounts are in the document's own currency and
// in the branch's base currency.

/// <summary>
/// What the report is asked for: a period, with both ends required (D-67), and
/// optionally one source, one status and one account. The result comes a page of
/// journals at a time. The branch is always the token's: a request naming another
/// branch is refused with 403.
/// </summary>
public sealed class JournalReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>The document type that posted it, such as JRN for a manual journal or INV for a sales invoice; all when blank.</summary>
    [MaxLength(10, ErrorMessage = "Source cannot exceed 10 characters.")]
    public string? Source { get; set; }

    /// <summary>
    /// Posted or Reversed, or Draft for journals not yet posted, which have no
    /// ledger rows. Blank is every journal that reached the ledger, posted and
    /// reversed together.
    /// </summary>
    public JournalStatus? Status { get; set; }

    /// <summary>Only the journals that have a line on this account, each with all its lines.</summary>
    public long? AccountId { get; set; }

    /// <summary>The page of journals wanted, from 1.</summary>
    [Range(1, 100000, ErrorMessage = "Page number must be 1 or more.")]
    public int PageNumber { get; set; } = 1;

    /// <summary>How many journals a page holds.</summary>
    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; set; } = 25;

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>One line of a journal.</summary>
public sealed class JournalReportLineDto
{
    public int LineNumber { get; set; }

    public long AccountId { get; set; }

    [MaxLength(20, ErrorMessage = "Account code cannot exceed 20 characters.")]
    public string AccountCode { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "Account name cannot exceed 200 characters.")]
    public string AccountName { get; set; } = string.Empty;

    public long? ContactId { get; set; }

    /// <summary>The contact the line names; null when it names none, and always null on a draft.</summary>
    [MaxLength(200, ErrorMessage = "Contact name cannot exceed 200 characters.")]
    public string? ContactName { get; set; }

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

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
}

/// <summary>One journal: its header, its lines and the totals that must agree.</summary>
public sealed class JournalReportEntryDto
{
    /// <summary>The document type that posted it: JRN, INV, BIL, SPM.</summary>
    [MaxLength(10, ErrorMessage = "Source cannot exceed 10 characters.")]
    public string SourceCode { get; set; } = string.Empty;

    /// <summary>The document's id in its own service; a manual journal's is its journal id.</summary>
    public long TransactionId { get; set; }

    /// <summary>The manual journal behind it, when there is one; null for a document's own posting.</summary>
    public long? JournalId { get; set; }

    public DateOnly Date { get; set; }

    /// <summary>
    /// The document's own number; for a posting written before the ledger carried
    /// one, its type and id joined, such as INV-42; for a draft, "Draft" and its id.
    /// </summary>
    [MaxLength(60, ErrorMessage = "Number cannot exceed 60 characters.")]
    public string Number { get; set; } = string.Empty;

    [MaxLength(100, ErrorMessage = "Reference cannot exceed 100 characters.")]
    public string? Reference { get; set; }

    [MaxLength(500, ErrorMessage = "Narration cannot exceed 500 characters.")]
    public string? Narration { get; set; }

    /// <summary>A document's own posting is Posted; a manual journal's is its own status.</summary>
    public JournalStatus Status { get; set; }

    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
    public string? CreatedByName { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }

    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
    public string? PostedByName { get; set; }

    public DateTimeOffset? PostedAt { get; set; }

    /// <summary>The number of the journal this one reverses, when it is a reversal.</summary>
    [MaxLength(60, ErrorMessage = "Number cannot exceed 60 characters.")]
    public string? ReversesNumber { get; set; }

    /// <summary>The number of the journal that reversed this one, when it has been reversed.</summary>
    [MaxLength(60, ErrorMessage = "Number cannot exceed 60 characters.")]
    public string? ReversedByNumber { get; set; }

    public List<JournalReportLineDto> Lines { get; set; } = [];

    /// <summary>Base currency; the lines added up.</summary>
    public decimal TotalDebit { get; set; }

    public decimal TotalCredit { get; set; }

    /// <summary>The two totals agreeing: every journal that reached the ledger does, since each posting is checked when it is written.</summary>
    public bool IsBalanced { get; set; }
}

/// <summary>A page of the period's journals, with the totals of all of them.</summary>
public sealed class JournalReportResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    /// <summary>Every journal the filters match, not only those on this page.</summary>
    public int TotalJournals { get; set; }

    /// <summary>Base currency, over every journal the filters match. Equal to the Trial Balance's movement for the same dates when no filter narrows it.</summary>
    public decimal TotalDebit { get; set; }

    public decimal TotalCredit { get; set; }

    /// <summary>The period's two totals agreeing.</summary>
    public bool IsBalanced { get; set; }

    public List<JournalReportEntryDto> Journals { get; set; } = [];
}
