using Accounting.Api.Services;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// Closing the books, one date per branch per role.
///
/// The point of the per-role part is the soft close: the month shuts to whoever
/// keys documents while staying open to whoever has to make the adjusting
/// entries the close itself produced. Every test here is really about that
/// asymmetry.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class PeriodLockTests
{
    private const int Clerk = 4;
    private const int Accountant = 3;

    private readonly PostgresFixture _postgres;

    public PeriodLockTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>A branch that has never closed a period stops nobody.</summary>
    [SkippableFact]
    public async Task With_no_locks_configured_nothing_is_refused()
    {
        await using Harness h = await Harness.CreateAsync(_postgres, Clerk);

        Assert.Null(await h.Locks.LockedUptoAsync(CancellationToken.None));
        Assert.Null(await h.Locks.RefusedByAsync(new DateOnly(2020, 1, 1), CancellationToken.None));
    }

    /// <summary>The date is inclusive — locked to 31 March closes 31 March itself.</summary>
    [SkippableFact]
    public async Task The_locked_date_itself_is_closed()
    {
        await using Harness h = await Harness.CreateAsync(_postgres, Clerk);
        CancellationToken ct = CancellationToken.None;

        await h.Lock(Clerk, new DateOnly(2026, 3, 31), ct);

        Assert.NotNull(await h.Locks.RefusedByAsync(new DateOnly(2026, 3, 31), ct));
        Assert.NotNull(await h.Locks.RefusedByAsync(new DateOnly(2026, 3, 30), ct));
        Assert.Null(await h.Locks.RefusedByAsync(new DateOnly(2026, 4, 1), ct));
    }

    /// <summary>
    /// The whole reason the lock is per role. The same date, the same branch, two
    /// roles — one shut out, one still working.
    /// </summary>
    [SkippableFact]
    public async Task One_role_can_be_closed_while_another_stays_open()
    {
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        await using Harness clerk = await Harness.CreateAsync(_postgres, (int?)Clerk, orgId, customerId);
        CancellationToken ct = CancellationToken.None;

        await clerk.Lock(Clerk, new DateOnly(2026, 3, 31), ct);
        await clerk.Lock(Accountant, new DateOnly(2026, 1, 31), ct);

        // 15 March: closed to the clerk, open to the accountant who is still
        // finishing the March close.
        var march = new DateOnly(2026, 3, 15);

        Assert.NotNull(await clerk.Locks.RefusedByAsync(march, ct));

        await using Harness accountant =
            await Harness.CreateAsync(_postgres, (int?)Accountant, orgId, customerId);

        Assert.Null(await accountant.Locks.RefusedByAsync(march, ct));
    }

    /// <summary>
    /// A caller we cannot identify gets the branch's <b>strictest</b> lock, not
    /// none. Falling open would let a token minted before the role claim existed
    /// post into a closed period for as long as it lives.
    /// </summary>
    [SkippableFact]
    public async Task A_caller_with_no_role_gets_the_strictest_lock()
    {
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        await using Harness known = await Harness.CreateAsync(_postgres, (int?)Clerk, orgId, customerId);
        CancellationToken ct = CancellationToken.None;

        await known.Lock(Accountant, new DateOnly(2026, 1, 31), ct);
        await known.Lock(Clerk, new DateOnly(2026, 3, 31), ct);

        await using Harness anonymous =
            await Harness.CreateAsync(_postgres, null, orgId, customerId);

        // The strictest of the two, not the loosest and not nothing.
        Assert.Equal(new DateOnly(2026, 3, 31), await anonymous.Locks.LockedUptoAsync(ct));
    }

    /// <summary>One row per role — setting the same role again moves the date.</summary>
    [SkippableFact]
    public async Task Setting_a_role_twice_moves_the_date_rather_than_adding_a_row()
    {
        await using Harness h = await Harness.CreateAsync(_postgres, Clerk);
        CancellationToken ct = CancellationToken.None;

        await h.Lock(Clerk, new DateOnly(2026, 1, 31), ct);
        await h.Lock(Clerk, new DateOnly(2026, 2, 28), ct);

        PeriodLockListItem row = Assert.Single(await h.Locks.ListAsync(ct));
        Assert.Equal(new DateOnly(2026, 2, 28), row.LockedUpto);
    }

    /// <summary>A close made in error has to be undoable.</summary>
    [SkippableFact]
    public async Task Clearing_a_lock_reopens_every_period_for_that_role()
    {
        await using Harness h = await Harness.CreateAsync(_postgres, Clerk);
        CancellationToken ct = CancellationToken.None;

        await h.Lock(Clerk, new DateOnly(2026, 3, 31), ct);
        Assert.True(await h.Locks.ClearAsync(Clerk, ct));

        Assert.Null(await h.Locks.RefusedByAsync(new DateOnly(2020, 1, 1), ct));
    }

    /// <summary>
    /// The end of it: a journal dated inside a closed period cannot be posted,
    /// and the refusal names the date rather than merely failing.
    /// </summary>
    [SkippableFact]
    public async Task A_journal_in_a_closed_period_cannot_be_posted()
    {
        await using Harness h = await Harness.CreateAsync(_postgres, Clerk);
        CancellationToken ct = CancellationToken.None;

        await h.Lock(Clerk, new DateOnly(2026, 8, 31), ct);

        SaveJournalResult draft = await h.Journals.CreateAsync(h.Entry(500m), ct);
        SaveJournalResult posted = await h.Journals.PostAsync(draft.JournalId, ct);

        Assert.Equal(SaveJournalOutcome.PeriodClosed, posted.Outcome);
        Assert.Contains("31 Aug 2026", posted.Detail);

        // Nothing moved: still a draft, still no number, no ledger rows.
        JournalDetailView? after = await h.Journals.GetAsync(draft.JournalId, ct);
        Assert.Equal("Draft", after!.Status);
        Assert.Null(after.JournalNo);
        Assert.Empty(await h.Db.JournalLedger.Where(l => l.JournalId == draft.JournalId).ToListAsync(ct));
    }

    /// <summary>
    /// A draft is free. Someone catching up on last month's paperwork can still
    /// key it — they simply cannot post it into the closed month.
    /// </summary>
    [SkippableFact]
    public async Task A_draft_in_a_closed_period_still_saves()
    {
        await using Harness h = await Harness.CreateAsync(_postgres, Clerk);
        CancellationToken ct = CancellationToken.None;

        await h.Lock(Clerk, new DateOnly(2026, 8, 31), ct);

        SaveJournalResult draft = await h.Journals.CreateAsync(h.Entry(500m), ct);

        Assert.Equal(SaveJournalOutcome.Ok, draft.Outcome);
    }

    /// <summary>And the same entry posts once its date is outside the lock.</summary>
    [SkippableFact]
    public async Task The_same_entry_posts_on_an_open_date()
    {
        await using Harness h = await Harness.CreateAsync(_postgres, Clerk);
        CancellationToken ct = CancellationToken.None;

        await h.Lock(Clerk, new DateOnly(2026, 7, 31), ct);

        SaveJournalResult draft = await h.Journals.CreateAsync(h.Entry(500m), ct);

        Assert.Equal(SaveJournalOutcome.Ok, (await h.Journals.PostAsync(draft.JournalId, ct)).Outcome);
    }

    private sealed class Harness : IAsyncDisposable
    {
        public required AccountingDbContext Db { get; init; }

        public required PeriodLockService Locks { get; init; }

        public required JournalService Journals { get; init; }

        public required Guid OrgId { get; init; }

        private long RentId { get; init; }

        private long CashId { get; init; }

        /// <summary>An entry dated 1 August 2026, which every test locks around.</summary>
        public SaveJournalRequest Entry(decimal amount) => new()
        {
            JournalDate = new DateOnly(2026, 8, 1),
            Reference = "Test entry",
            Lines =
            [
                new SaveJournalLineRequest { AccountId = RentId, DebitAmount = amount },
                new SaveJournalLineRequest { AccountId = CashId, CreditAmount = amount },
            ],
        };

        public Task Lock(int roleId, DateOnly upto, CancellationToken ct) =>
            Locks.SetAsync(
                new SavePeriodLockRequest { RoleId = roleId, LockedUpto = upto }, ct);

        public static Task<Harness> CreateAsync(PostgresFixture postgres, int roleId) =>
            CreateAsync(postgres, (int?)roleId, null, null);

        public static async Task<Harness> CreateAsync(
            PostgresFixture postgres, int? roleId, Guid? org, Guid? customer)
        {
            Skip.If(postgres.SkipReason is not null, postgres.SkipReason ?? string.Empty);

            Guid orgId = org ?? Guid.NewGuid();
            Guid customerId = customer ?? Guid.NewGuid();
            var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
            AccountingDbContext db = postgres.CreateContext(customerId, orgId);

            ICurrentUser user = new RoleUser(roleId);

            async Task<long> Account(string code, string name, int typeId)
            {
                Account? existing = await db.Accounts
                    .FirstOrDefaultAsync(a => a.AccountCode == code);

                if (existing is not null)
                {
                    return existing.AccountId;
                }

                var account = new Account
                {
                    OrgId = orgId,
                    AccountTypeId = typeId,
                    AccountCode = code,
                    AccountName = name,
                    IsActive = true,
                };

                db.Accounts.Add(account);
                await db.SaveChangesAsync();
                return account.AccountId;
            }

            if (!await db.NumberingSeries.AnyAsync(s => s.SeriesCode == "JRN"))
            {
                db.NumberingSeries.AddRange(
                    Repository.SeedData.NumberingSeriesSeed.Build(orgId)
                        .Where(s => s.SeriesCode == "JRN"));
                await db.SaveChangesAsync();
            }

            var locks = new PeriodLockService(db, user);
            var postings = new LedgerPostingService(db, tenant, new StubBaseCurrency());

            return new Harness
            {
                Db = db,
                OrgId = orgId,
                Locks = locks,
                RentId = await Account("6100", "Rent", 5),
                CashId = await Account("1010", "Cash", 1),
                Journals = new JournalService(
                    db,
                    postings,
                    locks,
                    new NumberGenerator(
                        db, Options.Create(new NumberingOptions()), new StubFinancialYear()),
                    new StubBaseCurrency(),
                    user,
                    tenant,
                    TimeProvider.System),
            };
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    /// <summary>A caller holding one role, or none at all.</summary>
    private sealed class RoleUser(int? roleId) : ICurrentUser
    {
        public Guid? UserId { get; } = Guid.NewGuid();

        public Guid? CustomerId => null;

        public Guid? OrgId => null;

        public int? RoleId { get; } = roleId;
    }
}
