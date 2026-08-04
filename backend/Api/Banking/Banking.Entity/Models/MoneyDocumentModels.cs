using System.ComponentModel.DataAnnotations;
using Banking.Entity.Enums;

namespace Banking.Entity.Models;

/// <summary>
/// A payment or a receipt as the screen sends it. Header plus every allocation
/// line, in one request — the lines have to add up to the header before it can
/// post, so saving them apart would let the two drift between calls.
/// </summary>
public class SaveMoneyDocumentRequest
{
    public DateOnly TransactionDate { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose a bank or cash account.")]
    public long BankAccountId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose the contact.")]
    public long ContactId { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335",
        ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    /// <summary>Null means the branch's own currency, which is the ordinary case.</summary>
    [MaxLength(3, ErrorMessage = "Currency code must be a 3-letter code.")]
    public string? CurrencyCode { get; set; }

    /// <summary>Snapshot at the transaction date. Never looked up live.</summary>
    [Range(typeof(decimal), "0.00000001", "79228162514264337593543950335",
        ErrorMessage = "Exchange rate must be greater than zero.")]
    public decimal? ExchangeRate { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;

    [MaxLength(50, ErrorMessage = "Reference cannot exceed 50 characters.")]
    public string? ReferenceNo { get; set; }

    public DateOnly? ReferenceDate { get; set; }

    public string? Memo { get; set; }

    /// <summary>
    /// The document this is about, when it is about exactly one. Left null when
    /// the payment is split across several — the lines say which.
    /// </summary>
    [MaxLength(3, ErrorMessage = "Mapped transaction type code must be a 3-letter code.")]
    public string? MappingTransactionTypeCode { get; set; }

    public long? MappingTransactionId { get; set; }

    public List<SaveMoneyLineRequest> Lines { get; set; } = [];
}

/// <summary>One meaning within a money document, and what it settles.</summary>
public class SaveMoneyLineRequest
{
    /// <summary>
    /// What this part of the money is — a bill payment, a deposit, an
    /// overpayment, a refund. It decides which account the line posts to.
    /// </summary>
    [Range(1, 19, ErrorMessage = "Choose what this line is for.")]
    public int LedgerSourceId { get; set; }

    /// <summary>The document settled, or null when the line settles nothing.</summary>
    [MaxLength(3, ErrorMessage = "Mapped transaction type code must be a 3-letter code.")]
    public string? MappingTransactionTypeCode { get; set; }

    public long? MappingTransactionId { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335",
        ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    [MaxLength(300, ErrorMessage = "Line memo cannot exceed 300 characters.")]
    public string? LineMemo { get; set; }
}

/// <summary>A transfer between the organization's own accounts. No contact, no lines.</summary>
public class SaveTransferRequest
{
    public DateOnly TransactionDate { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose the account the money leaves.")]
    public long FromBankAccountId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose the account the money arrives in.")]
    public long ToBankAccountId { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335",
        ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code must be a 3-letter code.")]
    public string? CurrencyCode { get; set; }

    [Range(typeof(decimal), "0.00000001", "79228162514264337593543950335",
        ErrorMessage = "Exchange rate must be greater than zero.")]
    public decimal? ExchangeRate { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;

    [MaxLength(50, ErrorMessage = "Reference cannot exceed 50 characters.")]
    public string? ReferenceNo { get; set; }

    public DateOnly? ReferenceDate { get; set; }

    public string? Memo { get; set; }
}

/// <summary>Why a money document was refused. Every value is something a user can act on.</summary>
public enum MoneyDocumentOutcome
{
    Ok = 0,
    NotFound = 1,

    /// <summary>An edit, a delete or a post aimed at a document that is no longer a draft.</summary>
    NotDraft = 2,

    /// <summary>Nothing to post: the document has no lines.</summary>
    NoLines = 3,

    /// <summary>The lines do not add up to the header amount.</summary>
    NotAllocated = 4,

    /// <summary>A line names a purpose money cannot move under on this document.</summary>
    UnknownLedgerSource = 5,

    /// <summary>The bank account does not exist in this branch, or has no ledger account behind it.</summary>
    BankAccountUnusable = 6,

    /// <summary>A transfer to the account the money came from.</summary>
    SameAccount = 7,

    /// <summary>The books are closed to this caller for that date.</summary>
    PeriodClosed = 8,

    /// <summary>Whether the books are closed could not be established. Transient.</summary>
    PeriodLockUnavailable = 9,

    /// <summary>The ledger refused the posting, or could not be reached.</summary>
    PostingRefused = 10,

    /// <summary>Only a posted document can be voided.</summary>
    NotPosted = 11,

    /// <summary>
    /// What the settled document was booked at could not be established. Transient,
    /// for the same reason an unreadable period lock is: a rate that could not be
    /// read is not a rate that matched.
    /// </summary>
    SettlementRateUnavailable = 12,

    /// <summary>
    /// A line settles a document raised in a different currency from the payment.
    /// Refused rather than converted — two conversions in one settlement is a
    /// cross-rate, and guessing one would put a figure in the books that no rate
    /// on record produces.
    /// </summary>
    SettlementCurrencyMismatch = 13,
}

public sealed record MoneyDocumentResult(
    MoneyDocumentOutcome Outcome, long DocumentId = 0, string? Detail = null);

/// <summary>A money document on the list screen.</summary>
public class MoneyDocumentListItem
{
    public long DocumentId { get; set; }

    /// <summary>Null while the document is a draft — a number is taken at post.</summary>
    public string? TransactionNo { get; set; }

    public DateOnly TransactionDate { get; set; }

    public long BankAccountId { get; set; }

    public string BankAccountName { get; set; } = null!;

    /// <summary>Null on a transfer, which has no counterparty.</summary>
    public long? ContactId { get; set; }

    /// <summary>Set on a transfer only.</summary>
    public long? ToBankAccountId { get; set; }

    public string? ToBankAccountName { get; set; }

    public decimal Amount { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public decimal ExchangeRate { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public string? ReferenceNo { get; set; }

    public string? Memo { get; set; }

    public string Status { get; set; } = null!;

    public DateTimeOffset? PostedAt { get; set; }

    public string? MappingTransactionTypeCode { get; set; }

    public long? MappingTransactionId { get; set; }
}

/// <summary>A money document with its allocation lines.</summary>
public class MoneyDocumentView : MoneyDocumentListItem
{
    public List<MoneyLineView> Lines { get; set; } = [];
}

public class MoneyLineView
{
    public long DetailId { get; set; }

    public int LineNumber { get; set; }

    public int LedgerSourceId { get; set; }

    public string? MappingTransactionTypeCode { get; set; }

    public long? MappingTransactionId { get; set; }

    public decimal Amount { get; set; }

    public decimal AmountBase { get; set; }

    public string? LineMemo { get; set; }
}

/// <summary>Why a posted document was withdrawn.</summary>
public class VoidMoneyDocumentRequest
{
    [MaxLength(300, ErrorMessage = "Reason cannot exceed 300 characters.")]
    public string? Reason { get; set; }
}
