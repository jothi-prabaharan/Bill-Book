using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

[System.ComponentModel.DataAnnotations.Schema.Table("BillDetailTax", Schema = "pur")]
public class BillDetailTaxRead : OrgScopedEntity
{
    public long BillDetailTaxId { get; set; }
}

