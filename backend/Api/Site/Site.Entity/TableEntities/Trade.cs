using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Shared.Kernel.Tenancy;

namespace Site.Entity.TableEntities;

[Table("Trades", Schema = "sit")]
public class Trade : OrgScopedEntity
{
    [Key]
    public long TradeId { get; set; }

    [Required(ErrorMessage = "Trade code is required.")]
    [StringLength(30, ErrorMessage = "Trade code cannot exceed 30 characters.")]
    public string TradeCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Trade name is required.")]
    [StringLength(100, ErrorMessage = "Trade name cannot exceed 100 characters.")]
    public string TradeName { get; set; } = string.Empty;

    [Range(0, 1000000, ErrorMessage = "Standard hourly rate must be between 0 and 1,000,000.")]
    public decimal StandardHourlyRate { get; set; }

    [Range(0, 1000000, ErrorMessage = "Standard overtime hourly rate must be between 0 and 1,000,000.")]
    public decimal StandardOvertimeHourlyRate { get; set; }

    public bool IsActive { get; set; } = true;
}
