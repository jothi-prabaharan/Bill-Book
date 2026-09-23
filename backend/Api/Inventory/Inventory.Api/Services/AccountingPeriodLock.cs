using System.Net.Http.Json;

namespace Inventory.Api.Services;

/// <summary>
/// How far back a branch's books are closed, for a caller that is no person.
///
/// Accounting owns period locks and nobody else reads the table, so this is a
/// call rather than a query. The recalculation needs the date because a
/// stock-out on or before it must not be restated: its cost of sales is in a
/// period somebody has closed.
/// </summary>
public interface IAccountingPeriodLock
{
    Task<PeriodLockLookup> LockedUptoAsync(Guid customerId, Guid orgId, CancellationToken ct);
}

/// <summary>
/// The answer, or the fact that there was none. The distinction matters:
/// "no lock" lets every stock-out be revalued, while "could not ask" must
/// revalue nothing — guessing open would restate a closed period.
/// </summary>
/// <param name="Known">False when Accounting could not be asked.</param>
/// <param name="LockedUpto">Inclusive. Null when the branch has closed no period.</param>
public sealed record PeriodLockLookup(bool Known, DateOnly? LockedUpto)
{
    public static PeriodLockLookup Unknown { get; } = new(false, null);
}

public sealed class AccountingPeriodLock : IAccountingPeriodLock
{
    private readonly HttpClient _http;
    private readonly ILogger<AccountingPeriodLock> _log;

    public AccountingPeriodLock(HttpClient http, ILogger<AccountingPeriodLock> log)
    {
        _http = http;
        _log = log;
    }

    public async Task<PeriodLockLookup> LockedUptoAsync(
        Guid customerId, Guid orgId, CancellationToken ct)
    {
        try
        {
            BranchPeriodLock? response = await _http.GetFromJsonAsync<BranchPeriodLock>(
                $"internal/period-locks/branch?customerId={customerId}&orgId={orgId}", ct);

            return response is null
                ? PeriodLockLookup.Unknown
                : new PeriodLockLookup(true, response.LockedUpto);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
            or System.Text.Json.JsonException)
        {
            // Not a costing failure. The work stays queued and the next tick
            // asks again.
            _log.LogWarning(
                ex, "Could not read the period lock for organization {OrgId}", orgId);

            return PeriodLockLookup.Unknown;
        }
    }

    private sealed record BranchPeriodLock(DateOnly? LockedUpto);
}
