using System.ComponentModel.DataAnnotations;
using Banking.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Banking.Entity.TableEntities;

/// <summary>
/// Money leaving, arriving, or moving between the organization's own accounts —
/// <c>SPM</c>, <c>RCM</c> and <c>TRM</c> in one table, discriminated by
/// <see cref="TransactionTypeCode"/>.
///
/// <b>One table rather than three.</b> The three share every column that matters:
/// a bank account, a date, an amount, a currency and rate, a payment method, a
/// reference and a status. What differs is a destination account on a transfer
/// and a contact on the other two — two nullable columns against three
/// near-identical tables, three list screens and three numbering paths. The same
/// reasoning the sales documents are being built under.
///
/// <b>What a document means is not its type.</b> A payment, a supplier deposit
/// and a refund are all <c>SPM</c>; what separates them is the ledger source on
/// each <see cref="MoneyTransactionDetail"/>. That is deliberate — a payment
/// that runs past what was owed is a bill payment <i>and</i> a deposit at once,
/// so the meaning cannot live on the header.
///
/// Balances are never stored here. They come from the ledger.
/// </summary>
public class MoneyTransaction : OrgScopedEntity
{
    public long MoneyTransactionId { get; set; }

    /// <summary>
    /// <c>SPM</c>, <c>RCM</c> or <c>TRM</c>, from <c>mst.TransactionTypes</c>.
    /// Stored as its code with no FK, because the master lives in another
    /// database and a ledger row should read without a join.
    /// </summary>
    [Required(ErrorMessage = "Transaction type code is required.")]
    [MaxLength(3, ErrorMessage = "Transaction type code must be a 3-letter code.")]
    public string TransactionTypeCode { get; set; } = null!;

    /// <summary>
    /// The document number, taken from this type's series <b>at post, not at
    /// draft</b> — so it is null while the document is being keyed. A number
    /// taken when the form opens is a number lost every time someone changes
    /// their mind, and these series have to run without gaps.
    /// </summary>
    [MaxLength(30, ErrorMessage = "Transaction number cannot exceed 30 characters.")]
    public string? TransactionNo { get; set; }

    public DateOnly TransactionDate { get; set; }

    /// <summary>
    /// The organization's own account the money moves from or to. On a transfer
    /// this is the <b>source</b>.
    /// </summary>
    public long BankAccountId { get; set; }

    /// <summary>
    /// The destination, on a transfer and nowhere else. A transfer is the one
    /// money document with no counterparty — both sides are the organization's.
    /// </summary>
    public long? ToBankAccountId { get; set; }

    /// <summary>
    /// Who the money moved to or from. Null on a transfer, and no FK — Contacts
    /// owns that table and lives in its own service.
    /// </summary>
    public long? ContactId { get; set; }

    /// <summary>
    /// The total moved. Its detail lines must add up to exactly this before the
    /// document can post — enforced by a deferred trigger, because it spans rows.
    /// </summary>
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Currency code is required.")]
    [MaxLength(3, ErrorMessage = "Currency code must be a 3-letter code.")]
    public string CurrencyCode { get; set; } = null!;

    /// <summary>
    /// Snapshot at <see cref="TransactionDate"/>. <b>Never looked up live</b> —
    /// and on this document doubly so: the difference between this rate and the
    /// settled document's is the realized exchange gain or loss, so a rate that
    /// moved on its own would invent one.
    /// </summary>
    public decimal ExchangeRate { get; set; } = 1m;

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;

    /// <summary>A cheque number, a UTR, a UPI reference — whatever the bank will be asked about.</summary>
    [MaxLength(50, ErrorMessage = "Reference cannot exceed 50 characters.")]
    public string? ReferenceNo { get; set; }

    /// <summary>Instrument date, when a cheque is written for a date other than today's.</summary>
    public DateOnly? ReferenceDate { get; set; }

    public string? Memo { get; set; }

    public MoneyTransactionStatus Status { get; set; } = MoneyTransactionStatus.Draft;

    public DateTimeOffset? PostedAt { get; set; }

    /// <summary>Who posted it. A plain Guid — users live in the master database.</summary>
    public Guid? PostedBy { get; set; }

    public DateTimeOffset? VoidedAt { get; set; }

    public Guid? VoidedBy { get; set; }

    /// <summary>Why it was withdrawn. Worth more than the fact that it was.</summary>
    [MaxLength(300, ErrorMessage = "Void reason cannot exceed 300 characters.")]
    public string? VoidReason { get; set; }
}
