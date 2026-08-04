using System.ComponentModel.DataAnnotations;

namespace Accounting.Entity.Models;

/// <summary>One role's closing date, as the settings screen shows it.</summary>
public class PeriodLockListItem
{
    public long PeriodLockId { get; set; }

    public int RoleId { get; set; }

    /// <summary>Inclusive — nothing posts on or before this date.</summary>
    public DateOnly LockedUpto { get; set; }

    public string? Note { get; set; }
}

/// <summary>Sets or moves one role's closing date. One row per role, so this upserts.</summary>
public class SavePeriodLockRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Choose the role this closing date applies to.")]
    public int RoleId { get; set; }

    /// <summary>
    /// Inclusive. Nothing posts, reverses or voids on or before this date for
    /// the chosen role.
    /// </summary>
    public DateOnly LockedUpto { get; set; }

    /// <summary>Why the period was closed. Read far more often than it is written.</summary>
    public string? Note { get; set; }
}

/// <summary>What a caller is allowed to post, for a screen to grey out dates with.</summary>
public class PeriodLockStatus
{
    /// <summary>Null when nothing stops this caller.</summary>
    public DateOnly? LockedUpto { get; set; }

    /// <summary>
    /// The first date this caller may still post on. Null when unrestricted —
    /// a date picker binds its minimum to this and the refusal never happens.
    /// </summary>
    public DateOnly? OpenFrom { get; set; }
}
