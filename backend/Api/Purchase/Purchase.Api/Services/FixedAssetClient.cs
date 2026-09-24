using System.Net.Http.Json;

namespace Purchase.Api.Services;

/// <summary>
/// Where a posted bill's capital lines go onto Accounting's fixed asset register
/// (D-19, TK-12). Purchase never touches <c>acc</c> itself (hard rule 8).
///
/// <b>Safe to repeat.</b> Accounting registers each bill line once, keyed on the
/// line, so a bill post retried after a dropped response registers nothing twice
/// — the same property the ledger's replace-on-post gives the bill's own legs.
/// </summary>
public interface IFixedAssetClient
{
    Task<CapitaliseBillOutcome> CapitaliseBillAsync(CapitaliseBillRequest request, CancellationToken ct);
}

public sealed class FixedAssetClient : IFixedAssetClient
{
    private readonly HttpClient _http;

    public FixedAssetClient(HttpClient http) => _http = http;

    public async Task<CapitaliseBillOutcome> CapitaliseBillAsync(
        CapitaliseBillRequest request, CancellationToken ct)
    {
        HttpResponseMessage response =
            await _http.PostAsJsonAsync("internal/fixed-assets/capitalise-bill", request, ct);

        if (response.IsSuccessStatusCode)
        {
            return new CapitaliseBillOutcome(true, null);
        }

        string detail = await response.Content.ReadAsStringAsync(ct);

        return new CapitaliseBillOutcome(
            false,
            $"The fixed asset register refused the capital lines ({(int)response.StatusCode}): {detail}");
    }
}

public sealed record CapitaliseBillOutcome(bool Registered, string? Detail);

/// <summary>Accounting's <c>CapitaliseBillRequest</c>, in Purchase's own copy — Purchase does not reference its assemblies.</summary>
public sealed class CapitaliseBillRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    public long PurchaseBillId { get; set; }

    public string DocumentNo { get; set; } = null!;

    public DateOnly DocumentDate { get; set; }

    public List<CapitaliseBillLine> Lines { get; set; } = [];
}

public sealed class CapitaliseBillLine
{
    public long BillDetailId { get; set; }

    public int LineNumber { get; set; }

    public long FixedAssetCategoryId { get; set; }

    public string Description { get; set; } = null!;

    /// <summary>The line's taxable value — what the bill debited to Fixed Asset.</summary>
    public decimal Amount { get; set; }
}
