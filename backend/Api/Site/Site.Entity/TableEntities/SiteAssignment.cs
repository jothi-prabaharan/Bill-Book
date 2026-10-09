using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Site.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Site.Entity.TableEntities;

/// <summary>
/// Assignment of a laborer to a site over a date range (TK-170).
/// </summary>
[Table("SiteAssignments", Schema = "sit")]
public class SiteAssignment : OrgScopedEntity
{
    [Key]
    public long SiteAssignmentId { get; set; }

    [Required(ErrorMessage = "Site is required.")]
    public long SiteId { get; set; }

    [Required(ErrorMessage = "Laborer is required.")]
    public long LaborerId { get; set; }

    [Required(ErrorMessage = "Trade is required.")]
    public long TradeId { get; set; }

    [Required(ErrorMessage = "Start date is required.")]
    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    [Range(0, 1000000, ErrorMessage = "Cost rate per day must be between 0 and 1,000,000.")]
    public decimal? CostRatePerDay { get; set; }

    [Range(0, 1000000, ErrorMessage = "Sell rate per day must be between 0 and 1,000,000.")]
    public decimal? SellRatePerDay { get; set; }

    public decimal ApprovedDays { get; set; }

    public decimal ApprovedOtHours { get; set; }

    public decimal BilledDays { get; set; }

    public decimal BilledOtHours { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Active;
}
