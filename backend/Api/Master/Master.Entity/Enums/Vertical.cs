namespace Master.Entity.Enums;

/// <summary>
/// The trade a branch is in — the vertical that narrows what it is seeded with,
/// what a new item defaults to, and which settings screens apply.
///
/// One per branch, by the owner's decision (master.md 5.14). General is the
/// everything branch: it is seeded with and shown everything, because the
/// asymmetry is one-sided — a jeweller without the metal purities cannot price
/// an ornament, while a chemist with them has unused rows on one screen.
///
/// Changeable after the branch has traded: a vertical change re-seeds what the
/// new vertical adds, and the old vertical's rows stay as data rather than
/// being deleted. The branch form and the seeding read it; nothing else keys
/// off it yet.
/// </summary>
public enum Vertical
{
    /// <summary>Everything. The default for a branch that declares nothing.</summary>
    General = 1,

    Pharma = 2,

    Jewellery = 3,
}