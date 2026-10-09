using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Shared.Kernel.Tenancy;

namespace Site.Entity.TableEntities;

/// <summary>
/// Master trade catalog (e.g., Electrical, Plumbing, HVAC, Carpentry) (TK-170).
/// </summary>
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

    /// <summary>Reference to inv.Items for SAC billing invoice line.</summary>
    public long? DefaultServiceItemId { get; set; }

    [Range(0, 1000000, ErrorMessage = "Default cost rate per day must be between 0 and 1,000,000.")]
    public decimal DefaultCostRatePerDay { get; set; }

    [Range(0, 1000000, ErrorMessage = "Default sell rate per day must be between 0 and 1,000,000.")]
    public decimal DefaultSellRatePerDay { get; set; }

    [Range(0, 1000000, ErrorMessage = "Default OT cost rate per hour must be between 0 and 1,000,000.")]
    public decimal DefaultOtCostRatePerHour { get; set; }

    [Range(0, 1000000, ErrorMessage = "Default OT sell rate per hour must be between 0 and 1,000,000.")]
    public decimal DefaultOtSellRatePerHour { get; set; }

    public bool IsActive { get; set; } = true;
}
