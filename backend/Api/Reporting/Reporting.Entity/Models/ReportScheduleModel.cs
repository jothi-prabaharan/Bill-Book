using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

/// <summary>A scheduled report as the API carries it (TK-114).</summary>
public class ReportScheduleModel
{
    public long ReportScheduleId { get; set; }

    [Required(ErrorMessage = "Report is required.")]
    [MaxLength(80, ErrorMessage = "Report cannot exceed 80 characters.")]
    public string ReportKey { get; set; } = null!;

    public long? CustomReportId { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(120, ErrorMessage = "Title cannot exceed 120 characters.")]
    public string Title { get; set; } = null!;

    public ReportScheduleFrequency Frequency { get; set; } = ReportScheduleFrequency.Weekly;

    public DayOfWeek? DayOfWeek { get; set; }

    [Range(1, 28, ErrorMessage = "Day of month must be between 1 and 28.")]
    public int? DayOfMonth { get; set; }

    [Range(0, 23, ErrorMessage = "Hour must be between 0 and 23.")]
    public int SendAtHour { get; set; } = 8;

    [MaxLength(60, ErrorMessage = "Time zone cannot exceed 60 characters.")]
    public string? TimeZoneId { get; set; }

    [MinLength(1, ErrorMessage = "Choose at least one recipient.")]
    [MaxLength(20, ErrorMessage = "Choose at most 20 recipients.")]
    public List<Guid> RecipientUserIds { get; set; } = [];

    public bool IsActive { get; set; } = true;

    [Required(ErrorMessage = "A schedule needs a layout.")]
    public ReportQueryRequest Layout { get; set; } = new();

    /// <summary>Set by the server.</summary>
    public DateTimeOffset? NextRunAt { get; set; }

    /// <summary>Set by the server.</summary>
    public DateTimeOffset? LastRunAt { get; set; }

    /// <summary>Set by the server.</summary>
    public ReportScheduleRunStatus? LastStatus { get; set; }

    /// <summary>Set by the server.</summary>
    public string? LastMessage { get; set; }
}

/// <summary>A user of the branch a schedule can be sent to.</summary>
public class ReportRecipientView
{
    public Guid UserId { get; set; }

    public string DisplayName { get; set; } = null!;
}
