using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>
/// How far back the books are closed, for one role in one branch.
///
/// <b>A soft close, not a hard one.</b> One date per branch per role is what
/// makes it useful: a month can be shut to everyone who keys documents while
/// staying open to whoever has to make the adjusting entries the close itself
/// produced. A single date for the whole branch would mean closing the books on
/// the person doing the closing.
///
/// <b>The date is inclusive.</b> <see cref="LockedUpto"/> of 31 March means
/// nothing may post <i>on or before</i> that day.
///
/// <b>A role with no row here is not locked.</b> Absence of configuration is not
/// a restriction — a branch that has never closed a period has no rows at all,
/// and everyone posts freely. That is the right default because the alternative
/// locks a product nobody has configured yet.
///
/// <see cref="RoleId"/> is a plain int with no foreign key: roles live in
/// <c>idn.Roles</c>, in the master database, and no per-customer table can
/// reference across that boundary. A role deleted in Identity leaves a row here
/// that matches nobody, which costs nothing.
/// </summary>
public class PeriodLock : OrgScopedEntity
{
    public long PeriodLockId { get; set; }

    /// <summary>
    /// The role this date applies to. One row per role per branch — a user holds
    /// exactly one role in a branch, so this resolves to exactly one date per
    /// user with nothing to reconcile.
    /// </summary>
    public int RoleId { get; set; }

    /// <summary>
    /// Nothing posts, reverses or voids on or before this date. Moving it
    /// backwards reopens a period, which is why the screen behind it is an
    /// audited action rather than a form field.
    /// </summary>
    public DateOnly LockedUpto { get; set; }

    /// <summary>Why the period was closed to this role. Read far more often than it is written.</summary>
    public string? Note { get; set; }
}
