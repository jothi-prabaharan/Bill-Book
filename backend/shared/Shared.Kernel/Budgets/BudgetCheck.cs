using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Shared.Kernel.Budgets;

/// <summary>One line of a document being saved, as the over-budget check reads it (TK-109).</summary>
public sealed class BudgetSpendLine
{
    public long AccountId { get; set; }

    public long? ProjectId { get; set; }

    /// <summary>Base currency, positive when it adds to what the account has spent.</summary>
    public decimal Amount { get; set; }
}

public sealed class BudgetCheckRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    /// <summary>The document's date, which picks the budget's month and year.</summary>
    public DateOnly Date { get; set; }

    public List<BudgetSpendLine> Lines { get; set; } = [];
}

public sealed class BudgetCheckResponse
{
    public List<string> Warnings { get; set; } = [];
}

/// <summary>
/// Asks Accounting whether a document takes an expense account past the
/// branch's approved budget. Advisory only: a warning never blocks a save, so
/// a failed check answers with no warnings rather than an error.
/// </summary>
public interface IBudgetCheckClient
{
    Task<List<string>> WarningsAsync(Guid customerId, Guid orgId, DateOnly date, IReadOnlyCollection<BudgetSpendLine> lines, CancellationToken ct);
}

public sealed class HttpBudgetCheckClient : IBudgetCheckClient
{
    private readonly HttpClient _accounting;
    private readonly ILogger<HttpBudgetCheckClient> _log;

    public HttpBudgetCheckClient(HttpClient accounting, ILogger<HttpBudgetCheckClient> log)
    {
        _accounting = accounting;
        _log = log;
    }

    public async Task<List<string>> WarningsAsync(
        Guid customerId, Guid orgId, DateOnly date, IReadOnlyCollection<BudgetSpendLine> lines, CancellationToken ct)
    {
        if (lines.Count == 0)
        {
            return [];
        }

        try
        {
            using HttpResponseMessage response = await _accounting.PostAsJsonAsync(
                "internal/budgets/check",
                new BudgetCheckRequest { CustomerId = customerId, OrgId = orgId, Date = date, Lines = [.. lines] },
                ct);

            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("The budget check answered {Status}; saving without warnings.", (int)response.StatusCode);
                return [];
            }

            return (await response.Content.ReadFromJsonAsync<BudgetCheckResponse>(ct))?.Warnings ?? [];
        }
        catch (HttpRequestException ex)
        {
            _log.LogWarning(ex, "The budget check could not be reached; saving without warnings.");
            return [];
        }
    }
}
