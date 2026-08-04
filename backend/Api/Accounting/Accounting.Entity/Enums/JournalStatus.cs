namespace Accounting.Entity.Enums;

/// <summary>
/// The life of a journal. One direction only: a posted entry is never edited
/// and never deleted, it is offset by a reversing entry that stands beside it in
/// the ledger. History that can be rewritten is not history.
/// </summary>
public enum JournalStatus
{
    /// <summary>
    /// Being written. May be unbalanced, carries no number, and posts nothing —
    /// the balance trigger and the numbering series both wait for the post.
    /// </summary>
    Draft = 0,

    /// <summary>In the ledger. Immutable from here.</summary>
    Posted = 1,

    /// <summary>
    /// Posted, and then offset in full by a reversing journal. The rows stay
    /// exactly where they were; the reversal sits beside them.
    /// </summary>
    Reversed = 2,
}
