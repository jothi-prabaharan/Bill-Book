namespace Master.Api.Services;

/// <summary>
/// Currency reference data — the master list, and the id behind an ISO code.
///
/// This was the seam from Platform to Master: the two owned different schemas in
/// the same database, could not reference each other's contexts, and so joined
/// OrgCurrencies to Currencies in C# over an API. Both schemas are mst now and
/// <see cref="MasterCurrencies"/> does it as a query. The projection stays,
/// because the callers want the currency's display shape rather than the entity.
/// </summary>
public interface IMasterCurrencies
{
    Task<IReadOnlyList<MasterCurrency>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Resolves an ISO code to its master id, or null when unknown.</summary>
    Task<int?> FindCurrencyIdAsync(string code, CancellationToken ct = default);
}

public sealed class MasterCurrency
{
    public int CurrencyId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Symbol { get; set; } = null!;

    public string Format { get; set; } = null!;

    public int DecimalPlaces { get; set; }
}
