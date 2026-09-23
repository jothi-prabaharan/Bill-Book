namespace Reporting.Entity.Enums;

/// <summary>
/// Mirrors <c>Accounting.Entity.Enums.AssetTransactionType</c>, which
/// <c>acc.AssetTransactions</c> stores <b>by name</b>. The names are the
/// contract, so a rename in Accounting has to be made here too.
/// </summary>
public enum AssetTransactionType
{
    Acquisition,
    Depreciation,
    Disposal,
    Revaluation
}
