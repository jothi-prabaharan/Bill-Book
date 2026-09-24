using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace RateSync.Worker.Ibja;

public class HttpIbjaClient : IIbjaClient
{
    private readonly HttpClient _client;
    
    public HttpIbjaClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<IbjaRates> FetchRatesAsync(string apiKey, CancellationToken ct)
    {
        using var response = await _client.GetAsync($"?access_token={apiKey}", ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<IbjaResponse>(cancellationToken: ct);
        if (body is null || !body.Success || body.Rates is null)
        {
            throw new InvalidOperationException("Failed to fetch rates from IBJA or response was malformed.");
        }

        return new IbjaRates
        {
            Date = DateOnly.Parse(body.Date),
            Rates = body.Rates
        };
    }

    private class IbjaResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; } = "";

        [JsonPropertyName("rates")]
        public Dictionary<string, decimal>? Rates { get; set; }
    }
}
