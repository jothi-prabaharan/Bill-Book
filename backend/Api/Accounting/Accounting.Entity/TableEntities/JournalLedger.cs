using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>
/// <b>The single posting target.</b> Every financial document in the system —
/// invoice, bill, payment, refund, journal, opening balance, depreciation, stock
/// movement — writes its double-entry legs here and nowhere else. This is what
/// reports read.
///
/// One row is one leg: a debit <b>xor</b> a credit, never both, never negative.
/// A signed amount would let a reversal be written as a negative debit, and then
/// no column could be summed without knowing which convention its writer used.
///
/// Rows are grouped by the document that produced them —
/// (<see cref="TransactionTypeCode"/>, <see cref="TransactionId"/>) — and a
/// deferred constraint trigger checks that each such group balances in base
/// currency. Deferred so a multi-row insert is judged on the set rather than on
/// whichever row went in first.
///
/// <b>Nothing outside Accounting inserts here.</b> Other services describe a
/// posting and ask; the mapping from a document to its legs is this service's
/// business, and a second writer is how two services come to disagree about what
/// an account means.
/// </summary>
public class JournalLedger : OrgScopedEntity
{
    public long LedgerId { get; set; }

    /// <summary>Posting date. What every report ages and periods on.</summary>
    public DateOnly LedgerDate { get; set; }

    /// <summary>The GL account being hit.</summary>
    public long AccountId { get; set; }

    /// <summary>
    /// The sub-dimension beneath the control account: which contact, which item,
    /// which tax rate. Null for legs that have none — bank and equity.
    /// </summary>
    public long? SubAccountId { get; set; }

    /// <summary>
    /// The kind of document, from <c>mst.TransactionTypes</c>. Stored as its code
    /// rather than an id so a ledger row reads without a join, and with no FK
    /// because the master lives in another database.
    /// </summary>
    [Required(ErrorMessage = "Transaction type code is required.")]
    [MaxLength(3, ErrorMessage = "Transaction type code must be a 3-letter code.")]
    public string TransactionTypeCode { get; set; } = null!;

    /// <summary>The source document header.</summary>
    public long TransactionId { get; set; }

    /// <summary>
    /// The source document line, or 0 when the leg is not line-level. Together
    /// with the type, the id and <see cref="LedgerTypeId"/> this identifies the
    /// set of rows one writer owns, which is what makes a posting replaceable
    /// without disturbing anyone else's legs on the same document.
    /// </summary>
    public long TransactionDetailId { get; set; }

    /// <summary>Transaction currency.</summary>
    public decimal DebitAmount { get; set; }

    /// <summary>Transaction currency.</summary>
    public decimal CreditAmount { get; set; }

    /// <summary>Base currency. What reports sum, and what the balance trigger checks.</summary>
    public decimal DebitAmountBase { get; set; }

    /// <summary>Base currency.</summary>
    public decimal CreditAmountBase { get; set; }

    [Required(ErrorMessage = "Currency code is required.")]
    [MaxLength(3, ErrorMessage = "Currency code must be a 3-letter code.")]
    public string CurrencyCode { get; set; } = null!;

    /// <summary>
    /// Snapshot at <see cref="LedgerDate"/>. <b>Never looked up live</b> — a
    /// historical document would silently reprice.
    /// </summary>
    public decimal ExchangeRate { get; set; } = 1m;

    /// <summary>Tax can settle at a different rate from the document it sits on.</summary>
    public decimal? TaxExchangeRate { get; set; }

    /// <summary>Customer or vendor. No FK — Contacts owns it.</summary>
    public long? ContactId { get; set; }

    /// <summary>Which leg this is, from <c>mst.LedgerTypes</c>. No FK — master database.</summary>
    public int LedgerTypeId { get; set; }

    /// <summary>What produced it, from <c>mst.LedgerSources</c>. No FK — master database.</summary>
    public int LedgerSourceId { get; set; }

    /// <summary>Provenance, when the posting came from a row in another service.</summary>
    public long? SourceDocumentId { get; set; }

    [MaxLength(500, ErrorMessage = "Transaction description cannot exceed 500 characters.")]
    public string? TransactionDesc { get; set; }

    /// <summary>
    /// Links a payment back to the document it settles. This pair is the whole
    /// mechanism for tracing a receipt to its invoice.
    /// </summary>
    public long? MappingTransactionId { get; set; }

    [MaxLength(3, ErrorMessage = "Mapped transaction type code must be a 3-letter code.")]
    public string? MappingTransactionTypeCode { get; set; }

    /// <summary>Set when the posting came from a manual journal.</summary>
    public long? JournalId { get; set; }

    /// <summary>Whether this ledger line has been reconciled against a bank feed.</summary>
    public bool IsReconciled { get; set; }

    /// <summary>The bank statement line this ledger row was reconciled against.</summary>
    public long? BankStatementLineId { get; set; }
}
