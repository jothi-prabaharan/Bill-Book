using System.Net.Http.Json;

namespace Contacts.Api.Services;

/// <summary>
/// Creates a contact's six sub-accounts in Accounting — the trade balance, a
/// prepayment advance and an overpayment advance, beneath each of Accounts
/// Receivable and Accounts Payable.
/// Only Accounting writes ledger rows, so this is a call rather than an insert.
/// </summary>
public interface IAccountingSubAccounts
{
    /// <summary>True when the sub-ledger was provisioned. False leaves the contact without one.</summary>
    Task<bool> ProvisionForContactAsync(long contactId, string displayName, CancellationToken ct);
}

public sealed class AccountingSubAccounts : IAccountingSubAccounts
{
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _accessor;
    private readonly ILogger<AccountingSubAccounts> _log;

    public AccountingSubAccounts(
        HttpClient http, IHttpContextAccessor accessor, ILogger<AccountingSubAccounts> log)
    {
        _http = http;
        _accessor = accessor;
        _log = log;
    }

    public async Task<bool> ProvisionForContactAsync(
        long contactId, string displayName, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "internal/sub-accounts/provision")
        {
            Content = JsonContent.Create(new
            {
                referenceType = "Contact",
                referenceId = contactId,
                name = displayName,
            }),
        };

        // Forwarded so Accounting resolves the same customer and organization.
        // Without it the call lands with no tenant context and writes nowhere.
        string? authorization = _accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authorization))
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        try
        {
            HttpResponseMessage response = await _http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            // 409 means the chart of accounts is missing a control account —
            // the contact is saved but its sub-ledger is incomplete, and the
            // caller has to be told rather than shown a bare success.
            _log.LogWarning(
                "Sub-account provisioning for contact {ContactId} returned {Status}.",
                contactId,
                (int)response.StatusCode);
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Idempotent on Accounting's side, so a later retry is safe.
            _log.LogWarning(ex, "Sub-account provisioning for contact {ContactId} failed.", contactId);
            return false;
        }
    }
}
