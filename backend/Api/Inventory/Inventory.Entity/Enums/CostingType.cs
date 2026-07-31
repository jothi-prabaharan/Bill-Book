namespace Inventory.Entity.Enums;

/// <summary>
/// How an item's cost is relieved when stock is issued. Chosen per item, and
/// immutable once any movement exists — there is no valid conversion from
/// weighted average to a layered method mid-life, because the layers were never
/// recorded and inventing them would restate every posting since.
/// </summary>
public enum CostingType
{
    /// <summary>Not stocked. Services and non-inventory items.</summary>
    None = 0,

    /// <summary>One average cost per item, company-wide across every branch.</summary>
    WeightedAverage = 1,

    /// <summary>Cost layers consumed oldest receipt first.</summary>
    Fifo = 2,

    /// <summary>Newest receipt first. Disallowed under AS-2 for statutory reporting.</summary>
    Lifo = 3,

    /// <summary>Earliest expiry first — not earliest receipt. Needs batch and expiry tracking.</summary>
    Fefo = 4,

    /// <summary>Each physical piece carries its own cost. Needs serial tracking.</summary>
    SpecificIdentification = 5,
}
