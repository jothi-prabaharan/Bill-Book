using System.Net.Http.Json;

namespace Claims.Api.Services;

public interface IAccountingClient
{
    Task<PostLedgerOutcomeResult> PostLedgerAsync(PostLedgerRequest request, CancellationToken ct);
}

public sealed class AccountingClient : IAccountingClient
{
    private readonly HttpClient _http;

    public AccountingClient(HttpClient http) => _http = http;

    public async Task<PostLedgerOutcomeResult> PostLedgerAsync(PostLedgerRequest request, CancellationToken ct)
    {
        try
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
        catch (Exception ex)
        {
            return new PostLedgerOutcomeResult(false, ex.Message);
        }
    }
}

public sealed record PostLedgerOutcomeResult(bool Posted, string? Detail);

public sealed class PostLedgerRequest
{
    public Guid CustomerId { get; set; }
    public Guid OrgId { get; set; }
    public string TransactionTypeCode { get; set; } = "CLM";
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
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Narration { get; set; }
}
