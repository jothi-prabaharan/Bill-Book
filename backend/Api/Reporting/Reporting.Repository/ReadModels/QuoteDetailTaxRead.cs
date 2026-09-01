using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

public class QuoteDetailTaxRead : OrgScopedEntity
{
    public long QuoteDetailTaxId { get; set; }
}
