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
/// <b>Replace, never append.</b> A posting is identified by
/// (<see cref="TransactionTypeCode"/>, <see cref="TransactionId"/>,
/// <see cref="TransactionDetailId"/>, <see cref="LedgerTypeId"/>) and posting
/// the same key twice replaces the earlier rows rather than doubling them. That
/// is what makes a caller safe to retry, and what lets a restated cost correct
/// itself. An empty <see cref="Legs"/> list removes the set, which is how a
/// posting that should no longer exist is withdrawn.
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

    /// <summary>The document line, or 0 when the posting is not line-level.</summary>
    public long TransactionDetailId { get; set; }

    [Range(1, 6, ErrorMessage = "Ledger type must be one of the six leg types.")]
    public int LedgerTypeId { get; set; }

    [Range(1, 15, ErrorMessage = "Ledger source must be one of the fifteen sources.")]
    public int LedgerSourceId { get; set; }

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

    public List<LedgerLegRequest> Legs { get; set; } = [];
}

/// <summary>One leg. Debit xor credit, never both, never negative.</summary>
public class LedgerLegRequest
{
    [Required(ErrorMessage = "An account system name is required on every leg.")]
    [MaxLength(200, ErrorMessage = "Account system name cannot exceed 200 characters.")]
    public string AccountSystemName { get; set; } = null!;

    /// <summary>
    /// Set together with <see cref="SubAccountReferenceId"/> to post against the
    /// sub-account under this control account — the item, contact or tax rate the
    /// leg is really about. Left null for legs with no sub-dimension.
    /// </summary>
    public SubAccountReferenceType? SubAccountReferenceType { get; set; }

    public long? SubAccountReferenceId { get; set; }

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
}

public sealed record PostLedgerResult(
    PostLedgerOutcome Outcome, int Posted, int Replaced, string? Detail = null);
