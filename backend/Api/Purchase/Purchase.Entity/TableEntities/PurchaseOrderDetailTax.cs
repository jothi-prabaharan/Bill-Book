using Shared.Kernel.Documents;

namespace Purchase.Entity.TableEntities;

/// <summary>One tax component on one purchase order line.</summary>
public class PurchaseOrderDetailTax : DocumentLineTaxBase
{
    public long PurchaseOrderDetailTaxId { get; set; }

    public long PurchaseOrderDetailId { get; set; }
}
