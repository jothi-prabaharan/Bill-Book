using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

[System.ComponentModel.DataAnnotations.Schema.Table("QuoteDetailTax", Schema = "sal")]
public class QuoteDetailTaxRead : OrgScopedEntity
{
    public long QuoteDetailTaxId { get; set; }
}

