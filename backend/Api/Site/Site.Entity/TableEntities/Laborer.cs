using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Site.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Site.Entity.TableEntities;

/// <summary>
/// Laborer / technician profiles (light profile per D-62, with optional HRMS employee link) (TK-170).
/// </summary>
[Table("Laborers", Schema = "sit")]
public class Laborer : OrgScopedEntity
{
    [Key]
    public long LaborerId { get; set; }

    [Required(ErrorMessage = "Trade is required.")]
    public long TradeId { get; set; }

    /// <summary>Optional user ID for mobile portal login.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Optional employee link if HRMS is active.</summary>
    public long? EmployeeId { get; set; }

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(150, ErrorMessage = "Full name cannot exceed 150 characters.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone is required.")]
    [Phone(ErrorMessage = "Phone must be a valid phone number.")]
    [StringLength(20, ErrorMessage = "Phone cannot exceed 20 characters.")]
    public string Phone { get; set; } = string.Empty;

    [Range(0, 1000000, ErrorMessage = "Cost rate per day must be between 0 and 1,000,000.")]
    public decimal? CostRatePerDay { get; set; }

    [Range(0, 1000000, ErrorMessage = "Sell rate per day must be between 0 and 1,000,000.")]
    public decimal? SellRatePerDay { get; set; }

    [Range(0, 1000000, ErrorMessage = "OT cost rate per hour must be between 0 and 1,000,000.")]
    public decimal? OtCostRatePerHour { get; set; }

    [Range(0, 1000000, ErrorMessage = "OT sell rate per hour must be between 0 and 1,000,000.")]
    public decimal? OtSellRatePerHour { get; set; }

    public bool IsActive { get; set; } = true;
}
