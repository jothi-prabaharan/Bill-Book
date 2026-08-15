using System.Net.Http.Json;
using Shared.Kernel.Tenancy;

namespace Sales.Api.Services;

public interface ILedgerClient
{
    Task<bool> PostAsync(PostLedgerRequest request, CancellationToken ct);
    Task<bool> AllocateAsync(AllocateTransactionRequest request, CancellationToken ct);
}

public sealed class LedgerClient : ILedgerClient
{
    private readonly HttpClient _http;
    private readonly TenantContext _tenant;

    public LedgerClient(HttpClient http, TenantContext tenant)
    {
        _http = http;
        _tenant = tenant;
    }

    public async Task<bool> AllocateAsync(AllocateTransactionRequest request, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("internal/allocations", request, ct);
        if (response.IsSuccessStatusCode)
            return true;
        
        var content = await response.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException($"Allocation failed: {response.StatusCode} {content}");
    }

    public async Task<bool> PostAsync(PostLedgerRequest request, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("internal/ledger/postings", request, ct);
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        var content = await response.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException($"Ledger post failed: {response.StatusCode} {content}");
    }
}

public sealed class PostLedgerRequest
{
    public Guid CustomerId { get; set; }
    public Guid OrgId { get; set; }
    public string TransactionTypeCode { get; set; } = null!;
    public long TransactionId { get; set; }
    public DateOnly LedgerDate { get; set; }
    public string? CurrencyCode { get; set; }
    public decimal? ExchangeRate { get; set; }
    public long? ContactId { get; set; }
    public long? SourceDocumentId { get; set; }
    public long? JournalId { get; set; }
    public List<int> WithdrawLedgerTypeIds { get; set; } = [];
    public List<LedgerLegRequest> Legs { get; set; } = [];
}

public sealed class LedgerLegRequest
{
    public int LedgerTypeId { get; set; }
    public int LedgerSourceId { get; set; }
    public long TransactionDetailId { get; set; }
    public string? AccountSystemName { get; set; }
    public long? AccountId { get; set; }
    public long? SubAccountReferenceId { get; set; }
    public decimal Amount { get; set; }
}

public sealed class AllocateTransactionRequest
{
    public string SourceTransactionTypeCode { get; set; } = null!;
    public long SourceTransactionId { get; set; }
    public string TargetTransactionTypeCode { get; set; } = null!;
    public long TargetTransactionId { get; set; }
    public decimal Amount { get; set; }
}
