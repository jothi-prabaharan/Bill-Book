namespace Banking.Entity.Enums;

/// <summary>
/// The life of a money document — shared by spend, receive and transfer, which
/// are three tables but one lifecycle.
///
/// One direction only, the same shape the manual journal uses: a posted document
/// is never edited, because a general ledger whose history can be rewritten is
/// not evidence of anything.
/// </summary>
public enum MoneyDocumentStatus
{
    /// <summary>
    /// Being keyed. Its detail lines need not add up to the header amount yet, it
    /// holds no number, and it touches no ledger.
    /// </summary>
    Draft = 0,

    /// <summary>In the ledger, with a number. Immutable from here.</summary>
    Posted = 1,

    /// <summary>
    /// Posted and then withdrawn. The row stays and keeps its number — a gap in
    /// a document series is what an auditor asks about, so a cancelled payment is
    /// marked, never deleted.
    /// </summary>
    Void = 2,
}
