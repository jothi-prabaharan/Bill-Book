using System.ComponentModel.DataAnnotations;
using Accounting.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>
/// Money arriving — <c>RCM</c>. An invoice settled, an advance taken from a
/// customer, a supplier refunding what was overpaid.
///
/// The mirror of <see cref="SpendMoney"/>, and its own table for the same
/// reason: each document then carries exactly the columns it needs. A receipt
/// has a payer and no destination account, so <see cref="ContactId"/> is
/// <c>NOT NULL</c> and there is no destination column at all.
///
/// There is no transaction type column. This table <i>is</i> <c>RCM</c>, and the
/// code is supplied when the document reaches the ledger.
/// </summary>
public class ReceiveMoney : OrgScopedEntity
{
    public long ReceiveMoneyId { get; set; }

    /// <summary>
    /// Taken from the <c>RCM</c> series <b>at post, not at draft</b>, so it is
    /// null while the document is being keyed.
    /// </summary>
    [MaxLength(30, ErrorMessage = "Transaction number cannot exceed 30 characters.")]
    public string? TransactionNo { get; set; }

    public DateOnly TransactionDate { get; set; }

    /// <summary>The organization's own account the money arrives into.</summary>
    [Range(1, long.MaxValue, ErrorMessage = "Choose the account the money arrives into.")]
    public long BankAccountId { get; set; }

    /// <summary>
    /// Who paid. Required — money never arrives from nobody — and no FK, because
    /// Contacts owns that table and lives in its own service.
    /// </summary>
    [Range(1, long.MaxValue, ErrorMessage = "Choose who the money came from.")]
    public long ContactId { get; set; }

    /// <summary>
    /// The total received. Its detail lines must add up to exactly this before
    /// the document can post.
    /// </summary>
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335",
        ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Currency code is required.")]
    [MaxLength(3, ErrorMessage = "Currency code must be a 3-letter code.")]
    public string CurrencyCode { get; set; } = null!;

    /// <summary>
    /// Snapshot at <see cref="TransactionDate"/>. Never looked up live — the
    /// difference between this rate and the settled invoice's <b>is</b> the
    /// realized exchange gain or loss.
    /// </summary>
    [Range(typeof(decimal), "0.00000001", "79228162514264337593543950335",
        ErrorMessage = "Exchange rate must be greater than zero.")]
    public decimal ExchangeRate { get; set; } = 1m;

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;

    /// <summary>A cheque number, a UTR, a UPI reference.</summary>
    [MaxLength(50, ErrorMessage = "Reference cannot exceed 50 characters.")]
    public string? ReferenceNo { get; set; }

    public DateOnly? ReferenceDate { get; set; }

    public string? Memo { get; set; }

    /// <summary>
    /// The document this payment is <i>about</i>, when it is about exactly one —
    /// an invoice being settled, a credit note being refunded. `INV` or `CRN`
    /// here, `BIL` or `DBN` on the spend side.
    ///
    /// <b>The lines remain authoritative.</b> This is the header's convenience
    /// for the ordinary case: one receipt, one document. A receipt split across
    /// three invoices leaves it null, because no single document is what the receipt
    /// is about — the three <see cref="ReceiveMoneyDetail"/> rows are. Anything
    /// reconciling an invoice must read the lines; anything listing receipts can read
    /// this and avoid a join.
    ///
    /// No FK, as on the line: the documents live in Sales and Purchase.
    /// </summary>
    [MaxLength(3, ErrorMessage = "Mapped transaction type code must be a 3-letter code.")]
    public string? MappingTransactionTypeCode { get; set; }

    public long? MappingTransactionId { get; set; }

    public MoneyDocumentStatus Status { get; set; } = MoneyDocumentStatus.Draft;

    public DateTimeOffset? PostedAt { get; set; }

    public Guid? PostedBy { get; set; }

    public DateTimeOffset? VoidedAt { get; set; }

    public Guid? VoidedBy { get; set; }

    [MaxLength(300, ErrorMessage = "Void reason cannot exceed 300 characters.")]
    public string? VoidReason { get; set; }
}
