namespace Reporting.Entity.Enums;

/// <summary>
/// Inventory's <c>CostingType</c>: an item's costing method, frozen once stock moves. Inventory stores it by name, so these names must stay Inventory's;
/// the numbers are only this copy's (TK-213).
/// </summary>
public enum CostingType
{
    None = 0,
    WeightedAverage = 1,
    Fifo = 2,
    Lifo = 3,
    Fefo = 4,
    SpecificIdentification = 5,
    MovingAverage = 6,
}
