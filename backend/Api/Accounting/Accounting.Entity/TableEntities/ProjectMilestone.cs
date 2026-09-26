using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>A point a fixed-fee project is invoiced at (TK-104).</summary>
public class ProjectMilestone : OrgScopedEntity
{
    public long ProjectMilestoneId { get; set; }

    public long ProjectId { get; set; }

    [Required(ErrorMessage = "Milestone name is required.")]
    [MaxLength(150, ErrorMessage = "Milestone name cannot exceed 150 characters.")]
    public string Name { get; set; } = null!;

    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "The amount cannot be negative.")]
    public decimal Amount { get; set; }

    public DateOnly? DueDate { get; set; }

    /// <summary>The invoice that billed it, once billed. No FK: invoices live in Sales.</summary>
    public long? InvoiceId { get; set; }
}
