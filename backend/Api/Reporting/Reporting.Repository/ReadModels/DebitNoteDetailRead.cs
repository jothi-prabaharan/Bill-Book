using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// <c>pur.DebitNoteDetails</c>, read-only (TK-239): the lines of a debit note, which the Purchase
/// Analysis takes off what was bought. Amounts are in the debit note's own currency and positive; a
/// debit note's lines reduce purchases, and the report signs them.
/// </summary>
[System.ComponentModel.DataAnnotations.Schema.Table("DebitNoteDetail", Schema = "pur")]
public class DebitNoteDetailRead : OrgScopedEntity
{
    public long DebitNoteDetailId { get; set; }

    public long DebitNoteId { get; set; }

    /// <summary>The bill line this one gives back or corrects.</summary>
    public long BillDetailId { get; set; }

    public int LineNumber { get; set; }

    public long? ItemId { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>The quantity in the item's base unit.</summary>
    public decimal BaseQuantity { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal GrossAmount { get; set; }

    public decimal TaxableAmount { get; set; }

    /// <summary>Stock, expense or capital, as on the bill line.</summary>
    public Shared.Kernel.Documents.DocumentLineType LineType { get; set; }
}
