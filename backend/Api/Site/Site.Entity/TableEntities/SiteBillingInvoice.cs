using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Shared.Kernel.Tenancy;

namespace Site.Entity.TableEntities;

[Table("SiteBillingInvoices", Schema = "sit")]
public class SiteBillingInvoice : OrgScopedEntity
{
    [Key]
    public long SiteBillingInvoiceId { get; set; }

    [Required(ErrorMessage = "Billing number is required.")]
    [StringLength(30, ErrorMessage = "Billing number cannot exceed 30 characters.")]
    public string BillingNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Site is required.")]
    public long SiteId { get; set; }

    public DateOnly BillingDate { get; set; }

    public long? SalesInvoiceId { get; set; }

    [Range(0, 1000000000, ErrorMessage = "Total labor amount cannot be negative.")]
    public decimal TotalLaborAmount { get; set; }

    [Range(0, 1000000000, ErrorMessage = "Total material amount cannot be negative.")]
    public decimal TotalMaterialAmount { get; set; }

    [Range(0, 1000000000, ErrorMessage = "Total billable amount cannot be negative.")]
    public decimal TotalBillableAmount { get; set; }

    public bool IsFinalBilling { get; set; }
}
