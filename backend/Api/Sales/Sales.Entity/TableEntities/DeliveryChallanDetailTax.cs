using Shared.Kernel.Documents;

namespace Sales.Entity.TableEntities;

/// <summary>One tax component on one delivery challan line.</summary>
public class DeliveryChallanDetailTax : DocumentLineTaxBase
{
    public long DeliveryChallanDetailTaxId { get; set; }

    public long DeliveryChallanDetailId { get; set; }
}
