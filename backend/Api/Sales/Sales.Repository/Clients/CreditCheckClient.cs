using Sales.Repository.Repositories;
using System.Net.Http.Json;

namespace Sales.Repository.Clients;

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

        try
        {
            var response = await _http.PostAsJsonAsync("internal/credit/evaluate", request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return new CreditEvaluateResponse { Allowed = true };
            }

            return await response.Content.ReadFromJsonAsync<CreditEvaluateResponse>(ct)
                ?? new CreditEvaluateResponse { Allowed = true };
        }
        catch (HttpRequestException)
        {
            return new CreditEvaluateResponse { Allowed = true };
        }
        catch (TaskCanceledException)
        {
            return new CreditEvaluateResponse { Allowed = true };
        }
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
