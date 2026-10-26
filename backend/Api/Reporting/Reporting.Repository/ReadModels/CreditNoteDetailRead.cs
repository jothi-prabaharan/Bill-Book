using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// <c>sal.CreditNoteDetails</c>, read-only (TK-228): the lines of a credit note, which the Sales
/// Analysis takes off what was sold. Amounts are in the credit note's own currency and positive; a
/// credit note's lines reduce sales, and the report signs them.
/// </summary>
[System.ComponentModel.DataAnnotations.Schema.Table("CreditNoteDetail", Schema = "sal")]
public class CreditNoteDetailRead : OrgScopedEntity
{
    public long CreditNoteDetailId { get; set; }

    public long CreditNoteId { get; set; }

    /// <summary>The invoice line this one gives back.</summary>
    public long InvoiceDetailId { get; set; }

    public int LineNumber { get; set; }

    public long? ItemId { get; set; }

    public long? WarehouseId { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>The quantity in the item's base unit.</summary>
    public decimal BaseQuantity { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal GrossAmount { get; set; }

    public decimal TaxableAmount { get; set; }
}
