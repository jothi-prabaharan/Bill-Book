using System.ComponentModel.DataAnnotations;
using Banking.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Banking.Entity.TableEntities;

/// <summary>
/// Money moving between the organization's own accounts — <c>TRM</c>. Bank to
/// bank, or bank to the cash drawer.
///
/// <b>The one money document with no counterparty.</b> Both sides are the
/// organization's, so there is no contact column here at all — which is the
/// clearest argument for these being three tables rather than one. A shared
/// table needs a nullable contact, a nullable destination, and a check
/// constraint policing which combination is legal for which type code; here the
/// shape simply <i>is</i> the shape.
///
/// <b>And no detail table.</b> A transfer allocates to nothing: it settles no
/// invoice, clears no bill, and holds no advance. A line table would carry one
/// row per document saying only what the header already said. If a transfer ever
/// needs to split — a bank charge taken out of the amount moved is the realistic
/// case — that is a detail table added then, which is cheaper than carrying an
/// empty one until it happens.
/// </summary>
public class TransferMoney : OrgScopedEntity
{
    public long TransferMoneyId { get; set; }

    /// <summary>
    /// Taken from the <c>TRM</c> series <b>at post, not at draft</b>, so it is
    /// null while the document is being keyed.
    /// </summary>
    [MaxLength(30, ErrorMessage = "Transaction number cannot exceed 30 characters.")]
    public string? TransactionNo { get; set; }

    public DateOnly TransactionDate { get; set; }

    /// <summary>The account the money leaves.</summary>
    [Range(1, long.MaxValue, ErrorMessage = "Choose the account the money leaves.")]
    public long FromBankAccountId { get; set; }

    /// <summary>
    /// The account it arrives in. Never the same as
    /// <see cref="FromBankAccountId"/> — that posts two legs that cancel, and
    /// reconciles as a transaction that never happened.
    /// </summary>
    [Range(1, long.MaxValue, ErrorMessage = "Choose the account the money arrives in.")]
    public long ToBankAccountId { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335",
        ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Currency code is required.")]
    [MaxLength(3, ErrorMessage = "Currency code must be a 3-letter code.")]
    public string CurrencyCode { get; set; } = null!;

    /// <summary>Snapshot at <see cref="TransactionDate"/>. Never looked up live.</summary>
    [Range(typeof(decimal), "0.00000001", "79228162514264337593543950335",
        ErrorMessage = "Exchange rate must be greater than zero.")]
    public decimal ExchangeRate { get; set; } = 1m;

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;

    [MaxLength(50, ErrorMessage = "Reference cannot exceed 50 characters.")]
    public string? ReferenceNo { get; set; }

    public DateOnly? ReferenceDate { get; set; }

    public string? Memo { get; set; }

    public MoneyDocumentStatus Status { get; set; } = MoneyDocumentStatus.Draft;

    public DateTimeOffset? PostedAt { get; set; }

    public Guid? PostedBy { get; set; }

    public DateTimeOffset? VoidedAt { get; set; }

    public Guid? VoidedBy { get; set; }

    [MaxLength(300, ErrorMessage = "Void reason cannot exceed 300 characters.")]
    public string? VoidReason { get; set; }
}
