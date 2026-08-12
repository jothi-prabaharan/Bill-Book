using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>
/// One meaning within a receipt, and what it settles. The mirror of
/// <see cref="SpendMoneyDetail"/>.
///
/// A receipt that runs past what was invoiced is an invoice payment <b>and</b> a
/// customer overpayment at once — the settled part clears the invoice, the
/// excess becomes a balance held for that customer. One line each, one bank
/// movement, and a receivables report reading invoice payments still sees the
/// part that was one.
/// </summary>
public class ReceiveMoneyDetail : OrgScopedEntity
{
    public long ReceiveMoneyDetailId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "A line must belong to a receipt.")]
    public long ReceiveMoneyId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Line number starts at one.")]
    public int LineNumber { get; set; }

    /// <summary>
    /// What this part of the money was, from <c>mst.LedgerSources</c> — an
    /// invoice payment, a customer advance, an overpayment, a supplier refund.
    /// </summary>
    [Range(1, 19, ErrorMessage = "Ledger source must be one of the nineteen sources.")]
    public int LedgerSourceId { get; set; }

    /// <summary>
    /// The document this line settles — <c>INV</c>, <c>DBN</c>. Null when there
    /// is nothing to settle, which is what an advance or an unallocated
    /// overpayment is.
    /// </summary>
    [MaxLength(3, ErrorMessage = "Mapped transaction type code must be a 3-letter code.")]
    public string? MappingTransactionTypeCode { get; set; }

    public long? MappingTransactionId { get; set; }

    /// <summary>Always positive — direction is the document's, not the line's.</summary>
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335",
        ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    /// <summary>Base currency, at the header's rate.</summary>
    public decimal AmountBase { get; set; }

    [MaxLength(300, ErrorMessage = "Line memo cannot exceed 300 characters.")]
    public string? LineMemo { get; set; }
}
