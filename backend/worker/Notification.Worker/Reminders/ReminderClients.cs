using System.Net.Http.Json;
using Shared.Kernel.Documents;

namespace Notification.Worker.Reminders;

/// <summary>What is still owed on each invoice, from Accounting's ledger.</summary>
public interface IInvoiceSettlements
{
    /// <summary>
    /// Outstanding amount per invoice id, or null when Accounting could not be
    /// read — in which case nobody is reminded this run, because reminding a
    /// customer who has paid is worse than reminding one a day late.
    /// </summary>
    Task<IReadOnlyDictionary<long, decimal>?> OutstandingAsync(
        Guid customerId, Guid orgId, IReadOnlyCollection<long> invoiceIds, CancellationToken ct);
}

/// <summary>
/// Accounting's <c>internal/ledger/settlements</c>, the same read the invoice
/// list uses for its paid column: the receivable (CONTROL) leg of each invoice
/// against what has been allocated to it.
/// </summary>
public sealed class HttpInvoiceSettlements : IInvoiceSettlements
{
    private const int ControlLedgerType = 3;

    private readonly HttpClient _http;

    public HttpInvoiceSettlements(HttpClient http) => _http = http;

    public async Task<IReadOnlyDictionary<long, decimal>?> OutstandingAsync(
        Guid customerId, Guid orgId, IReadOnlyCollection<long> invoiceIds, CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage response = await _http.PostAsJsonAsync(
                "internal/ledger/settlements",
                new
                {
                    customerId,
                    orgId,
                    transactionTypeCode = "INV",
                    ledgerTypeId = ControlLedgerType,
                    transactionIds = invoiceIds,
                },
                ct);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            List<SettlementRow>? rows = await response.Content.ReadFromJsonAsync<List<SettlementRow>>(ct);
            return rows?.ToDictionary(r => r.TransactionId, r => r.OutstandingAmount);
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

    private sealed record SettlementRow(long TransactionId, decimal TotalAmount, decimal PaidAmount, decimal OutstandingAmount);
}

/// <summary>Where each contact is written to, from Master (the worker may not read <c>con</c>).</summary>
public interface IContactEmails
{
    /// <summary>Null when Master could not be read; a contact with no email is simply absent.</summary>
    Task<IReadOnlyDictionary<long, ContactEmail>?> ForAsync(
        Guid customerId, Guid orgId, IReadOnlyCollection<long> contactIds, CancellationToken ct);
}

public sealed class HttpContactEmails : IContactEmails
{
    private readonly HttpClient _http;

    public HttpContactEmails(HttpClient http) => _http = http;

    public async Task<IReadOnlyDictionary<long, ContactEmail>?> ForAsync(
        Guid customerId, Guid orgId, IReadOnlyCollection<long> contactIds, CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage response = await _http.PostAsJsonAsync(
                "internal/contacts/emails",
                new NameLookupRequest { Ids = [.. contactIds], CustomerId = customerId, OrgId = orgId },
                ct);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            List<ContactEmail>? rows = await response.Content.ReadFromJsonAsync<List<ContactEmail>>(ct);
            return rows?.ToDictionary(r => r.ContactId);
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
