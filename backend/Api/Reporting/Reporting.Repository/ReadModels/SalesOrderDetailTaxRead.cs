using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

[System.ComponentModel.DataAnnotations.Schema.Table("SalesOrderDetailTax", Schema = "sal")]
public class SalesOrderDetailTaxRead : OrgScopedEntity
{
    public long SalesOrderDetailTaxId { get; set; }
}

