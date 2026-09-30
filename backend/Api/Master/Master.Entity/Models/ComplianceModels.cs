using System.ComponentModel.DataAnnotations;

namespace Master.Entity.Models;

public class UpcomingComplianceItem
{
    public int ComplianceDueDateId { get; set; }
    public string FormName { get; set; } = null!;
    public string Category { get; set; } = null!;
    public string PeriodName { get; set; } = null!;
    public string PeriodKey { get; set; } = null!;
    public DateOnly DueDate { get; set; }
    public int DaysRemaining { get; set; }
    public bool IsExtended { get; set; }
    public string? ExtensionNotice { get; set; }
    public bool IsReady { get; set; }
    public string Status { get; set; } = null!;
    public string RuleDescription { get; set; } = null!;
    public bool IsFiled { get; set; }
}

public class ComplianceCalendarItem
{
    public int ComplianceDueDateId { get; set; }
    public string FormName { get; set; } = null!;
    public string Category { get; set; } = null!;
    public string PeriodName { get; set; } = null!;
    public string PeriodKey { get; set; } = null!;
    public DateOnly DueDate { get; set; }
    public bool IsExtended { get; set; }
    public string? ExtensionNotice { get; set; }
    public string RuleDescription { get; set; } = null!;
    public bool IsApplicable { get; set; }
    public bool IsFiled { get; set; }
    public DateTimeOffset? FiledAt { get; set; }
}

public class MarkComplianceFiledRequest
{
    [Required(ErrorMessage = "Period is required.")]
    [MaxLength(20, ErrorMessage = "Period cannot exceed 20 characters.")]
    public string Period { get; set; } = null!;
}

public class ExtendDueDateRequest
{
    [Required(ErrorMessage = "Specific period is required.")]
    [MaxLength(20, ErrorMessage = "Specific period cannot exceed 20 characters.")]
    public string SpecificPeriod { get; set; } = null!;

    [Required(ErrorMessage = "Extended due date is required.")]
    public DateOnly ExtendedDueDate { get; set; }

    [MaxLength(500, ErrorMessage = "Extension notice cannot exceed 500 characters.")]
    public string? ExtensionNotice { get; set; }
}
