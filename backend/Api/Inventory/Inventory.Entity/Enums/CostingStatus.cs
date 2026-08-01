namespace Inventory.Entity.Enums;

/// <summary>
/// Where a movement is in costing.
///
/// Costing is asynchronous: the quantity moves inside the request, and what it
/// cost is settled afterwards by <c>CostingEngine.Worker</c>. This column is how
/// the two stay honest with each other — a movement that has not been costed
/// says so, rather than reporting a cost of zero.
/// </summary>
public enum CostingStatus
{
    /// <summary>Waiting for the worker. The quantity has moved; the cost has not been settled.</summary>
    Pending = 0,

    /// <summary>Claimed by a worker. Claiming is a guarded update, so only one can hold it.</summary>
    InProgress = 1,

    Costed = 2,

    /// <summary>Nothing to cost — a transfer, or an item on weighted average going out.</summary>
    Skipped = 3,

    /// <summary>Gave up after repeated failures. Needs a person; nothing retries it silently forever.</summary>
    Failed = 4,
}
