namespace Reporting.Entity.Enums;

/// <summary>How often a scheduled report is emailed (TK-114).</summary>
public enum ReportScheduleFrequency
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
}

/// <summary>What a scheduled report's last run did (TK-114).</summary>
public enum ReportScheduleRunStatus
{
    /// <summary>Emailed to every recipient.</summary>
    Sent = 1,

    /// <summary>Nothing was sent, for a reason that needs a person: the owner lost a permission, say.</summary>
    Skipped = 2,

    /// <summary>The report or the mail failed. Recorded in <c>rpt.ErrorLogs</c> as well.</summary>
    Failed = 3,
}
