using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Licensing;

/// <summary>What the branch's plan allows a month, as the licence states it (TK-209, TK-210).</summary>
/// <param name="Plan">Trial, Professional or Elite, as a person reads it.</param>
/// <param name="MonthlySalesTransactions">Sales documents a month; null is no limit.</param>
/// <param name="MonthlyPurchaseTransactions">Purchase documents a month; null is no limit.</param>
public sealed record PlanLimits(string Plan, int? MonthlySalesTransactions, int? MonthlyPurchaseTransactions)
{
    /// <summary>The monthly limit of one kind of document, or null when it has none.</summary>
    public int? MonthlyFor(PlanLimitKind kind) => kind switch
    {
        PlanLimitKind.SalesTransactions => MonthlySalesTransactions,
        PlanLimitKind.PurchaseTransactions => MonthlyPurchaseTransactions,
        _ => null,
    };
}

/// <summary>
/// The limits of the current branch's plan.
///
/// Read from Master, which owns the licence. A service cannot read
/// <c>mst.Licenses</c>, and the limits are not carried in the token: a claim
/// would not reach a user until their next refresh, and an operator switching a
/// plan wants it to take effect at once.
/// </summary>
public interface IPlanLimitProvider
{
    /// <summary>
    /// The plan's limits, or <c>null</c> when they could not be established.
    /// Null is never read as a limit: the caller counts nothing and refuses
    /// nothing, because failing to reach Master must not stop a branch trading.
    /// </summary>
    Task<PlanLimits?> GetAsync(CancellationToken ct = default);
}

/// <summary>
/// Reads the limits from Master's org context, cached per branch for one minute
/// so a plan an operator has just switched is in force within the minute, and a
/// busy branch costs one call a minute rather than one a document.
/// </summary>
public sealed class HttpPlanLimitProvider : IPlanLimitProvider
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ITenantContext _tenant;
    private readonly ILogger<HttpPlanLimitProvider> _log;

    public HttpPlanLimitProvider(
        HttpClient http,
        IMemoryCache cache,
        ITenantContext tenant,
        ILogger<HttpPlanLimitProvider> log)
    {
        _http = http;
        _cache = cache;
        _tenant = tenant;
        _log = log;
    }

    public async Task<PlanLimits?> GetAsync(CancellationToken ct = default)
    {
        if (_tenant.OrgId is not Guid orgId)
        {
            return null;
        }

        string key = $"plan-limits:{orgId}";

        if (_cache.TryGetValue(key, out PlanLimits? cached))
        {
            return cached;
        }

        try
        {
            OrgContextDto? context = await _http.GetFromJsonAsync<OrgContextDto>(
                $"internal/orgs/{orgId}/context", ct);
            if (context is null)
            {
                return null;
            }

            var limits = new PlanLimits(
                context.PlanName ?? "Professional",
                context.MonthlySalesTransactionLimit,
                context.MonthlyPurchaseTransactionLimit);

            // Only an answer is cached. A failure is tried again on the next
            // document, which costs one call and nothing else.
            _cache.Set(key, limits, Lifetime);
            return limits;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not read the plan limits for organization {OrgId}", orgId);
            return null;
        }
    }

    /// <summary>Only the fields this reads.</summary>
    private sealed record OrgContextDto(
        string? PlanName, int? MonthlySalesTransactionLimit, int? MonthlyPurchaseTransactionLimit);
}
