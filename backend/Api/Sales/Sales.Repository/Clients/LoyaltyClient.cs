using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Tenancy;

namespace Sales.Repository.Clients;

/// <summary>What Customer recorded: the points moved and the balance after, or nothing when it was not reached.</summary>
public sealed record LoyaltyPosting(int Points, int Balance, bool AlreadyRecorded);

/// <summary>
/// Loyalty points in Customer (TK-166), over its internal API rather than its
/// database (rule 8). Both calls are safe to repeat: Customer records an
/// invoice's earning once and its reversal once.
/// </summary>
public interface ILoyaltyClient
{
    /// <summary>Null when Customer could not be reached; the bill stands either way.</summary>
    Task<LoyaltyPosting?> EarnAsync(long contactId, long invoiceId, string? documentNo, int points, CancellationToken ct);

    /// <summary>Null when Customer could not be reached; a posting of 0 points when the invoice had earned nothing.</summary>
    Task<LoyaltyPosting?> ReverseAsync(long invoiceId, CancellationToken ct);
}

public sealed class LoyaltyClient : ILoyaltyClient
{
    private readonly HttpClient _http;
    private readonly ITenantContext _tenant;
    private readonly ILogger<LoyaltyClient> _log;

    public LoyaltyClient(HttpClient http, ITenantContext tenant, ILogger<LoyaltyClient> log)
    {
        _http = http;
        _tenant = tenant;
        _log = log;
    }

    public async Task<LoyaltyPosting?> EarnAsync(long contactId, long invoiceId, string? documentNo, int points, CancellationToken ct)
    {
        (Guid customerId, Guid orgId) = _tenant.Require();
        try
        {
            HttpResponseMessage response = await _http.PostAsJsonAsync(
                "internal/loyalty/earn", new { customerId, orgId, contactId, invoiceId, documentNo, points }, ct);
            response.EnsureSuccessStatusCode();
            Answer? answer = await response.Content.ReadFromJsonAsync<Answer>(ct);
            return answer is null ? null : new LoyaltyPosting(answer.Points, answer.Balance, answer.AlreadyRecorded);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Recording invoice {InvoiceId}'s loyalty points failed for organization {OrgId}", invoiceId, orgId);
            return null;
        }
    }

    public async Task<LoyaltyPosting?> ReverseAsync(long invoiceId, CancellationToken ct)
    {
        (Guid customerId, Guid orgId) = _tenant.Require();
        try
        {
            HttpResponseMessage response = await _http.PostAsJsonAsync("internal/loyalty/reverse", new { customerId, orgId, invoiceId }, ct);
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return new LoyaltyPosting(0, 0, false);
            }

            response.EnsureSuccessStatusCode();
            Answer? answer = await response.Content.ReadFromJsonAsync<Answer>(ct);
            return answer is null ? null : new LoyaltyPosting(answer.Points, answer.Balance, answer.AlreadyRecorded);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Taking back invoice {InvoiceId}'s loyalty points failed for organization {OrgId}", invoiceId, orgId);
            return null;
        }
    }

    private sealed class Answer
    {
        public int Points { get; set; }

        public int Balance { get; set; }

        public bool AlreadyRecorded { get; set; }
    }
}
