using System.Net.Http.Json;

namespace Platform.Api.Services;

/// <summary>
/// Cross-service seam to Master for currency reference data. Platform must not
/// reference Master's DbContext or entities, so the join between
/// plt.OrgCurrencies and mst.Currencies happens in C# over this API.
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

public sealed class MasterCurrenciesClient : IMasterCurrencies
{
    private readonly HttpClient _http;

    public MasterCurrenciesClient(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<MasterCurrency>> GetAllAsync(CancellationToken ct = default)
    {
        List<MasterCurrency>? currencies =
            await _http.GetFromJsonAsync<List<MasterCurrency>>("internal/master/currencies", ct);
        return currencies ?? [];
    }

    public async Task<int?> FindCurrencyIdAsync(string code, CancellationToken ct = default)
    {
        IReadOnlyList<MasterCurrency> all = await GetAllAsync(ct);
        return all.FirstOrDefault(c =>
            string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))?.CurrencyId;
    }
}
