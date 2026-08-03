using Accounting.Api.Services;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// Sub-account provisioning, against a real database — because what is being
/// proved is mostly the unique key, and a key is a property of Postgres.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SubAccountServiceTests
{
    private const int Asset = 1;
    private const int Liability = 2;

    private readonly PostgresFixture _postgres;

    public SubAccountServiceTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>
    /// Six sub-accounts under two parents: the trade balance, a prepayment
    /// advance and an overpayment advance beneath each of Accounts Receivable
    /// and Accounts Payable.
    ///
    /// Before the purpose column existed all three under a parent shared one
    /// key, so only the first would ever have been written — the other two would
    /// have been silently skipped as "already there".
    /// </summary>
    [SkippableFact]
    public async Task A_contact_gets_six_sub_accounts_under_two_parents()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        ProvisionSubAccountsResult result = await h.SubAccounts.ProvisionAsync(
            Contact(1, "Sharma Traders"), ct);

        Assert.Equal(6, result.Created);
        Assert.Empty(result.MissingAccounts);

        List<SubAccount> rows = await h.Db.SubAccounts
            .Where(s => s.ReferenceType == SubAccountReferenceType.Contact && s.ReferenceId == 1)
            .ToListAsync(ct);

        Assert.Equal(6, rows.Count);
        Assert.Equal(2, rows.Select(r => r.AccountId).Distinct().Count());

        // Three under each parent, one per purpose.
        foreach (long accountId in new[] { h.ReceivableId, h.PayableId })
        {
            List<SubAccountPurpose> purposes = [.. rows
                .Where(r => r.AccountId == accountId)
                .Select(r => r.Purpose)
                .Order()];

            Assert.Equal(
                [
                    SubAccountPurpose.Primary,
                    SubAccountPurpose.PrepaymentAdvance,
                    SubAccountPurpose.OverpaymentAdvance,
                ],
                purposes);
        }
    }

    /// <summary>
    /// The type is copied from the parent, so everything under Accounts
    /// Receivable is an asset and everything under Accounts Payable a liability.
    /// Grouped any other way, a report by account type would contradict the same
    /// report by account.
    /// </summary>
    [SkippableFact]
    public async Task Every_sub_account_takes_its_parents_type()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await h.SubAccounts.ProvisionAsync(Contact(2, "Sharma Traders"), ct);

        List<SubAccount> rows = await h.Db.SubAccounts
            .Where(s => s.ReferenceId == 2)
            .ToListAsync(ct);

        Assert.All(
            rows.Where(r => r.AccountId == h.ReceivableId),
            r => Assert.Equal(Asset, r.AccountTypeId));

        Assert.All(
            rows.Where(r => r.AccountId == h.PayableId),
            r => Assert.Equal(Liability, r.AccountTypeId));
    }

    /// <summary>
    /// Idempotent per target, not just per call. A contact provisioned before
    /// the advances existed gains exactly the four it is missing and keeps the
    /// two it had.
    /// </summary>
    [SkippableFact]
    public async Task Re_provisioning_creates_only_what_is_missing()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await h.SubAccounts.ProvisionAsync(Contact(3, "Sharma Traders"), ct);

        // The two a contact would have had before the advances were added.
        await h.Db.SubAccounts
            .Where(s => s.ReferenceId == 3 && s.Purpose != SubAccountPurpose.Primary)
            .ExecuteDeleteAsync(ct);

        ProvisionSubAccountsResult again = await h.SubAccounts.ProvisionAsync(
            Contact(3, "Sharma Traders"), ct);

        Assert.Equal(4, again.Created);
        Assert.Equal(6, await h.Db.SubAccounts.CountAsync(s => s.ReferenceId == 3, ct));

        // And a third run does nothing at all.
        Assert.Equal(0, (await h.SubAccounts.ProvisionAsync(Contact(3, "Sharma Traders"), ct)).Created);
    }

    /// <summary>
    /// Names have to be distinguishable on a picker: three balances under one
    /// parent for one contact are three different things.
    /// </summary>
    [SkippableFact]
    public async Task The_six_are_named_apart()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await h.SubAccounts.ProvisionAsync(Contact(4, "Sharma Traders"), ct);

        List<string> names = await h.Db.SubAccounts
            .Where(s => s.ReferenceId == 4)
            .Select(s => s.SubAccountName)
            .ToListAsync(ct);

        Assert.Equal(6, names.Distinct().Count());
        Assert.Contains("Accounts Receivable — Sharma Traders", names);
        Assert.Contains("Prepayment Advance Receivable — Sharma Traders", names);
        Assert.Contains("Overpayment Advance Receivable — Sharma Traders", names);
        Assert.Contains("Accounts Payable — Sharma Traders", names);
        Assert.Contains("Prepayment Advance Payable — Sharma Traders", names);
        Assert.Contains("Overpayment Advance Payable — Sharma Traders", names);
    }

    /// <summary>
    /// A missing control account is reported, not swallowed. A contact with no
    /// receivable sub-account would drop out of the aging report without
    /// anything having failed.
    /// </summary>
    [SkippableFact]
    public async Task A_missing_control_account_is_reported()
    {
        await using Harness h = await Harness.CreateAsync(_postgres, seedPayable: false);

        ProvisionSubAccountsResult result = await h.SubAccounts.ProvisionAsync(
            Contact(5, "Sharma Traders"), CancellationToken.None);

        Assert.Equal(3, result.Created);
        Assert.Equal("Accounts Payable", Assert.Single(result.MissingAccounts));
    }

    /// <summary>An item is unaffected — three parents, so no discriminator is needed.</summary>
    [SkippableFact]
    public async Task An_item_still_gets_three()
    {
        await using Harness h = await Harness.CreateAsync(_postgres, seedItemAccounts: true);

        ProvisionSubAccountsResult result = await h.SubAccounts.ProvisionAsync(
            new ProvisionSubAccountsRequest
            {
                ReferenceType = SubAccountReferenceType.Item,
                ReferenceId = 9,
                Name = "Gold Chain 22K",
            },
            CancellationToken.None);

        Assert.Equal(3, result.Created);
    }

    private static ProvisionSubAccountsRequest Contact(long id, string name) => new()
    {
        ReferenceType = SubAccountReferenceType.Contact,
        ReferenceId = id,
        Name = name,
    };

    private sealed class Harness : IAsyncDisposable
    {
        public required AccountingDbContext Db { get; init; }

        public required SubAccountService SubAccounts { get; init; }

        public required long ReceivableId { get; init; }

        public required long PayableId { get; init; }

        public static async Task<Harness> CreateAsync(
            PostgresFixture postgres, bool seedPayable = true, bool seedItemAccounts = false)
        {
            Skip.If(postgres.SkipReason is not null, postgres.SkipReason ?? string.Empty);

            var orgId = Guid.NewGuid();
            AccountingDbContext db = postgres.CreateContext(Guid.NewGuid(), orgId);

            async Task<long> Account(string code, SystemAccount system, int typeId)
            {
                string name = SystemAccountNames.Of(system);
                var account = new Account
                {
                    OrgId = orgId,
                    AccountTypeId = typeId,
                    AccountCode = code,
                    AccountSystemName = name,
                    AccountName = name,
                    IsSystemDefault = true,
                    IsActive = true,
                };

                db.Accounts.Add(account);
                await db.SaveChangesAsync();
                return account.AccountId;
            }

            long receivableId = await Account("1100", SystemAccount.AccountsReceivable, Asset);
            long payableId = seedPayable
                ? await Account("2100", SystemAccount.AccountsPayable, Liability)
                : 0;

            if (seedItemAccounts)
            {
                await Account("1200", SystemAccount.Inventory, Asset);
                await Account("5100", SystemAccount.CostOfGoodsSold, 5);
                await Account("4100", SystemAccount.SalesRevenue, 4);
            }

            return new Harness
            {
                Db = db,
                SubAccounts = new SubAccountService(db),
                ReceivableId = receivableId,
                PayableId = payableId,
            };
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
