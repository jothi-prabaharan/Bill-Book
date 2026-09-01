using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

public class DeliveryChallanDetailTaxRead : OrgScopedEntity
{
    public long DeliveryChallanDetailTaxId { get; set; }
}
