namespace Reporting.Entity.Enums;

/// <summary>
/// Inventory's <c>LedgerStatus</c>: whether a movement's cost has been posted to the ledger. Inventory stores it by name, so these names must stay Inventory's;
/// the numbers are only this copy's (TK-213).
/// </summary>
public enum LedgerStatus
{
    Pending = 0,
    InProgress = 1,
    Posted = 2,
    NotApplicable = 3,
    Failed = 4,
}
