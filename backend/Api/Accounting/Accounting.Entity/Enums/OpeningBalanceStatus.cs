namespace Accounting.Entity.Enums;

/// <summary>
/// The two states of an opening balance, and there is no way back from the
/// second.
///
/// <b>Not the three-state lifecycle every other document has.</b> A journal or a
/// payment can be voided or reversed because it is one event among thousands.
/// An opening balance is the moment the books begin: reversing it does not
/// restate a transaction, it deletes the starting position that every balance
/// since has been measured from. Anything wrong with it after go-live is
/// corrected the way any other error in a closed period is — with a journal
/// entry that says what changed and why.
/// </summary>
public enum OpeningBalanceStatus
{
    /// <summary>
    /// Being keyed. It may be wildly out of balance, it holds no number, and it
    /// touches no ledger — which is what makes it usable across the weeks a
    /// migration actually takes.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Go-live. In the ledger, numbered, and read-only from here.
    /// </summary>
    Finalized = 1,
}
