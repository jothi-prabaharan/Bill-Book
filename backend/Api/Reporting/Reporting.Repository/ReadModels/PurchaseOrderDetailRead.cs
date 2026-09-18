using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

[System.ComponentModel.DataAnnotations.Schema.Table("PurchaseOrderDetail", Schema = "pur")]
public class PurchaseOrderDetailRead : OrgScopedEntity
{
    public long PurchaseOrderDetailId { get; set; }
}

