using Shared.Kernel.Tenancy;
using Shared.Kernel.Documents;

namespace Reporting.Repository.ReadModels;

public class InvoiceDetailTaxRead : OrgScopedEntity
{
    public long InvoiceDetailTaxId { get; set; }
    public long InvoiceDetailId { get; set; }
    public TaxComponent TaxComponent { get; set; }
    public decimal Rate { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal Amount { get; set; }
}
