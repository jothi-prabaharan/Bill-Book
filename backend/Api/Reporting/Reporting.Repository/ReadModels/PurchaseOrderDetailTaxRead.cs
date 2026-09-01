using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

public class PurchaseOrderDetailTaxRead : OrgScopedEntity
{
    public long PurchaseOrderDetailTaxId { get; set; }
}
