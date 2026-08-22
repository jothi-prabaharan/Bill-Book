using Shared.Kernel.Documents;

namespace Purchase.Entity.TableEntities;

/// <summary>One tax component on one goods receipt line.</summary>
public class GoodsReceiptDetailTax : DocumentLineTaxBase
{
    public long GoodsReceiptDetailTaxId { get; set; }

    public long GoodsReceiptDetailId { get; set; }
}
