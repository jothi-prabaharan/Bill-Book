namespace Reporting.Entity.Enums;

/// <summary>
/// Mirrors <c>Accounting.Entity.Enums.FixedAssetStatus</c>, which
/// <c>acc.FixedAssets</c> stores <b>by name</b>. The names are the contract, so
/// a rename in Accounting has to be made here too; the values are not stored.
/// </summary>
public enum FixedAssetStatus
{
    Draft,
    Active,
    Disposed
}
