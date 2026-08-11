using System.ComponentModel.DataAnnotations;
using Banking.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Banking.Entity.TableEntities;

/// <summary>
/// One movement as the bank recorded it.
///
/// <b>Money in xor money out, never both, never negative</b> — the same rule the
/// ledger holds, and for the same reason. Banks export a signed amount about as
/// often as they export two columns, so the import normalizes: a negative in a
/// single amount column becomes a credit here. A signed column kept as-is would
/// mean no total could be summed without knowing which convention its importer
/// used.
///
/// <b><see cref="RowHash"/> is what makes re-import safe</b>, and it is the whole
/// reason this table can be used the way people actually use statements. Nobody
/// downloads a clean, non-overlapping statement each period; they download the
/// last thirty days every week. So the same movement arrives four times, and it
/// has to be recognised. The hash is over what the bank said — date, amount,
/// description, reference — plus an occurrence number, because two identical
/// ₹500 withdrawals on one day are two movements rather than a duplicate, and a
/// hash without it would silently drop the second.
/// </summary>
public class BankStatementLine : OrgScopedEntity
{
    public long BankStatementLineId { get; set; }

    public long BankStatementId { get; set; }

    /// <summary>
    /// Denormalized from the statement so a line can be found, matched and
    /// deduplicated without joining its header — which is what every query on
    /// this table does.
    /// </summary>
    public long BankAccountId { get; set; }

    /// <summary>Position in the file. What the screen orders by within a date.</summary>
    public int LineNumber { get; set; }

    /// <summary>When the bank says it happened.</summary>
    public DateOnly TransactionDate { get; set; }

    /// <summary>When it cleared, when the file distinguishes the two.</summary>
    public DateOnly? ValueDate { get; set; }

    /// <summary>
    /// The narration, verbatim. Ugly, abbreviated and the single most useful
    /// column on the screen — it is what a person reads to work out what a line
    /// was, and what the matcher reads to guess.
    /// </summary>
    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    /// <summary>A cheque number, a UPI reference, an NEFT UTR. What matching leans on hardest.</summary>
    [MaxLength(100, ErrorMessage = "Reference cannot exceed 100 characters.")]
    public string? ReferenceNo { get; set; }

    /// <summary>Money leaving the account, from the bank's point of view.</summary>
    public decimal WithdrawalAmount { get; set; }

    /// <summary>Money arriving.</summary>
    public decimal DepositAmount { get; set; }

    /// <summary>
    /// The balance the bank printed after this line, when the file carried one.
    /// Not recomputed and not trusted for arithmetic — it is here so a person
    /// comparing the screen with the paper sees the same numbers in the same
    /// places.
    /// </summary>
    public decimal? RunningBalance { get; set; }

    public StatementLineStatus Status { get; set; } = StatementLineStatus.Unmatched;

    /// <summary>
    /// The money document this line is the bank's version of — <c>SPM</c>,
    /// <c>RCM</c> or <c>TRM</c>. Set together with
    /// <see cref="MatchedTransactionId"/>, and only while
    /// <see cref="Status"/> is <see cref="StatementLineStatus.Matched"/>.
    /// </summary>
    [MaxLength(3, ErrorMessage = "Matched transaction type code must be a 3-letter code.")]
    public string? MatchedTransactionTypeCode { get; set; }

    public long? MatchedTransactionId { get; set; }

    /// <summary>
    /// Whether the match was found by the matcher or made by a person. Worth
    /// keeping: a reconciliation queried months later is asked "who decided
    /// this?", and "the computer, on these grounds" and "a person, deliberately"
    /// are different answers.
    /// </summary>
    public bool MatchedAutomatically { get; set; }

    public DateTimeOffset? MatchedAt { get; set; }

    public Guid? MatchedBy { get; set; }

    /// <summary>Why a line was set aside. Required to ignore one.</summary>
    [MaxLength(300, ErrorMessage = "Note cannot exceed 300 characters.")]
    public string? Note { get; set; }

    /// <summary>
    /// Identity of the movement as the bank described it — see the class summary.
    /// Unique per bank account, which is what makes importing an overlapping
    /// period add only what is new.
    /// </summary>
    [Required(ErrorMessage = "Row hash is required.")]
    [MaxLength(64, ErrorMessage = "Row hash cannot exceed 64 characters.")]
    public string RowHash { get; set; } = null!;
}
