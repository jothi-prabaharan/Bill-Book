using System.Net.Http.Json;

namespace Shared.Kernel.School;

// AMC contracts due a renewal reminder (TK-68), read by Notification.Worker from
// MaintenanceContract's internal/amc/renewals-due. The worker sends each reminder once per
// contract term, under a message id fixed by branch, contract and end date.

public sealed class AmcRenewalQuery
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    public DateOnly On { get; set; }
}

public sealed class AmcRenewalDue
{
    public long AmcContractId { get; set; }

    public string ContractNo { get; set; } = null!;

    public string VendorName { get; set; } = null!;

    public DateOnly EndDate { get; set; }

    public decimal ContractValue { get; set; }

    /// <summary>Who to remind. Null when the contract names nobody, and then no reminder can go.</summary>
    public string? ReminderEmail { get; set; }
}

public interface IAmcRenewals
{
    /// <summary>The branch's contracts due a reminder on <paramref name="on"/>, or null when MaintenanceContract could not be read.</summary>
    Task<IReadOnlyList<AmcRenewalDue>?> DueAsync(Guid customerId, Guid orgId, DateOnly on, CancellationToken ct);
}

public sealed class HttpAmcRenewals : IAmcRenewals
{
    private readonly HttpClient _http;

    public HttpAmcRenewals(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<AmcRenewalDue>?> DueAsync(Guid customerId, Guid orgId, DateOnly on, CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage response = await _http.PostAsJsonAsync(
                "internal/amc/renewals-due", new AmcRenewalQuery { CustomerId = customerId, OrgId = orgId, On = on }, ct);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<List<AmcRenewalDue>>(ct) ?? []
                : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }
}
