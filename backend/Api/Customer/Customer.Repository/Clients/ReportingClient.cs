using System.Net.Http.Json;
using Customer.Entity.Models;

namespace Customer.Repository.Clients;

public interface IReportingClient
{
    Task<List<long>> SegmentContactsAsync(Guid customerId, Guid orgId, PurchaseHistoryRule rule, CancellationToken ct);
    Task<Dictionary<long, (DateOnly Date, decimal Total)>> GetFirstInvoicesAsync(Guid customerId, Guid orgId, List<long> contactIds, CancellationToken ct);
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

    public async Task<Dictionary<long, (DateOnly Date, decimal Total)>> GetFirstInvoicesAsync(
        Guid customerId, Guid orgId, List<long> contactIds, CancellationToken ct)
    {
        if (contactIds == null || contactIds.Count == 0)
        {
            return [];
        }

        var request = new
        {
            CustomerId = customerId,
            OrgId = orgId,
            ContactIds = contactIds.Distinct().ToList()
        };

        HttpResponseMessage response = await _http.PostAsJsonAsync("internal/contacts/first-invoices", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var result = await response.Content.ReadFromJsonAsync<ContactFirstInvoicesResponseDto>(ct);
        if (result?.FirstInvoices is null)
        {
            return [];
        }

        return result.FirstInvoices.ToDictionary(
            x => x.ContactId,
            x => (x.FirstInvoiceDate, x.FirstInvoiceTotal));
    }

    private sealed class ContactSegmentResponseDto
    {
        public List<long> ContactIds { get; set; } = [];
    }

    private sealed class ContactFirstInvoicesResponseDto
    {
        public List<ContactFirstInvoiceItemDto> FirstInvoices { get; set; } = [];
    }

    private sealed class ContactFirstInvoiceItemDto
    {
        public long ContactId { get; set; }
        public DateOnly FirstInvoiceDate { get; set; }
        public decimal FirstInvoiceTotal { get; set; }
    }
}
