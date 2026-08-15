using System.Net.Http.Json;

namespace Purchase.Api.Services;

/// <summary>
/// The due date a payment term implies.
///
/// <b>Asked rather than computed.</b> The rule — net days against day-of-month,
/// and what to do when that day has already passed — belongs to Accounting,
/// which owns the term. Reading <c>DueDays</c> and doing the arithmetic here
/// would make a second implementation, and the two would disagree the first time
/// a day-of-month term was used.
/// </summary>
public interface IPaymentTermClient
{
    Task<DateOnly?> DueDateAsync(
        long paymentTermId,
        Guid customerId,
        Guid orgId,
        DateOnly documentDate,
        CancellationToken ct);
}

public sealed class PaymentTermClient : IPaymentTermClient
{
    private readonly HttpClient _http;

    public PaymentTermClient(HttpClient http) => _http = http;

    public async Task<DateOnly?> DueDateAsync(
        long paymentTermId,
        Guid customerId,
        Guid orgId,
        DateOnly documentDate,
        CancellationToken ct)
    {
        string url =
            $"internal/payment-terms/{paymentTermId}/due-date"
            + $"?customerId={customerId}&orgId={orgId}"
            + $"&documentDate={documentDate:yyyy-MM-dd}";

        HttpResponseMessage response = await _http.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        DueDateResponse? body = await response.Content.ReadFromJsonAsync<DueDateResponse>(ct);
        return body?.DueDate;
    }
}

public sealed class DueDateResponse
{
    public DateOnly DueDate { get; set; }

    public DateOnly? DiscountUntil { get; set; }

    public decimal DiscountPercent { get; set; }
}
