using Shared.Kernel.Documents;

namespace Sales.Entity.TableEntities;

/// <summary>One tax component on one sales order line.</summary>
public class SalesOrderDetailTax : DocumentLineTaxBase
{
    public long SalesOrderDetailTaxId { get; set; }

    public long SalesOrderDetailId { get; set; }
}
