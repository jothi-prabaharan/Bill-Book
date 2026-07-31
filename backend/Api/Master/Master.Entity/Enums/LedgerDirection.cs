namespace Master.Entity.Enums;

/// <summary>Which way money moves for a ledger source — sanity-checked against the transaction type.</summary>
public enum LedgerDirection
{
    In = 1,
    Out = 2,
    Both = 3,
}
