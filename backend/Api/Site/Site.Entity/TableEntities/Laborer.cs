using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Site.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Site.Entity.TableEntities;

[Table("Laborers", Schema = "sit")]
public class Laborer : OrgScopedEntity
{
    [Key]
    public long LaborerId { get; set; }

    public Guid? UserId { get; set; }

    public long? ContactId { get; set; }

    [Required(ErrorMessage = "Laborer type is required.")]
    public LaborerType LaborerType { get; set; } = LaborerType.InternalEmployee;

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(150, ErrorMessage = "Full name cannot exceed 150 characters.")]
    public string FullName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Phone must be a valid phone number.")]
    [StringLength(20, ErrorMessage = "Phone cannot exceed 20 characters.")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Trade is required.")]
    public long TradeId { get; set; }

    [Range(0, 1000000, ErrorMessage = "Cost hourly rate must be between 0 and 1,000,000.")]
    public decimal CostHourlyRate { get; set; }

    [Range(0, 1000000, ErrorMessage = "Billing hourly rate must be between 0 and 1,000,000.")]
    public decimal BillingHourlyRate { get; set; }

    public bool IsActive { get; set; } = true;
}
