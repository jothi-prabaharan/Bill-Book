using System.Net.Http.Json;

namespace Sales.Api.Services;

public interface ICreditCheckClient
{
    Task<CreditEvaluateResponse> EvaluateAsync(long contactId, decimal newOrderAmountBase, CancellationToken ct);
}

public class CreditCheckClient : ICreditCheckClient
{
    private readonly HttpClient _http;

    public CreditCheckClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<CreditEvaluateResponse> EvaluateAsync(long contactId, decimal newOrderAmountBase, CancellationToken ct)
    {
        var request = new CreditEvaluateRequest
        {
            ContactId = contactId,
            NewOrderAmountBase = newOrderAmountBase
        };

        var response = await _http.PostAsJsonAsync("internal/credit/evaluate", request, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CreditEvaluateResponse>(ct)
            ?? new CreditEvaluateResponse { Allowed = true };
    }
}

public class CreditEvaluateRequest
{
    public long ContactId { get; set; }
    public decimal NewOrderAmountBase { get; set; }
}

public class CreditEvaluateResponse
{
    public bool Allowed { get; set; }
    public string? Reason { get; set; }
}
