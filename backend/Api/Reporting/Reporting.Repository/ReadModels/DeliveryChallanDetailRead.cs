using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// <c>sal.DeliveryChallanDetails</c>, read-only (TK-232): the lines of a delivery challan, with how much of
/// each has been billed. Quantity and the line's amounts are in the challan's own unit and currency.
/// </summary>
[System.ComponentModel.DataAnnotations.Schema.Table("DeliveryChallanDetail", Schema = "sal")]
public class DeliveryChallanDetailRead : OrgScopedEntity
{
    public long DeliveryChallanDetailId { get; set; }

    public long DeliveryChallanId { get; set; }

    /// <summary>
    /// How much of the line an invoice raised from the challan has billed, in the line's own unit. An
    /// invoice that bills the order's lines and does not name the challan does not advance it.
    /// </summary>
    public decimal InvoicedQuantity { get; set; }

    public int LineNumber { get; set; }

    public long? ItemId { get; set; }

    public string? Description { get; set; }

    /// <summary>What was delivered, in the line's own unit.</summary>
    public decimal Quantity { get; set; }

    public long? UomId { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal TaxableAmount { get; set; }
}
