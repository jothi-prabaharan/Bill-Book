using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Banking.Entity.TableEntities;

/// <summary>
/// One meaning within a money document, and what it settles.
///
/// <b>This is where a payment says what kind of payment it is.</b> The header
/// says money moved and how much; each line says what that part of it was —
/// through <see cref="LedgerSourceId"/> — and which document it clears, through
/// <see cref="MappingTransactionTypeCode"/> and
/// <see cref="MappingTransactionId"/>.
///
/// The reason it is a line rather than a column on the header: a payment that
/// runs past what was owed is <b>two things at once</b>. Paying ₹11,000 against a
/// ₹10,000 bill is a bill payment of ₹10,000 allocated to that bill, and a
/// supplier deposit of ₹1,000 allocated to nothing. Record it as one meaning and
/// a payables report asking for bill payments quietly misses the ₹10,000 that
/// genuinely was one.
///
/// The same mechanism covers a payment split across several bills: one line per
/// bill, each with its own amount, all under one document and one bank movement.
/// </summary>
public class MoneyTransactionDetail : OrgScopedEntity
{
    public long MoneyTransactionDetailId { get; set; }

    public long MoneyTransactionId { get; set; }

    /// <summary>Position on the document. Unique within it, and what the screen orders by.</summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// What this part of the money was, from <c>mst.LedgerSources</c> — a bill
    /// payment, a deposit, an overpayment, a refund. No FK; the master is in
    /// another database.
    ///
    /// It travels to the ledger row this line produces, which is what lets a
    /// report tell a supplier deposit from a bill payment made on the same day
    /// out of the same account.
    /// </summary>
    public int LedgerSourceId { get; set; }

    /// <summary>
    /// The document this line settles — <c>BIL</c>, <c>INV</c>, <c>CRN</c>,
    /// <c>DBN</c>. Null when there is nothing to settle, which is exactly what an
    /// advance or an unallocated overpayment is.
    ///
    /// Together with <see cref="MappingTransactionId"/> this pair is the entire
    /// mechanism for tracing a payment to what it cleared. No FK — the documents
    /// live in Sales and Purchase.
    /// </summary>
    [MaxLength(3, ErrorMessage = "Mapped transaction type code must be a 3-letter code.")]
    public string? MappingTransactionTypeCode { get; set; }

    public long? MappingTransactionId { get; set; }

    /// <summary>
    /// How much of the document's total this line accounts for. Always positive:
    /// direction is the document's, not the line's, so a refund is a different
    /// source rather than a negative amount.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>Base currency, at the header's rate. What the ledger sums.</summary>
    public decimal AmountBase { get; set; }

    [MaxLength(300, ErrorMessage = "Line memo cannot exceed 300 characters.")]
    public string? LineMemo { get; set; }
}
