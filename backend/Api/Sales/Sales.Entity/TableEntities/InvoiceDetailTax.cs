using Shared.Kernel.Documents;

namespace Sales.Entity.TableEntities;

/// <summary>
/// One tax component on one invoice line. The rows GSTR-1 is built from, and
/// what the <c>TAX</c> ledger legs post against.
/// </summary>
public class InvoiceDetailTax : DocumentLineTaxBase
{
    public long InvoiceDetailTaxId { get; set; }

    public long InvoiceDetailId { get; set; }
}
