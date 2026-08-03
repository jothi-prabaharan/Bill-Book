using System.Net;
using System.Net.Http.Json;

namespace Inventory.Api.Services;

/// <summary>
/// Posts to the general ledger. Only Accounting writes ledger rows, so this is
/// a call rather than an insert.
///
/// The tenant travels in the body rather than in a token, because the caller is
/// a background worker: costing settles minutes after the sale and posting
/// follows it, long after whoever made the sale has gone home.
/// </summary>
public interface IAccountingLedger
{
    Task<LedgerPostOutcome> PostAsync(LedgerPosting posting, CancellationToken ct);
}

/// <summary>
/// A posting as Inventory describes it. Accounting decides what lands.
///
/// The leg type, the ledger source and the document line all sit on the leg
/// rather than here: a document produces several of each at once, and they are
/// what say which rows a leg replaces and what produced it. Inventory only ever
/// sends COGS legs on one line at a time under one source, but the shape is the
/// ledger's, not this caller's.
/// </summary>
public sealed record LedgerPosting(
    Guid CustomerId,
    Guid OrgId,
    string TransactionTypeCode,
    long TransactionId,
    DateOnly LedgerDate,
    long? SourceDocumentId,
    IReadOnlyList<LedgerPostingLeg> Legs);

public sealed record LedgerPostingLeg(
    int LedgerTypeId,
    int LedgerSourceId,
    long TransactionDetailId,
    string AccountSystemName,
    int? SubAccountReferenceType,
    long? SubAccountReferenceId,
    decimal DebitAmount,
    decimal CreditAmount,
    string? TransactionDesc);

/// <summary>
/// What came back. The distinction that matters is <see cref="Retry"/> versus
/// <see cref="Refused"/>: one leaves the posting queued, the other counts an
/// attempt against it, and treating a network blip as a refusal would park good
/// work as failed.
/// </summary>
public enum LedgerPostOutcome
{
    Posted = 0,

    /// <summary>Transient — unreachable, timed out, or Accounting said 503.</summary>
    Retry = 1,

    /// <summary>Accounting refused it. Retrying unchanged will be refused again.</summary>
    Refused = 2,
}

public sealed class AccountingLedger : IAccountingLedger
{
    private readonly HttpClient _http;
    private readonly ILogger<AccountingLedger> _log;

    public AccountingLedger(HttpClient http, ILogger<AccountingLedger> log)
    {
        _http = http;
        _log = log;
    }

    public async Task<LedgerPostOutcome> PostAsync(LedgerPosting posting, CancellationToken ct)
    {
        try
        {
            HttpResponseMessage response = await _http.PostAsJsonAsync(
                "internal/ledger/postings", posting, ct);

            if (response.IsSuccessStatusCode)
            {
                return LedgerPostOutcome.Posted;
            }

            // 409 is a chart of accounts that has not been seeded far enough,
            // and 503 is the base currency being briefly unreadable. Both are
            // fixed by something other than this movement changing, so both are
            // worth coming back to rather than parking.
            if (response.StatusCode is HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.Conflict
                or HttpStatusCode.RequestTimeout
                or HttpStatusCode.TooManyRequests
                || (int)response.StatusCode >= 500)
            {
                _log.LogWarning(
                    "Ledger posting for {Code}-{TransactionId} returned {Status}; will retry",
                    posting.TransactionTypeCode,
                    posting.TransactionId,
                    (int)response.StatusCode);

                return LedgerPostOutcome.Retry;
            }

            _log.LogError(
                "Ledger posting for {Code}-{TransactionId} was refused with {Status}: {Body}",
                posting.TransactionTypeCode,
                posting.TransactionId,
                (int)response.StatusCode,
                await response.Content.ReadAsStringAsync(ct));

            return LedgerPostOutcome.Refused;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Safe to retry however far the first attempt got: Accounting
            // replaces a posting rather than appending one, so a call that
            // succeeded and lost its response costs nothing to repeat.
            _log.LogWarning(
                ex,
                "Ledger posting for {Code}-{TransactionId} could not be delivered",
                posting.TransactionTypeCode,
                posting.TransactionId);

            return LedgerPostOutcome.Retry;
        }
    }
}
