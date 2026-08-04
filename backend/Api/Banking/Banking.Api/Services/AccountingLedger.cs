using System.Net;
using System.Net.Http.Json;

namespace Banking.Api.Services;

/// <summary>
/// Creates and maintains the GL account behind a bank account. Only Accounting
/// writes ledger rows, so this is a call rather than an insert.
/// </summary>
public interface IAccountingLedger
{
    /// <summary>The GL account id, or null when Accounting could not answer.</summary>
    Task<LedgerAccount?> ProvisionAsync(
        long bankAccountId, string accountName, string accountType, string? currencyCode,
        CancellationToken ct);

    /// <summary>Pushes a rename or a deactivation. Banking owns the name.</summary>
    Task<bool> UpdateAsync(long bankAccountId, string accountName, bool isActive, CancellationToken ct);

    /// <summary>
    /// Writes a money document's legs to the general ledger. Only Accounting
    /// inserts ledger rows, so this describes a posting rather than making one.
    /// </summary>
    Task<LedgerPostOutcome> PostAsync(LedgerPosting posting, CancellationToken ct);

    /// <summary>
    /// How far back the books are closed to this caller, or null when nothing
    /// stops them. Read before posting, so a document dated into a closed period
    /// is refused with the date rather than by a constraint.
    /// </summary>
    Task<DateOnly?> LockedUptoAsync(CancellationToken ct);
}

/// <summary>A money document's posting, as Banking describes it.</summary>
public sealed record LedgerPosting(
    string TransactionTypeCode,
    long TransactionId,
    DateOnly LedgerDate,
    string CurrencyCode,
    decimal ExchangeRate,
    long? ContactId,
    IReadOnlyList<LedgerPostingLeg> Legs,

    /// <summary>
    /// Which leg types to clear when <paramref name="Legs"/> is empty. That is
    /// how a posting is withdrawn: with no legs there is nothing to infer the
    /// types from, so a void names them.
    /// </summary>
    IReadOnlyList<int>? WithdrawLedgerTypeIds = null);

/// <summary>
/// One leg. The account is named by system name, except the bank leg which
/// carries the id Accounting itself issued when the bank account was created.
/// </summary>
public sealed record LedgerPostingLeg(
    int LedgerTypeId,
    int LedgerSourceId,
    long TransactionDetailId,
    string? AccountSystemName,
    long? AccountId,
    int? SubAccountReferenceType,
    long? SubAccountReferenceId,
    int SubAccountPurpose,
    decimal DebitAmount,
    decimal CreditAmount,
    string? TransactionDesc);

/// <summary>What came back from a posting attempt.</summary>
public enum LedgerPostOutcome
{
    Posted = 0,

    /// <summary>Transient — unreachable, timed out, or Accounting said 503.</summary>
    Retry = 1,

    /// <summary>Accounting refused it. Retrying unchanged will be refused again.</summary>
    Refused = 2,
}

public sealed record LedgerAccount(long AccountId, string AccountCode);

public sealed class AccountingLedger : IAccountingLedger
{
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _accessor;
    private readonly ILogger<AccountingLedger> _log;

    public AccountingLedger(
        HttpClient http, IHttpContextAccessor accessor, ILogger<AccountingLedger> log)
    {
        _http = http;
        _accessor = accessor;
        _log = log;
    }

    public async Task<LedgerAccount?> ProvisionAsync(
        long bankAccountId,
        string accountName,
        string accountType,
        string? currencyCode,
        CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "internal/accounts/bank-account")
        {
            Content = JsonContent.Create(new
            {
                bankAccountId,
                accountName,
                accountType,
                currencyCode,
            }),
        };

        Forward(request);

        try
        {
            HttpResponseMessage response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning(
                    "Ledger account for bank account {BankAccountId} returned {Status}.",
                    bankAccountId,
                    (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<LedgerAccount>(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Idempotent on Accounting's side, so retrying is safe.
            _log.LogWarning(ex, "Ledger account for bank account {BankAccountId} failed.", bankAccountId);
            return null;
        }
    }

    public async Task<bool> UpdateAsync(
        long bankAccountId, string accountName, bool isActive, CancellationToken ct)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Put, $"internal/accounts/bank-account/{bankAccountId}")
        {
            Content = JsonContent.Create(new { accountName, isActive }),
        };

        Forward(request);

        try
        {
            HttpResponseMessage response = await _http.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogWarning(ex, "Ledger update for bank account {BankAccountId} failed.", bankAccountId);
            return false;
        }
    }

    public async Task<LedgerPostOutcome> PostAsync(LedgerPosting posting, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "internal/ledger/postings")
        {
            Content = JsonContent.Create(new
            {
                transactionTypeCode = posting.TransactionTypeCode,
                transactionId = posting.TransactionId,
                ledgerDate = posting.LedgerDate,
                currencyCode = posting.CurrencyCode,
                exchangeRate = posting.ExchangeRate,
                contactId = posting.ContactId,
                sourceDocumentId = posting.TransactionId,
                withdrawLedgerTypeIds = posting.WithdrawLedgerTypeIds ?? [],
                legs = posting.Legs.Select(l => new
                {
                    ledgerTypeId = l.LedgerTypeId,
                    ledgerSourceId = l.LedgerSourceId,
                    transactionDetailId = l.TransactionDetailId,
                    accountSystemName = l.AccountSystemName,
                    accountId = l.AccountId,
                    subAccountReferenceType = l.SubAccountReferenceType,
                    subAccountReferenceId = l.SubAccountReferenceId,
                    subAccountPurpose = l.SubAccountPurpose,
                    debitAmount = l.DebitAmount,
                    creditAmount = l.CreditAmount,
                    transactionDesc = l.TransactionDesc,
                }),
            }),
        };

        Forward(request);

        try
        {
            HttpResponseMessage response = await _http.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                return LedgerPostOutcome.Posted;
            }

            // 409 is a chart of accounts not seeded far enough and 503 is the
            // base currency being briefly unreadable. Both are fixed by
            // something other than this document changing, so both are worth
            // coming back to rather than parking.
            if (response.StatusCode is HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.Conflict
                or HttpStatusCode.RequestTimeout
                || (int)response.StatusCode >= 500)
            {
                _log.LogWarning(
                    "Posting {Code}-{Id} returned {Status}; will retry",
                    posting.TransactionTypeCode,
                    posting.TransactionId,
                    (int)response.StatusCode);

                return LedgerPostOutcome.Retry;
            }

            _log.LogError(
                "Posting {Code}-{Id} was refused with {Status}: {Body}",
                posting.TransactionTypeCode,
                posting.TransactionId,
                (int)response.StatusCode,
                await response.Content.ReadAsStringAsync(ct));

            return LedgerPostOutcome.Refused;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Safe to retry however far the first attempt got: Accounting
            // replaces a posting by key rather than appending one.
            _log.LogWarning(
                ex, "Posting {Code}-{Id} could not be delivered",
                posting.TransactionTypeCode, posting.TransactionId);

            return LedgerPostOutcome.Retry;
        }
    }

    public async Task<DateOnly?> LockedUptoAsync(CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "api/period-locks/mine");
        Forward(request);

        try
        {
            HttpResponseMessage response = await _http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                // Unreadable is not unlocked. A lock that failed open would let a
                // document into a closed period because a lookup blipped, so the
                // caller treats null-with-a-warning as "could not confirm" and
                // refuses. See the callers.
                _log.LogWarning(
                    "Period lock lookup returned {Status}.", (int)response.StatusCode);

                throw new PeriodLockUnavailableException();
            }

            PeriodLockStatusDto? status =
                await response.Content.ReadFromJsonAsync<PeriodLockStatusDto>(ct);

            return status?.LockedUpto;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogWarning(ex, "Period lock lookup could not be delivered.");
            throw new PeriodLockUnavailableException();
        }
    }

    private sealed record PeriodLockStatusDto(DateOnly? LockedUpto, DateOnly? OpenFrom);

    /// <summary>
    /// Forwards the caller's token so Accounting resolves the same customer and
    /// organization. Without it the call lands with no tenant context.
    /// </summary>
    private void Forward(HttpRequestMessage request)
    {
        string? authorization = _accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authorization))
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }
    }
}

/// <summary>
/// Accounting could not say whether the books are closed. Thrown rather than
/// answered null, because "no lock" and "could not ask" must not look the same to
/// a caller about to write to the general ledger.
/// </summary>
public sealed class PeriodLockUnavailableException : Exception
{
    public PeriodLockUnavailableException()
        : base("Whether the books are closed could not be established.")
    {
    }
}
