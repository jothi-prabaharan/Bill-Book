using System.Net.Http.Json;

namespace Fee.Api.Services;

// Where Fee posts to the general ledger (S4, TK-64): Accounting's internal
// posting route, in Accounting's own shape, the way Sales' LedgerClient does.
// Fee never writes a ledger row itself. A posting is keyed on the document, and
// posting the same document again replaces what was there, so a retry after a
// rollback is safe.

/// <summary>The ledger types Fee uses: 1 is a line, 3 the control leg (receivable, bank).</summary>
public static class LedgerType
{
    public const int Item = 1;
    public const int Control = 3;
}

public sealed class LedgerLeg
{
    public int LedgerTypeId { get; set; }

    public int LedgerSourceId { get; set; } = 1;

    public long TransactionDetailId { get; set; }

    public long? AccountId { get; set; }

    public string? AccountSystemName { get; set; }

    public long? BankAccountId { get; set; }

    /// <summary>1 Contact.</summary>
    public int? SubAccountReferenceType { get; set; }

    public long? SubAccountReferenceId { get; set; }

    /// <summary>0 the trade balance, 2 an overpayment advance.</summary>
    public int SubAccountPurpose { get; set; }

    public decimal DebitAmount { get; set; }

    public decimal CreditAmount { get; set; }
}

public sealed class LedgerPosting
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    public string TransactionTypeCode { get; set; } = null!;

    public long TransactionId { get; set; }

    public DateOnly LedgerDate { get; set; }

    public string? CurrencyCode { get; set; }

    public decimal? ExchangeRate { get; set; }

    public long? ContactId { get; set; }

    public string? DocumentNo { get; set; }

    /// <summary>For a withdrawal (a void), the leg types to clear.</summary>
    public List<int> WithdrawLedgerTypeIds { get; set; } = [];

    public List<LedgerLeg> Legs { get; set; } = [];
}

public sealed record LedgerOutcome(bool Posted, string? Detail);

public interface IFeeLedger
{
    /// <summary>Posts, or says why not. Throws when Accounting cannot be reached.</summary>
    Task<LedgerOutcome> PostAsync(LedgerPosting posting, CancellationToken ct);
}

public sealed class HttpFeeLedger : IFeeLedger
{
    private readonly HttpClient _http;

    public HttpFeeLedger(HttpClient http) => _http = http;

    public async Task<LedgerOutcome> PostAsync(LedgerPosting posting, CancellationToken ct)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync("internal/ledger/postings", posting, ct);
        if (response.IsSuccessStatusCode)
        {
            return new LedgerOutcome(true, null);
        }

        // Accounting's refusal names the leg; it goes to the error log, not to the screen.
        return new LedgerOutcome(false, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
    }
}
