using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Site.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Site.Entity.TableEntities;

[Table("SiteMaterialUsages", Schema = "sit")]
public class SiteMaterialUsage : OrgScopedEntity
{
    [Key]
    public long SiteMaterialUsageId { get; set; }

    [Required(ErrorMessage = "Usage number is required.")]
    [StringLength(30, ErrorMessage = "Usage number cannot exceed 30 characters.")]
    public string UsageNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Site is required.")]
    public long SiteId { get; set; }

    [Required(ErrorMessage = "Reporting laborer is required.")]
    public long ReportedByLaborerId { get; set; }

    public DateOnly UsageDate { get; set; }

    [Required(ErrorMessage = "Approval status is required.")]
    public UsageApprovalStatus ApprovalStatus { get; set; } = UsageApprovalStatus.Pending;

    public Guid? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    public string? Notes { get; set; }
}
