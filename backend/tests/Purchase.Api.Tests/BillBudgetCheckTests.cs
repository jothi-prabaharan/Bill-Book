using Purchase.Repository.Clients;
using Purchase.Repository.Repositories;
using Purchase.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Budgets;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Purchase.Api.Tests;

/// <summary>
/// Verifies that bill budget warnings are gated by the Budget add-on (D-36, TK-128).
/// </summary>
public sealed class BillBudgetCheckTests
{
    private sealed class RecordingBudgetCheckClient : IBudgetCheckClient
    {
        public bool Called { get; private set; }

        public Task<List<string>> WarningsAsync(
            Guid customerId, Guid orgId, DateOnly documentDate, IReadOnlyCollection<BudgetSpendLine> lines, CancellationToken ct)
        {
            Called = true;
            return Task.FromResult<List<string>>(["Budget exceeded warning"]);
        }
    }

    [Fact]
    public async Task WarningsAsync_returns_empty_when_budget_addon_is_disabled()
    {
        var tenant = new TenantContext
        {
            CustomerId = Guid.NewGuid(),
            OrgId = Guid.NewGuid(),
            AddOns = AddOn.None,
        };
        var client = new RecordingBudgetCheckClient();
        // Since budget add-on check happens before DB query, a null db is never accessed when disabled.
        var repo = new BillBudgetCheckRepository(null!, client, tenant);

        List<string> warnings = await repo.WarningsAsync(123, CancellationToken.None);

        Assert.Empty(warnings);
        Assert.False(client.Called);
    }
}
