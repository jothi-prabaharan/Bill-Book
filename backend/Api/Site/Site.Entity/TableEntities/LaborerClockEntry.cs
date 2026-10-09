using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Site.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Site.Entity.TableEntities;

[Table("LaborerClockEntries", Schema = "sit")]
public class LaborerClockEntry : OrgScopedEntity
{
    [Key]
    public long LaborerClockEntryId { get; set; }

    [Required(ErrorMessage = "Laborer is required.")]
    public long LaborerId { get; set; }

    [Required(ErrorMessage = "Site is required.")]
    public long SiteId { get; set; }

    [Required(ErrorMessage = "Clock-in time is required.")]
    public DateTime ClockInUtc { get; set; }

    public DateTime? ClockOutUtc { get; set; }

    public decimal? ClockInLatitude { get; set; }

    public decimal? ClockInLongitude { get; set; }

    public decimal? ClockOutLatitude { get; set; }

    public decimal? ClockOutLongitude { get; set; }

    [StringLength(500, ErrorMessage = "Selfie photo URL cannot exceed 500 characters.")]
    public string? SelfiePhotoUrl { get; set; }

    [StringLength(500, ErrorMessage = "Site photo URL cannot exceed 500 characters.")]
    public string? SitePhotoUrl { get; set; }

    [Range(0, 24, ErrorMessage = "Regular hours must be between 0 and 24.")]
    public decimal RegularHours { get; set; }

    [Range(0, 24, ErrorMessage = "Overtime hours must be between 0 and 24.")]
    public decimal OvertimeHours { get; set; }

    public bool IsOtClaimed { get; set; }

    [StringLength(500, ErrorMessage = "Overtime reason cannot exceed 500 characters.")]
    public string? OtReason { get; set; }

    [Required(ErrorMessage = "Approval status is required.")]
    public ClockApprovalStatus ApprovalStatus { get; set; } = ClockApprovalStatus.Pending;

    public Guid? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    [StringLength(500, ErrorMessage = "Rejection reason cannot exceed 500 characters.")]
    public string? RejectionReason { get; set; }
}
