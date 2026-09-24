namespace RateSync.Worker.Rbi;

/// <summary>Fetches the reference-rate page's HTML. A seam so the sync can be tested without RBI.</summary>
public interface IReferenceRatePageSource
{
    Task<string> FetchAsync(CancellationToken ct);
}

/// <summary>
/// The page over HTTP, from <c>Rbi:ReferenceRateUrl</c>. A plain GET with a
/// browser-like user agent; the page needs no session.
/// </summary>
public sealed class HttpReferenceRatePageSource : IReferenceRatePageSource
{
    private readonly HttpClient _http;

    public HttpReferenceRatePageSource(HttpClient http) => _http = http;

    public async Task<string> FetchAsync(CancellationToken ct)
    {
        using HttpResponseMessage response = await _http.GetAsync(string.Empty, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}
