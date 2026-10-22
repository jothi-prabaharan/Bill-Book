namespace Reporting.Entity.Enums;

/// <summary>
/// Inventory's <c>StockDirection</c>: whether a movement adds stock or takes it away. Inventory stores it by name, so these names must stay Inventory's;
/// the numbers are only this copy's (TK-213).
/// </summary>
public enum StockDirection
{
    In = 0,
    Out = 1,
}
