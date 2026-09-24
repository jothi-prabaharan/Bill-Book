using System.Net.Http.Json;

namespace Payroll.Api.Services;

public interface ILedgerClient
{
    Task<PostLedgerOutcomeResult> PostAsync(PostLedgerRequest request, CancellationToken ct);
}

public sealed class LedgerClient : ILedgerClient
{
    private readonly HttpClient _http;

    public LedgerClient(HttpClient http) => _http = http;

    public async Task<PostLedgerOutcomeResult> PostAsync(
        PostLedgerRequest request, CancellationToken ct)
    {
        HttpResponseMessage response =
            await _http.PostAsJsonAsync("internal/ledger/postings", request, ct);

        if (response.IsSuccessStatusCode)
        {
            return new PostLedgerOutcomeResult(true, null);
        }

        string detail = await response.Content.ReadAsStringAsync(ct);
        return new PostLedgerOutcomeResult(
            false, $"The ledger refused the posting ({(int)response.StatusCode}): {detail}");
    }
}

public sealed record PostLedgerOutcomeResult(bool Posted, string? Detail);

public sealed class PostLedgerRequest
{
    public Guid CustomerId { get; set; }
    public Guid OrgId { get; set; }
    public string TransactionTypeCode { get; set; } = "PAY";
    public long TransactionId { get; set; }
    public DateOnly LedgerDate { get; set; }
    public string? CurrencyCode { get; set; }
    public decimal? ExchangeRate { get; set; }
    public long? ContactId { get; set; }
    public long? SourceDocumentId { get; set; }
    public string? DocumentNo { get; set; }
    public List<int> WithdrawLedgerTypeIds { get; set; } = [];
    public List<LedgerLegRequest> Legs { get; set; } = [];
}

public sealed class LedgerLegRequest
{
    public int LedgerTypeId { get; set; }
    public int LedgerSourceId { get; set; } = 1;
    public long TransactionDetailId { get; set; }
    public string? AccountSystemName { get; set; }
    public long? AccountId { get; set; }
    public int? SubAccountReferenceType { get; set; }
    public long? SubAccountReferenceId { get; set; }
    public int SubAccountPurpose { get; set; }
    public int SubAccountTaxComponent { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? TransactionDesc { get; set; }
}
