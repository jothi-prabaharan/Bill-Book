using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

[System.ComponentModel.DataAnnotations.Schema.Table("DeliveryChallanDetailTax", Schema = "sal")]
public class DeliveryChallanDetailTaxRead : OrgScopedEntity
{
    public long DeliveryChallanDetailTaxId { get; set; }
}

