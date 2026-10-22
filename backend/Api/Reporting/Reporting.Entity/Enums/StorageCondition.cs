namespace Reporting.Entity.Enums;

/// <summary>
/// Inventory's <c>StorageCondition</c>, a warehouse's <c>StorageType</c>. Inventory stores it by name, so these names must stay Inventory's;
/// the numbers are only this copy's (TK-213).
/// </summary>
public enum StorageCondition
{
    Ambient = 0,
    CoolDry = 1,
    ColdChain = 2,
    Frozen = 3,
}
