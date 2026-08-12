using System.ComponentModel.DataAnnotations;
using Accounting.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>
/// Money leaving the organization — <c>SPM</c>. A bill paid, a deposit put down
/// with a supplier, a customer refunded.
///
/// <b>Its own table, not a row in a shared one.</b> Spend, receive and transfer
/// are three tables rather than one discriminated by a type code: each then
/// carries exactly the columns it needs and nothing it does not. A payment has a
/// payee and no destination account, so <see cref="ContactId"/> is
/// <c>NOT NULL</c> here and there is no destination column at all — where a
/// shared table would need both nullable and a check constraint to police which
/// combination is legal.
///
/// There is no transaction type column either. This table <i>is</i> <c>SPM</c>,
/// and a column that can only ever hold one value is noise. The code is supplied
/// when the document reaches the ledger.
///
/// <b>What kind of payment this is does not live here.</b> A payment that runs
/// past what was owed is a bill payment and a supplier deposit at once, so the
/// meaning belongs to each <see cref="SpendMoneyDetail"/> rather than to the
/// document.
/// </summary>
public class SpendMoney : OrgScopedEntity
{
    public long SpendMoneyId { get; set; }

    /// <summary>
    /// Taken from the <c>SPM</c> series <b>at post, not at draft</b> — so it is
    /// null while the document is being keyed. A number taken when the form opens
    /// is a number lost every time someone changes their mind, and this series
    /// has to run without gaps.
    /// </summary>
    [MaxLength(30, ErrorMessage = "Transaction number cannot exceed 30 characters.")]
    public string? TransactionNo { get; set; }

    public DateOnly TransactionDate { get; set; }

    /// <summary>The organization's own account the money leaves from.</summary>
    [Range(1, long.MaxValue, ErrorMessage = "Choose the account the money leaves from.")]
    public long BankAccountId { get; set; }

    /// <summary>
    /// Who was paid. Required — money never leaves without a payee — and no FK,
    /// because Contacts owns that table and lives in its own service.
    /// </summary>
    [Range(1, long.MaxValue, ErrorMessage = "Choose who is being paid.")]
    public long ContactId { get; set; }

    /// <summary>
    /// The total paid. Its detail lines must add up to exactly this before the
    /// document can post — enforced by a deferred trigger, because it spans rows.
    /// </summary>
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335",
        ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Currency code is required.")]
    [MaxLength(3, ErrorMessage = "Currency code must be a 3-letter code.")]
    public string CurrencyCode { get; set; } = null!;

    /// <summary>
    /// Snapshot at <see cref="TransactionDate"/>. <b>Never looked up live</b> —
    /// and here doubly so: the difference between this rate and the settled
    /// document's is the realized exchange gain or loss, so a rate that moved on
    /// its own would invent one.
    /// </summary>
    [Range(typeof(decimal), "0.00000001", "79228162514264337593543950335",
        ErrorMessage = "Exchange rate must be greater than zero.")]
    public decimal ExchangeRate { get; set; } = 1m;

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;

    /// <summary>A cheque number, a UTR, a UPI reference — whatever the bank will be asked about.</summary>
    [MaxLength(50, ErrorMessage = "Reference cannot exceed 50 characters.")]
    public string? ReferenceNo { get; set; }

    /// <summary>Instrument date, when a cheque is written for a date other than today's.</summary>
    public DateOnly? ReferenceDate { get; set; }

    public string? Memo { get; set; }

    /// <summary>
    /// The document this payment is <i>about</i>, when it is about exactly one —
    /// a bill being paid, a debit note being refunded. `BIL` or `DBN` here, `INV`
    /// or `CRN` on the receive side.
    ///
    /// <b>The lines remain authoritative.</b> This is the header's convenience
    /// for the ordinary case: one payment, one document. A payment split across
    /// three bills leaves it null, because no single document is what the payment
    /// is about — the three <see cref="SpendMoneyDetail"/> rows are. Anything
    /// reconciling a bill must read the lines; anything listing payments can read
    /// this and avoid a join.
    ///
    /// No FK, as on the line: the documents live in Sales and Purchase.
    /// </summary>
    [MaxLength(3, ErrorMessage = "Mapped transaction type code must be a 3-letter code.")]
    public string? MappingTransactionTypeCode { get; set; }

    public long? MappingTransactionId { get; set; }

    public MoneyDocumentStatus Status { get; set; } = MoneyDocumentStatus.Draft;

    public DateTimeOffset? PostedAt { get; set; }

    /// <summary>Who posted it. A plain Guid — users live in the master database.</summary>
    public Guid? PostedBy { get; set; }

    public DateTimeOffset? VoidedAt { get; set; }

    public Guid? VoidedBy { get; set; }

    /// <summary>Why it was withdrawn. Worth more than the fact that it was.</summary>
    [MaxLength(300, ErrorMessage = "Void reason cannot exceed 300 characters.")]
    public string? VoidReason { get; set; }
}
