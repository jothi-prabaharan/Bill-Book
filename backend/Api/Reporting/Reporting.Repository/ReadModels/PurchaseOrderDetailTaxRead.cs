using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

[System.ComponentModel.DataAnnotations.Schema.Table("PurchaseOrderDetailTax", Schema = "pur")]
public class PurchaseOrderDetailTaxRead : OrgScopedEntity
{
    public long PurchaseOrderDetailTaxId { get; set; }
}

