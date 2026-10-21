using Accounting.Entity.Enums;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Accounting.Repository.Repositories;
using Accounting.Repository.SeedData;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// Rates written with no group id, which is what Master's startup seed left
/// behind: every rate shared group 0, so a purchase line offered one rate, and
/// no GST sub-account existed to post to. Accounting's own seed repairs them.
///
/// Written with the fix and not run by the AI that wrote it (docs/TASKS.md 0.5).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class TaxMasterRepairTests
{
    private readonly PostgresFixture _postgres;

    public TaxMasterRepairTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task The_seed_gives_ungrouped_rates_their_own_group_and_their_sub_accounts()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        await using AccountingDbContext db = _postgres.CreateContext(customerId, orgId);

        // As Master wrote them: the chart and the rates, group ids left at 0.
        db.Accounts.AddRange(ChartOfAccountsSeed.Build(orgId));
        db.TaxMasters.AddRange(TaxMasterSeed.Build(orgId, new DateOnly(2026, 4, 1)));
        await db.SaveChangesAsync();
        Assert.All(await db.TaxMasters.ToListAsync(), t => Assert.Equal(0, t.TaxGroupId));

        var taxes = new TaxMasterRepository(db, new SubAccountRepository(db), TimeProvider.System);

        // Nothing is missing by name, so nothing new is added — but every row is repaired.
        Assert.Equal(0, await taxes.SeedForOrganizationAsync(orgId, default));

        db.ChangeTracker.Clear();
        List<TaxMaster> rates = await db.TaxMasters.ToListAsync();
        Assert.All(rates, t => Assert.Equal(t.TaxMasterId, t.TaxGroupId));

        // Each rate has its GST sub-accounts, keyed on its own group.
        foreach (TaxMaster rate in rates)
        {
            Assert.True(
                await db.SubAccounts.AnyAsync(s => s.ReferenceType == SubAccountReferenceType.Tax && s.ReferenceId == rate.TaxGroupId),
                $"{rate.TaxName} has no GST sub-account.");
        }

        // A second run changes nothing.
        int before = await db.SubAccounts.CountAsync();
        await taxes.SeedForOrganizationAsync(orgId, default);
        Assert.Equal(before, await db.SubAccounts.CountAsync());
    }
}
