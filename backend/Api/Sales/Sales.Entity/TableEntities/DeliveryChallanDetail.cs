using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Documents;

namespace Sales.Entity.TableEntities;

/// <summary>One line of a delivery challan.</summary>
public class DeliveryChallanDetail : DocumentLineBase
{
    public long DeliveryChallanDetailId { get; set; }

    public long DeliveryChallanId { get; set; }

    /// <summary>The order line being delivered against, when there is one. A real foreign key.</summary>
    public long? SalesOrderDetailId { get; set; }

    /// <summary>
    /// How much of what was delivered has since been invoiced.
    ///
    /// Goods can go out on one challan and be billed across two invoices, or
    /// several challans billed on one — so the figure lives here rather than
    /// being inferred from a one-to-one link that does not exist.
    /// </summary>
    public decimal InvoicedQuantity { get; set; }
    
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

    public List<DeliveryChallanDetailTax> Taxes { get; set; } = [];
}

