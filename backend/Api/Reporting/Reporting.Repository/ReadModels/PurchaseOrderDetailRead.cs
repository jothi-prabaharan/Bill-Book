using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

public class PurchaseOrderDetailRead : OrgScopedEntity
{
    public long PurchaseOrderDetailId { get; set; }
}
