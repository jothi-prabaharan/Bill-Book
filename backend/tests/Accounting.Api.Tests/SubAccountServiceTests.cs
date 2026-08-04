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

    /// <summary>
    /// The list sends the enums by name, and that is a contract, not a detail.
    ///
    /// Nothing in this product configures a string enum converter, so an enum
    /// left as an enum on a response model goes out as an integer. The screens
    /// that read this list — the sub-account list itself, and the money
    /// documents, which pick the account a payment lands on by matching the
    /// purpose — compare against names. A number there matches nothing, and the
    /// failure is silent: a badge that never draws, an account that never
    /// resolves. Hence the projection to strings, and hence this test.
    /// </summary>
    [SkippableFact]
    public async Task The_list_names_the_purpose_rather_than_numbering_it()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await h.SubAccounts.ProvisionAsync(Contact(11, "Sharma Traders"), ct);

        IReadOnlyList<SubAccountListItem> rows = await h.SubAccounts.ListAsync(
            SubAccountReferenceType.Contact, 11, ct);

        Assert.Equal(6, rows.Count);
        Assert.All(rows, r => Assert.Equal("Contact", r.ReferenceType));
        Assert.All(rows, r => Assert.Equal("None", r.TaxComponent));

        Assert.Equal(
            ["OverpaymentAdvance", "PrepaymentAdvance", "Primary"],
            rows.Select(r => r.Purpose).Distinct().Order());
    }

    /// <summary>
    /// The posting door has to name the purpose, because the reference alone is
    /// ambiguous once a contact has three sub-accounts under one parent.
    ///
    /// This is the regression that matters: matching on reference type and id
    /// only would land a supplier deposit on the trade receivable balance —
    /// silently, with no error, and the two would never reconcile again.
    /// </summary>
    [SkippableFact]
    public async Task A_posting_reaches_the_purpose_it_names_and_not_a_sibling()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await h.SubAccounts.ProvisionAsync(Contact(6, "Sharma Traders"), ct);

        var postings = new LedgerPostingService(h.Db, h.Tenant, new StubBaseCurrency());

        // Two legs on one document, both against Accounts Receivable for the
        // same contact, differing only in purpose.
        PostLedgerResult result = await postings.PostAsync(new PostLedgerRequest
        {
            TransactionTypeCode = "SPM",
            TransactionId = 4242,
            LedgerDate = new DateOnly(2026, 8, 1),
            Legs =
            [
                Leg(h.ReceivableId, 6, SubAccountPurpose.PrepaymentAdvance, 1, debit: 600m),
                Leg(h.ReceivableId, 6, SubAccountPurpose.OverpaymentAdvance, 2, debit: 400m),
                Leg(h.ReceivableId, 6, SubAccountPurpose.Primary, 3, credit: 1_000m),
            ],
        }, ct);

        Assert.Equal(PostLedgerOutcome.Ok, result.Outcome);

        List<SubAccount> subs = await h.Db.SubAccounts
            .Where(s => s.ReferenceId == 6 && s.AccountId == h.ReceivableId)
            .ToListAsync(ct);

        var rows = await h.Db.JournalLedger
            .Where(l => l.TransactionTypeCode == "SPM" && l.TransactionId == 4242)
            .ToListAsync(ct);

        // Three legs, three different sub-accounts — not one sub-account three
        // times, which is what the ambiguous lookup produced.
        Assert.Equal(3, rows.Select(r => r.SubAccountId).Distinct().Count());

        long PurposeId(SubAccountPurpose purpose) =>
            subs.Single(s => s.Purpose == purpose).SubAccountId;

        Assert.Equal(
            600m,
            rows.Single(r => r.SubAccountId == PurposeId(SubAccountPurpose.PrepaymentAdvance))
                .DebitAmountBase);

        Assert.Equal(
            400m,
            rows.Single(r => r.SubAccountId == PurposeId(SubAccountPurpose.OverpaymentAdvance))
                .DebitAmountBase);
    }

    /// <summary>A purpose the contact was never provisioned for is refused, not guessed.</summary>
    [SkippableFact]
    public async Task A_purpose_that_was_never_provisioned_is_refused()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await h.SubAccounts.ProvisionAsync(Contact(7, "Sharma Traders"), ct);

        await h.Db.SubAccounts
            .Where(s => s.ReferenceId == 7 && s.Purpose == SubAccountPurpose.OverpaymentAdvance)
            .ExecuteDeleteAsync(ct);

        var postings = new LedgerPostingService(h.Db, h.Tenant, new StubBaseCurrency());

        PostLedgerResult result = await postings.PostAsync(new PostLedgerRequest
        {
            TransactionTypeCode = "SPM",
            TransactionId = 4243,
            LedgerDate = new DateOnly(2026, 8, 1),
            Legs =
            [
                Leg(h.ReceivableId, 7, SubAccountPurpose.OverpaymentAdvance, 1, debit: 100m),
                Leg(h.ReceivableId, 7, SubAccountPurpose.Primary, 2, credit: 100m),
            ],
        }, ct);

        Assert.Equal(PostLedgerOutcome.SubAccountMissing, result.Outcome);
        Assert.Contains("OverpaymentAdvance", result.Detail);
    }

    private static LedgerLegRequest Leg(
        long accountId,
        long contactId,
        SubAccountPurpose purpose,
        long detail,
        decimal debit = 0m,
        decimal credit = 0m) =>
        new()
        {
            LedgerTypeId = 3,
            LedgerSourceId = 1,
            TransactionDetailId = detail,
            AccountId = accountId,
            SubAccountReferenceType = SubAccountReferenceType.Contact,
            SubAccountReferenceId = contactId,
            SubAccountPurpose = purpose,
            DebitAmount = debit,
            CreditAmount = credit,
        };

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

        public required TenantContext Tenant { get; init; }

        public static async Task<Harness> CreateAsync(
            PostgresFixture postgres, bool seedPayable = true, bool seedItemAccounts = false)
        {
            Skip.If(postgres.SkipReason is not null, postgres.SkipReason ?? string.Empty);

            var orgId = Guid.NewGuid();
            var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = orgId };
            AccountingDbContext db = postgres.CreateContext(
                tenant.CustomerId!.Value, tenant.OrgId!.Value);

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
                Tenant = tenant,
                SubAccounts = new SubAccountService(db),
                ReceivableId = receivableId,
                PayableId = payableId,
            };
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
