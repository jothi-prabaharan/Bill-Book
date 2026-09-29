using System.Net.Http.Json;
using Customer.Entity.Models;

namespace Customer.Repository.Clients;

public interface IReportingClient
{
    Task<List<long>> SegmentContactsAsync(Guid customerId, Guid orgId, PurchaseHistoryRule rule, CancellationToken ct);
}

public sealed class ReportingClient : IReportingClient
{
    private readonly HttpClient _http;

    public ReportingClient(HttpClient http) => _http = http;

    public async Task<List<long>> SegmentContactsAsync(Guid customerId, Guid orgId, PurchaseHistoryRule rule, CancellationToken ct)
    {
        var request = new
        {
            CustomerId = customerId,
            OrgId = orgId,
            rule.LastInvoiceBefore,
            rule.LastInvoiceAfter,
            rule.SalesMinAmount,
            rule.SalesMaxAmount,
            rule.SalesPeriodFrom,
            rule.SalesPeriodTo,
            rule.ItemCategoryId
        };

        HttpResponseMessage response = await _http.PostAsJsonAsync("internal/contacts/segment", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var result = await response.Content.ReadFromJsonAsync<ContactSegmentResponseDto>(ct);
        return result?.ContactIds ?? [];
    }

    private sealed class ContactSegmentResponseDto
    {
        public List<long> ContactIds { get; set; } = [];
    }
}
