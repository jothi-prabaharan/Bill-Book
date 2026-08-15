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
    
    public List<DeliveryChallanDetailTax> Taxes { get; set; } = [];
}

