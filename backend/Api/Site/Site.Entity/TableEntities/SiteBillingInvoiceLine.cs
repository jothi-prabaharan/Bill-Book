using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Site.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Site.Entity.TableEntities;

[Table("SiteBillingInvoiceLines", Schema = "sit")]
public class SiteBillingInvoiceLine : OrgScopedEntity
{
    [Key]
    public long SiteBillingInvoiceLineId { get; set; }

    [Required(ErrorMessage = "Site billing invoice is required.")]
    public long SiteBillingInvoiceId { get; set; }

    [Required(ErrorMessage = "Line type is required.")]
    public BillingLineType LineType { get; set; } = BillingLineType.Labor;

    public long? ReferenceId { get; set; }

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string Description { get; set; } = string.Empty;

    [Range(0, 10000000, ErrorMessage = "Quantity cannot be negative.")]
    public decimal Quantity { get; set; }

    [Range(0, 10000000, ErrorMessage = "Unit price cannot be negative.")]
    public decimal UnitPrice { get; set; }

    [Range(0, 1000000000, ErrorMessage = "Total amount cannot be negative.")]
    public decimal TotalAmount { get; set; }
}
