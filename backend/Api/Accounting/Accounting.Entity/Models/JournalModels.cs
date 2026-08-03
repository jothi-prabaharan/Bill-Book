using System.ComponentModel.DataAnnotations;

namespace Accounting.Entity.Models;

/// <summary>
/// A manual journal as the screen sends it. Header plus every line, in one
/// request: a journal is only meaningful as a whole, and a line saved on its own
/// would leave the entry unbalanced in a way nobody asked for.
/// </summary>
public class SaveJournalRequest
{
    public DateOnly JournalDate { get; set; }

    /// <summary>Null means the branch's own currency, which is the ordinary case.</summary>
    [MaxLength(3, ErrorMessage = "Currency code must be a 3-letter code.")]
    public string? CurrencyCode { get; set; }

    /// <summary>
    /// Units of the transaction currency per unit of base, as at
    /// <see cref="JournalDate"/>. Never looked up live.
    /// </summary>
    [Range(typeof(decimal), "0.00000001", "79228162514264337593543950335",
        ErrorMessage = "Exchange rate must be greater than zero.")]
    public decimal? ExchangeRate { get; set; }

    [MaxLength(200, ErrorMessage = "Reference cannot exceed 200 characters.")]
    public string? Reference { get; set; }

    public string? Memo { get; set; }

    public List<SaveJournalLineRequest> Lines { get; set; } = [];
}

/// <summary>One line. Debit xor credit, never both, never negative.</summary>
public class SaveJournalLineRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose an account for every line.")]
    public long AccountId { get; set; }

    /// <summary>
    /// The sub-dimension under a control account — a contact, an item, a tax
    /// rate. Null for a line that has none.
    /// </summary>
    public long? SubAccountId { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335",
        ErrorMessage = "A debit cannot be negative.")]
    public decimal DebitAmount { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335",
        ErrorMessage = "A credit cannot be negative.")]
    public decimal CreditAmount { get; set; }

    [MaxLength(300, ErrorMessage = "Line memo cannot exceed 300 characters.")]
    public string? LineMemo { get; set; }
}

/// <summary>A journal on the list screen. Totals included so the list can show the imbalance.</summary>
public class JournalListItem
{
    public long JournalId { get; set; }

    /// <summary>Null while the entry is a draft — a number is taken at post.</summary>
    public string? JournalNo { get; set; }

    public DateOnly JournalDate { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public decimal ExchangeRate { get; set; }

    public string? Reference { get; set; }

    public string? Memo { get; set; }

    public string Status { get; set; } = null!;

    public DateTimeOffset? PostedAt { get; set; }

    public int LineCount { get; set; }

    public decimal TotalDebit { get; set; }

    public decimal TotalCredit { get; set; }

    /// <summary>Set on a reversing entry: the journal it offsets.</summary>
    public long? ReversesJournalId { get; set; }

    public string? ReversesJournalNo { get; set; }

    /// <summary>Set on a reversed entry: the journal that offset it.</summary>
    public long? ReversedByJournalId { get; set; }

    public string? ReversedByJournalNo { get; set; }
}

/// <summary>One journal with its lines, for the entry screen.</summary>
public class JournalDetailView : JournalListItem
{
    public List<JournalLineView> Lines { get; set; } = [];
}

public class JournalLineView
{
    public long JournalDetailId { get; set; }

    public int LineNumber { get; set; }

    public long AccountId { get; set; }

    public string AccountCode { get; set; } = null!;

    public string AccountName { get; set; } = null!;

    public long? SubAccountId { get; set; }

    public string? SubAccountName { get; set; }

    public decimal DebitAmount { get; set; }

    public decimal CreditAmount { get; set; }

    public decimal DebitAmountBase { get; set; }

    public decimal CreditAmountBase { get; set; }

    public string? LineMemo { get; set; }

    /// <summary>The original line this one offsets, on a reversing entry.</summary>
    public long? ReversesJournalDetailId { get; set; }

    public long? ReversedByJournalDetailId { get; set; }
}

/// <summary>What to reverse a journal on. The date is the only choice a reversal offers.</summary>
public class ReverseJournalRequest
{
    /// <summary>
    /// Null reverses on the original entry's own date, which is what an
    /// immediate correction wants. A later date is what a reversal in the period
    /// the mistake was found wants, and both are legitimate.
    /// </summary>
    public DateOnly? ReversalDate { get; set; }

    [MaxLength(200, ErrorMessage = "Reference cannot exceed 200 characters.")]
    public string? Reference { get; set; }

    public string? Memo { get; set; }
}

/// <summary>Why a journal was refused. Every value is something the user can act on.</summary>
public enum SaveJournalOutcome
{
    Ok = 0,
    NotFound = 1,

    /// <summary>An edit, a delete or a post aimed at an entry that is no longer a draft.</summary>
    NotDraft = 2,

    /// <summary>A line carries both a debit and a credit, or neither.</summary>
    LineNotExclusive = 3,

    /// <summary>Debits and credits do not agree. Refused on post; allowed on a draft.</summary>
    Unbalanced = 4,

    /// <summary>Nothing to post: no lines at all.</summary>
    NoLines = 5,

    /// <summary>A line names an account that does not exist in this branch.</summary>
    AccountMissing = 6,

    /// <summary>A line names an account frozen for posting, or one not open to hand entries.</summary>
    AccountNotPostable = 7,

    /// <summary>A line names a sub-account that does not sit under its account.</summary>
    SubAccountMismatch = 8,

    /// <summary>The entry is already reversed. A reversal of a reversal is a new entry, not this.</summary>
    AlreadyReversed = 9,

    /// <summary>Only a posted entry can be reversed — a draft is deleted instead.</summary>
    NotPosted = 10,

    /// <summary>The branch's base currency could not be read. Transient.</summary>
    BaseCurrencyUnavailable = 11,

    /// <summary>The ledger refused the posting. The detail says why.</summary>
    PostingRefused = 12,

    /// <summary>
    /// The books are closed to this caller for that date. Not a validation
    /// failure — the entry may be perfectly good, and dating it later will post
    /// it.
    /// </summary>
    PeriodClosed = 13,
}

public sealed record SaveJournalResult(
    SaveJournalOutcome Outcome, long JournalId = 0, string? Detail = null);
