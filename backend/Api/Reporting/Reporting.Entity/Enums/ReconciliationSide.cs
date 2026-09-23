namespace Reporting.Entity.Enums;

/// <summary>
/// Which side of the Fixed Asset Reconciliation a row states: what the asset
/// register says an account should hold, or what the ledger says it does hold.
/// </summary>
public enum ReconciliationSide
{
    Register,
    Ledger
}
