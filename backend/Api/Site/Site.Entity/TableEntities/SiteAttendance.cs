using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Shared.Kernel.Tenancy;
using Site.Entity.Enums;

namespace Site.Entity.TableEntities;

/// <summary>
/// Laborer daily check-in and clock-out record (TK-171).
/// </summary>
[Table("SiteAttendance", Schema = "sit")]
public class SiteAttendance : OrgScopedEntity
{
    [Key]
    public long AttendanceId { get; set; }

    [Required(ErrorMessage = "Assignment is required.")]
    public long AssignmentId { get; set; }

    [Required(ErrorMessage = "Site is required.")]
    public long SiteId { get; set; }

    [Required(ErrorMessage = "Laborer is required.")]
    public long LaborerId { get; set; }

    [Required(ErrorMessage = "Work date is required.")]
    public DateOnly WorkDate { get; set; }

    [Required(ErrorMessage = "Check-in time is required.")]
    public DateTime CheckInTime { get; set; }

    public decimal? CheckInLat { get; set; }

    public decimal? CheckInLng { get; set; }

    public bool IsGeofenceBreached { get; set; }

    public DateTime? ClockOutTime { get; set; }

    [Range(0, 1.0, ErrorMessage = "Day fraction must be between 0 and 1.0.")]
    public decimal DayFraction { get; set; } = 1.0m;

    [Range(0, 24, ErrorMessage = "OT hours claimed must be between 0 and 24.")]
    public decimal OtHoursClaimed { get; set; }

    [Range(0, 24, ErrorMessage = "OT hours approved must be between 0 and 24.")]
    public decimal OtHoursApproved { get; set; }

    [StringLength(1000, ErrorMessage = "Work summary cannot exceed 1000 characters.")]
    public string? WorkSummary { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    public AttendanceStatus Status { get; set; } = AttendanceStatus.CheckedIn;

    public long? ApprovalRequestId { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public long? ApprovedBy { get; set; }

    [StringLength(500, ErrorMessage = "Rejection reason cannot exceed 500 characters.")]
    public string? RejectionReason { get; set; }

    public decimal DayCostSnapshot { get; set; }

    public decimal DayMarginSnapshot { get; set; }

    public decimal DaySellSnapshot { get; set; }

    public decimal OtCostSnapshot { get; set; }

    public decimal OtMarginSnapshot { get; set; }

    public decimal OtSellSnapshot { get; set; }

    public long? SiteBillLineId { get; set; }
}
