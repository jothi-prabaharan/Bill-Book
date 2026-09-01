using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

public class BillDetailTaxRead : OrgScopedEntity
{
    public long BillDetailTaxId { get; set; }
}
