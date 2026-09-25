using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Documents;

namespace Sales.Entity.TableEntities;

/// <summary>One line of an invoice or a POS sale.</summary>
public class InvoiceDetail : DocumentLineBase
{
    public long InvoiceDetailId { get; set; }

    public long InvoiceId { get; set; }

    /// <summary>The order line this bills, when there is one. A real foreign key.</summary>
    public long? SalesOrderDetailId { get; set; }

    /// <summary>
    /// How much of this line has come back on a credit note.
    ///
    /// It is what stops a customer being credited twice for the same goods: a
    /// credit note line names the invoice line it reverses, and the running total
    /// here is what the guard checks against.
    /// </summary>
    public decimal ReturnedQuantity { get; set; }
    
    /// <summary>The inventory movement generated when this line was issued.</summary>
    public long? StockMovementId { get; set; }
    
    public decimal UnitCost { get; set; }
    
    /// <summary>
    /// The GST unit (UQC) the line reports in, copied from the unit's
    /// <c>inv.UnitsOfMeasure.UqcCode</c> when the document posts (TK-91). Sales
    /// cannot read Inventory's tables, and the IRP, GSTR-1's HSN summary and the
    /// e-way bill all need it per line. <c>OTH</c> for a line with no unit.
    /// </summary>
    [MaxLength(10, ErrorMessage = "UQC cannot exceed 10 characters.")]
    public string? UqcCode { get; set; }

    public List<InvoiceDetailTax> Taxes { get; set; } = [];
}
