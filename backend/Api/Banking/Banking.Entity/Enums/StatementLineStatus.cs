namespace Banking.Entity.Enums;

/// <summary>
/// What has been decided about one line of a bank statement.
///
/// <b>None of these states writes to the general ledger.</b> Reconciling is not
/// posting: the money already moved and was already posted by whatever document
/// recorded it. A statement line is the bank's account of the same event, and
/// matching the two says the bank and the books agree. Conflating that with
/// posting is how a reconciliation run doubles a month of transactions.
/// </summary>
public enum StatementLineStatus
{
    /// <summary>
    /// Imported and nothing decided. Either the document it belongs to has not
    /// been found yet, or there is no document — a bank charge nobody keyed.
    /// </summary>
    Unmatched = 0,

    /// <summary>
    /// Tied to a posted money document. The bank and the books agree about this
    /// one movement.
    /// </summary>
    Matched = 1,

    /// <summary>
    /// Deliberately set aside: a line that is genuinely not the organization's
    /// business, or a duplicate the bank itself printed twice.
    ///
    /// Kept rather than deleted, and it needs a reason. A line that vanishes is
    /// indistinguishable from one that was never imported, and the difference is
    /// exactly what an auditor asks about.
    /// </summary>
    Ignored = 2,
}
