using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Reporting.Entity.TableEntities;

/// <summary>
/// A report emailed as an Excel workbook on a schedule (TK-114): which report,
/// laid out how, how often, and to which of the branch's users.
///
/// <b>It runs as its owner, checked at every run.</b> The owner's permissions
/// are read from Master when the schedule falls due, not when it was saved, so
/// someone who loses <c>purchase.view</c> stops receiving purchase reports on
/// the next run, and the schedule records why.
/// </summary>
public class ReportSchedule : OrgScopedEntity
{
    public long ReportScheduleId { get; set; }

    [Required(ErrorMessage = "Report is required.")]
    [MaxLength(80, ErrorMessage = "Report cannot exceed 80 characters.")]
    public string ReportKey { get; set; } = null!;

    /// <summary>The custom report it was scheduled from, if any. Its layout is copied into <see cref="LayoutJson"/>.</summary>
    public long? CustomReportId { get; set; }

    /// <summary>The email's subject and the schedule's name.</summary>
    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(120, ErrorMessage = "Title cannot exceed 120 characters.")]
    public string Title { get; set; } = null!;

    /// <summary>The layout the report runs with, as a saved view holds it.</summary>
    [Required(ErrorMessage = "Layout is required.")]
    public string LayoutJson { get; set; } = null!;

    public ReportScheduleFrequency Frequency { get; set; }

    /// <summary>The day a weekly schedule sends on.</summary>
    public DayOfWeek? DayOfWeek { get; set; }

    /// <summary>The day a monthly schedule sends on, 1 to 28 so every month has it.</summary>
    [Range(1, 28, ErrorMessage = "Day of month must be between 1 and 28.")]
    public int? DayOfMonth { get; set; }

    /// <summary>The hour it sends at, in <see cref="TimeZoneId"/>.</summary>
    [Range(0, 23, ErrorMessage = "Hour must be between 0 and 23.")]
    public int SendAtHour { get; set; }

    [Required(ErrorMessage = "Time zone is required.")]
    [MaxLength(60, ErrorMessage = "Time zone cannot exceed 60 characters.")]
    public string TimeZoneId { get; set; } = "Asia/Kolkata";

    /// <summary>Who scheduled it, and whose permissions it runs with. No foreign key: users are in the master database.</summary>
    public Guid OwnerUserId { get; set; }

    /// <summary>Users of the branch it is emailed to. A user who has left the branch is passed over.</summary>
    public List<Guid> RecipientUserIds { get; set; } = [];

    public bool IsActive { get; set; } = true;

    /// <summary>When it next falls due. Claimed by moving it forward with a guarded update, which is what makes a run happen once.</summary>
    public DateTimeOffset NextRunAt { get; set; }

    public DateTimeOffset? LastRunAt { get; set; }

    public ReportScheduleRunStatus? LastStatus { get; set; }

    /// <summary>What the last run did, in words for the owner: sent to whom, or why nothing was sent.</summary>
    [MaxLength(500, ErrorMessage = "Last message cannot exceed 500 characters.")]
    public string? LastMessage { get; set; }
}
