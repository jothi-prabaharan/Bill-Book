namespace Reporting.Entity.Enums;

/// <summary>A project's status, as Accounting stores it: by name, so these names must stay Accounting's (TK-207).</summary>
public enum ProjectStatus
{
    Active = 1,

    /// <summary>Paused; it still accepts postings.</summary>
    OnHold = 2,

    /// <summary>Finished; it accepts no new postings.</summary>
    Completed = 3,

    Cancelled = 4,
}
