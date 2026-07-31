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
