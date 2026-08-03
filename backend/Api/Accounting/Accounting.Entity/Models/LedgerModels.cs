using System.ComponentModel.DataAnnotations;
using Accounting.Entity.Enums;

namespace Accounting.Entity.Models;

/// <summary>
/// A complete, balanced posting, described by the service that owns the document
/// rather than by Accounting.
///
/// The caller names accounts by their <c>AccountSystemName</c> — "Inventory",
/// "Cost of Goods Sold" — and names a sub-dimension by what it refers to, an
/// item or a contact. It never sends an <c>AccountId</c>, because an account id
/// is a per-organization number in a database the caller does not read, and
/// resolving one on the far side is how a leg ends up on the wrong account.
///
/// <b>A document has more than one kind of leg, and they only balance
/// together.</b> An invoice credits Sales Revenue per line, credits Output GST
/// per rate, and debits Accounts Receivable once for the whole document — no
/// subset of those balances on its own. So the leg type and the document line
/// belong to the leg, not to the request, and one call carries the lot.
///
/// <b>A document can also be more than one kind of thing at once.</b> Paying
/// ₹11,000 against a ₹10,000 bill is a bill payment and a vendor prepayment on
/// one document: ₹10,000 settles the bill and ₹1,000 becomes an advance. Stamp
/// the whole thing "overpayment" and a payables report filtering on bill
/// payments silently misses ₹10,000 of a real one — so the source is on the leg
/// too.
///
/// <b>Replace, never append.</b> A leg is identified by
/// (<see cref="TransactionTypeCode"/>, <see cref="TransactionId"/>,
/// <see cref="LedgerLegRequest.TransactionDetailId"/>,
/// <see cref="LedgerLegRequest.LedgerTypeId"/>), and a posting replaces exactly
/// the keys its legs name rather than doubling them. That is what makes a caller
/// safe to retry, and what lets a restated cost correct itself.
///
/// <b>Withdrawal is the asymmetric case.</b> An empty <see cref="Legs"/> list
/// has no keys to infer, so it names its leg types in
/// <see cref="WithdrawLedgerTypeIds"/> and clears them across the whole
/// document. A posting cannot do the same, because Inventory posts a document
/// one line at a time — a document-wide replace would have line two erase line
/// one's rows.
/// </summary>
public class PostLedgerRequest
{
    /// <summary>
    /// Which database. Carried in the body because the callers include a
    /// background worker holding no user token.
    /// </summary>
    public Guid CustomerId { get; set; }

    /// <summary>Which branch's books. Every row written is stamped with it.</summary>
    public Guid OrgId { get; set; }

    [Required(ErrorMessage = "Transaction type code is required.")]
    [MaxLength(3, ErrorMessage = "Transaction type code must be a 3-letter code.")]
    public string TransactionTypeCode { get; set; } = null!;

    public long TransactionId { get; set; }

    public DateOnly LedgerDate { get; set; }

    /// <summary>
    /// Left null for a posting in the branch's own books, which is every posting
    /// today. Supplying it also requires <see cref="ExchangeRate"/>.
    /// </summary>
    [MaxLength(3, ErrorMessage = "Currency code must be a 3-letter code.")]
    public string? CurrencyCode { get; set; }

    /// <summary>
    /// Units of the transaction currency per unit of base. Snapshot at the
    /// posting date — never a live rate.
    /// </summary>
    public decimal? ExchangeRate { get; set; }

    public long? ContactId { get; set; }

    /// <summary>The row in the calling service that produced this posting.</summary>
    public long? SourceDocumentId { get; set; }

    /// <summary>
    /// The manual journal behind this posting. Set only when
    /// <see cref="LedgerSourceId"/> is 12 (Journal) — it is what lets a ledger
    /// row drill back to the journal that wrote it rather than only to a
    /// document type and an id.
    /// </summary>
    public long? JournalId { get; set; }

    /// <summary>
    /// Which leg types to clear when <see cref="Legs"/> is empty. Required for a
    /// withdrawal and ignored otherwise: with no legs there is nothing to infer
    /// the types from, and a withdrawal that guessed would either leave rows
    /// behind or take another writer's.
    /// </summary>
    public List<int> WithdrawLedgerTypeIds { get; set; } = [];

    public List<LedgerLegRequest> Legs { get; set; } = [];
}

/// <summary>
/// One leg. Debit xor credit, never both, never negative.
///
/// The leg carries its own <see cref="LedgerTypeId"/> and
/// <see cref="TransactionDetailId"/> because a single document produces several
/// of each at once, and it is the pair of them that says which rows this leg
/// replaces. It carries its own <see cref="LedgerSourceId"/> because a single
/// document can be several things at once.
/// </summary>
public class LedgerLegRequest
{
    [Range(1, 6, ErrorMessage = "Ledger type must be one of the six leg types.")]
    public int LedgerTypeId { get; set; }

    /// <summary>
    /// What produced this leg, from <c>mst.LedgerSources</c>. On the leg rather
    /// than the request: an overpayment settles a bill with part of itself and
    /// leaves the rest as an advance, and those two halves are different sources
    /// on one document.
    /// </summary>
    [Range(1, 19, ErrorMessage = "Ledger source must be one of the nineteen sources.")]
    public int LedgerSourceId { get; set; }

    /// <summary>The document line, or 0 when the leg is not line-level.</summary>
    public long TransactionDetailId { get; set; }

    /// <summary>
    /// How a caller in another service names the account: "Inventory", "Cost of
    /// Goods Sold". Required from outside Accounting, and the reason is in the
    /// class summary above.
    ///
    /// Only seeded control accounts have one. An account a user added has none,
    /// which is why <see cref="AccountId"/> exists.
    /// </summary>
    [MaxLength(200, ErrorMessage = "Account system name cannot exceed 200 characters.")]
    public string? AccountSystemName { get; set; }

    /// <summary>
    /// The account itself, for callers inside Accounting — a manual journal names
    /// any account in the chart, and most of those have no system name at all.
    ///
    /// <b>Not for another service.</b> An account id is a per-organization number
    /// in a database the caller does not read, and resolving one on the far side
    /// is how a leg lands on the wrong account. Accounting owns that database, so
    /// for Accounting the id is the more precise of the two, not the looser.
    /// </summary>
    public long? AccountId { get; set; }

    /// <summary>
    /// Set together with <see cref="SubAccountReferenceId"/> to post against the
    /// sub-account under this control account — the item, contact or tax rate the
    /// leg is really about. Left null for legs with no sub-dimension.
    /// </summary>
    public SubAccountReferenceType? SubAccountReferenceType { get; set; }

    public long? SubAccountReferenceId { get; set; }

    /// <summary>
    /// The sub-account directly, the counterpart of <see cref="AccountId"/> and
    /// subject to the same rule: inside Accounting only. Checked against the
    /// leg's own account, so a receivable sub-account cannot be hung under a
    /// payable line.
    /// </summary>
    public long? SubAccountId { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335",
        ErrorMessage = "Debit amount cannot be negative.")]
    public decimal DebitAmount { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335",
        ErrorMessage = "Credit amount cannot be negative.")]
    public decimal CreditAmount { get; set; }

    [MaxLength(500, ErrorMessage = "Transaction description cannot exceed 500 characters.")]
    public string? TransactionDesc { get; set; }
}

public class PostLedgerResponse
{
    /// <summary>Rows written by this call.</summary>
    public int Posted { get; set; }

    /// <summary>Rows of an earlier posting under the same key that this call replaced.</summary>
    public int Replaced { get; set; }
}

/// <summary>
/// Why a posting was refused. Every value is a caller error except
/// <see cref="BaseCurrencyUnavailable"/>, which is transient and worth retrying.
/// </summary>
public enum PostLedgerOutcome
{
    Ok = 0,

    /// <summary>Debits and credits do not agree in base currency.</summary>
    Unbalanced = 1,

    /// <summary>A leg carries both a debit and a credit, or neither.</summary>
    LegNotExclusive = 2,

    /// <summary>The chart of accounts has no account with that system name.</summary>
    AccountMissing = 3,

    /// <summary>The named item, contact or tax rate has no sub-account under that account.</summary>
    SubAccountMissing = 4,

    /// <summary>The account is frozen for posting.</summary>
    AccountLocked = 5,

    /// <summary>No tenant was supplied, so there is nothing to post into.</summary>
    TenantMissing = 6,

    /// <summary>
    /// The branch's base currency could not be read. Transient — the posting has
    /// not happened and the caller should try again rather than assume one.
    /// </summary>
    BaseCurrencyUnavailable = 7,

    /// <summary>
    /// A withdrawal that named no leg types. There are no legs to infer them
    /// from, so the request says nothing about what should be removed.
    /// </summary>
    WithdrawalTypesMissing = 8,
}

public sealed record PostLedgerResult(
    PostLedgerOutcome Outcome, int Posted, int Replaced, string? Detail = null);
