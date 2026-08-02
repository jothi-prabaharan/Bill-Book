namespace Inventory.Entity.Enums;

/// <summary>
/// Where a movement is in posting to the general ledger.
///
/// Posting is asynchronous for the same reason costing is, and behind it: a
/// movement cannot be posted until what it cost has been settled, and settling
/// the cost is itself work the request does not wait for. So the movement
/// carries where it has got to, and the queue is again the movements table —
/// no broker, ordering from the read, exactly-once from a guarded status claim.
///
/// The alternative was posting from inside the costing transaction. That would
/// tie a stock movement's fate to Accounting being reachable at that instant:
/// either the cost rolls back because the ledger was briefly unavailable, or the
/// cost commits and the posting is lost with no record that it was owed.
/// </summary>
public enum LedgerStatus
{
    /// <summary>Waiting to be posted. The cost may not be settled yet either.</summary>
    Pending = 0,

    /// <summary>Claimed by a worker. Claiming is a guarded update, so only one can hold it.</summary>
    InProgress = 1,

    Posted = 2,

    /// <summary>
    /// Nothing to post. A transfer moves no value, and a receipt against a
    /// purchase document is posted by the document, not by the stock movement.
    /// </summary>
    NotApplicable = 3,

    /// <summary>
    /// Gave up after repeated failures. Needs a person: stock and the ledger
    /// disagree until it is resolved, and silently retrying forever would hide
    /// that.
    /// </summary>
    Failed = 4,
}
