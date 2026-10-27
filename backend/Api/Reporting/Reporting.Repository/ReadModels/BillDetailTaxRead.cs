using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>One tax component of one bill line (<c>pur.BillDetailTaxes</c>), read-only (TK-236): the rates of a line's rows add up to the rate the report shows.</summary>
[System.ComponentModel.DataAnnotations.Schema.Table("BillDetailTax", Schema = "pur")]
public class BillDetailTaxRead : OrgScopedEntity
{
    public long BillDetailTaxId { get; set; }
    public long BillDetailId { get; set; }
    public TaxComponent TaxComponent { get; set; }
    public decimal Rate { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal Amount { get; set; }
}
