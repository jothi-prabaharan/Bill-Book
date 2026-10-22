namespace Reporting.Entity.Enums;

/// <summary>
/// Inventory's <c>CostingStatus</c>: how far the costing engine has got with a movement. Inventory stores it by name, so these names must stay Inventory's;
/// the numbers are only this copy's (TK-213).
/// </summary>
public enum CostingStatus
{
    Pending = 0,
    InProgress = 1,
    Costed = 2,
    Skipped = 3,
    Failed = 4,
}
