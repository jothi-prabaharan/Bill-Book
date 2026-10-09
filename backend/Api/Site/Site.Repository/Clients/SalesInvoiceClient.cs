using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Site.Repository.Clients;

public sealed class SiteBillingInvoiceLinePayload
{
    public long? ItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? HsnSacCode { get; set; }
    public long? WarehouseId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public sealed class CreateSiteInvoicePayload
{
    public Guid CustomerId { get; set; }
    public Guid OrgId { get; set; }
    public long ContactId { get; set; }
    public DateOnly DocumentDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? Notes { get; set; }
    public List<SiteBillingInvoiceLinePayload> Lines { get; set; } = new();
}

public sealed class CreateSiteInvoiceResult
{
    public bool Success { get; set; }
    public long InvoiceId { get; set; }
    public string? InvoiceNo { get; set; }
    public string? Error { get; set; }
}

public interface ISalesInvoiceClient
{
    Task<CreateSiteInvoiceResult> CreateAndPostInvoiceAsync(
        CreateSiteInvoicePayload payload, CancellationToken ct = default);
}

public sealed class HttpSalesInvoiceClient : ISalesInvoiceClient
{
    private readonly HttpClient _http;
    private readonly ILogger<HttpSalesInvoiceClient> _log;

    public HttpSalesInvoiceClient(HttpClient http, ILogger<HttpSalesInvoiceClient> log)
    {
        _http = http;
        _log = log;
    }

    public async Task<CreateSiteInvoiceResult> CreateAndPostInvoiceAsync(
        CreateSiteInvoicePayload payload, CancellationToken ct = default)
    {
        try
        {
            var req = new
            {
                CustomerId = payload.CustomerId,
                OrgId = payload.OrgId,
                Request = new
                {
                    ContactId = payload.ContactId,
                    DocumentDate = payload.DocumentDate,
                    DueDate = payload.DueDate ?? payload.DocumentDate,
                    Notes = payload.Notes,
                    Lines = payload.Lines.Select(l => new
                    {
                        ItemId = l.ItemId,
                        Description = l.Description,
                        HsnSacCode = l.HsnSacCode,
                        WarehouseId = l.WarehouseId,
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice,
                    }).ToList(),
                }
            };

            HttpResponseMessage res = await _http.PostAsJsonAsync("internal/invoices/create-and-post", req, ct);
            if (!res.IsSuccessStatusCode)
            {
                string err = await res.Content.ReadAsStringAsync(ct);
                _log.LogWarning("Sales invoice create and post refused ({Status}): {Error}", res.StatusCode, err);
                return new CreateSiteInvoiceResult { Success = false, Error = err };
            }

            var result = await res.Content.ReadFromJsonAsync<CreateSiteInvoiceResult>(cancellationToken: ct);
            return result ?? new CreateSiteInvoiceResult { Success = false, Error = "Empty response from Sales" };
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not contact Sales service for invoice creation");
            return new CreateSiteInvoiceResult { Success = false, Error = ex.Message };
        }
    }
}
