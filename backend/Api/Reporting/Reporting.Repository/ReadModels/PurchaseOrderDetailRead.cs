using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>One line of a purchase order (<c>pur.PurchaseOrderDetails</c>), read-only (TK-240): what was ordered and how much of it has been received.</summary>
[System.ComponentModel.DataAnnotations.Schema.Table("PurchaseOrderDetail", Schema = "pur")]
public class PurchaseOrderDetailRead : OrgScopedEntity
{
    public long PurchaseOrderDetailId { get; set; }
    public long PurchaseOrderId { get; set; }

    /// <summary>How much has arrived across every posted goods receipt against the line, in the line's own unit; only the accepted quantity counts.</summary>
    public decimal ReceivedQuantity { get; set; }

    public int LineNumber { get; set; }
    public long? ItemId { get; set; }
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public long? UomId { get; set; }
    public decimal TaxableAmount { get; set; }
}
