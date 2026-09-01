using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

public class SalesOrderDetailTaxRead : OrgScopedEntity
{
    public long SalesOrderDetailTaxId { get; set; }
}
