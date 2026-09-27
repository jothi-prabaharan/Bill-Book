using Accounting.Api.Services;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Kernel.Budgets;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// Budgets (TK-108) against a real PostgreSQL: the card's "done when" (import,
/// approve, then no edits), a revision superseding its original, and last
/// year's actuals landing in the right months for an April-start branch.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class BudgetTests
{
    private static readonly Guid Approver = Guid.NewGuid();

    private readonly PostgresFixture _postgres;

    public BudgetTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>The card's "done when".</summary>
    [SkippableFact]
    public async Task An_imported_budget_once_approved_cannot_be_edited()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        long id = await h.CreateAsync("FY 2026-27 base");

        BudgetResult imported = await h.Budgets.ImportAsync(id,
        [
            ["6100", "Rent", "30000", "30000", "30000", "30000", "30000", "30000", "30000", "30000", "30000", "30000", "30000", "30000", "360000"],
            ["4000", "Sales", "1,00,000.50", "", "", "", "", "", "", "", "", "", "", "200000", "300000.50"],
        ], default);
        Assert.Equal(BudgetOutcome.Ok, imported.Outcome);

        BudgetView view = (await h.Budgets.GetAsync(id, default))!;
        Assert.Equal(360_000m, view.Lines.Single(l => l.AccountCode == "6100").Total);
        BudgetLineView sales = view.Lines.Single(l => l.AccountCode == "4000");
        Assert.Equal(100_000.50m, sales.Amounts[0]);
        Assert.Equal(200_000m, sales.Amounts[11]);

        Assert.Equal(BudgetOutcome.Ok, (await h.Budgets.ApproveAsync(id, makeDefault: true, Approver, default)).Outcome);
        h.Db.ChangeTracker.Clear();

        BudgetResult edit = await h.Budgets.SaveAsync(id, new SaveBudgetRequest { Name = "Changed", Lines = [] }, default);
        BudgetResult reimport = await h.Budgets.ImportAsync(id, [["6100", "Rent", "1", "1", "1", "1", "1", "1", "1", "1", "1", "1", "1", "1"]], default);
        BudgetResult delete = await h.Budgets.DeleteAsync(id, default);

        Assert.Equal(BudgetOutcome.Locked, edit.Outcome);
        Assert.Equal(BudgetOutcome.Locked, reimport.Outcome);
        Assert.Equal(BudgetOutcome.Locked, delete.Outcome);

        h.Db.ChangeTracker.Clear();
        BudgetView after = (await h.Budgets.GetAsync(id, default))!;
        Assert.Equal("Approved", after.Status);
        Assert.True(after.IsBranchDefault);
        Assert.Equal(view.Total, after.Total);
    }

    [SkippableFact]
    public async Task A_revision_supersedes_its_original_and_takes_the_default_when_approved()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        long original = await h.CreateAsync("Base");
        await h.Budgets.SaveAsync(original, h.Rent(10_000m), default);
        await h.Budgets.ApproveAsync(original, makeDefault: true, Approver, default);
        h.Db.ChangeTracker.Clear();

        BudgetResult revised = await h.Budgets.ReviseAsync(original, "Rent went up in October", default);
        Assert.Equal(BudgetOutcome.Ok, revised.Outcome);
        long revision = revised.BudgetId;

        // A second draft revision of the same budget is refused while one is open.
        Assert.Equal(BudgetOutcome.Refused, (await h.Budgets.ReviseAsync(original, "Again", default)).Outcome);

        h.Db.ChangeTracker.Clear();
        BudgetView copy = (await h.Budgets.GetAsync(revision, default))!;
        Assert.Equal("Draft", copy.Status);
        Assert.Equal(original, copy.RevisionOf);
        Assert.Equal(120_000m, copy.Total);

        await h.Budgets.SaveAsync(revision, h.Rent(12_000m), default);
        h.Db.ChangeTracker.Clear();

        // The original stays the approved default until the revision is approved.
        Assert.True((await h.Budgets.GetAsync(original, default))!.IsBranchDefault);

        Assert.Equal(BudgetOutcome.Ok, (await h.Budgets.ApproveAsync(revision, makeDefault: false, Approver, default)).Outcome);
        h.Db.ChangeTracker.Clear();

        BudgetView old = (await h.Budgets.GetAsync(original, default))!;
        BudgetView now = (await h.Budgets.GetAsync(revision, default))!;
        Assert.Equal("Superseded", old.Status);
        Assert.False(old.IsBranchDefault);
        Assert.Equal("Approved", now.Status);
        Assert.True(now.IsBranchDefault);
        Assert.Equal(144_000m, now.Total);
    }

    [SkippableFact]
    public async Task Last_years_actuals_seed_the_right_months_for_an_april_start_branch()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        // Financial year 2025-26 runs April 2025 to March 2026.
        await h.PostAsync(new DateOnly(2025, 4, 15), h.RentId, debit: 1_000m);
        await h.PostAsync(new DateOnly(2026, 3, 10), h.RentId, debit: 500m);
        await h.PostAsync(new DateOnly(2025, 3, 31), h.RentId, debit: 777m);
        await h.PostAsync(new DateOnly(2026, 4, 1), h.RentId, debit: 999m);
        await h.PostAsync(new DateOnly(2025, 5, 5), h.SalesId, credit: 3_000m);
        await h.PostAsync(new DateOnly(2025, 5, 6), h.ReturnsId, debit: 200m);

        BudgetResult created = await h.Budgets.CreateAsync(new CreateBudgetRequest
        {
            Name = "FY 2026-27 from actuals",
            Year = 2026,
            Source = "LastYearActuals",
            AdjustPercent = 10m,
            TypeAdjustments = [new BudgetTypeAdjustment { AccountTypeId = 4, Percent = 20m }],
        }, default);
        Assert.Equal(BudgetOutcome.Ok, created.Outcome);
        h.Db.ChangeTracker.Clear();

        BudgetView view = (await h.Budgets.GetAsync(created.BudgetId, default))!;
        Assert.Equal(new DateOnly(2026, 4, 1), view.FinancialYearStart);

        BudgetLineView rent = view.Lines.Single(l => l.AccountId == h.RentId);
        Assert.Equal(1_100m, rent.Amounts[0]);
        Assert.Equal(550m, rent.Amounts[11]);
        Assert.Equal(1_650m, rent.Total);

        // Income takes its own 20%; the contra return is positive in its own direction.
        Assert.Equal(3_600m, view.Lines.Single(l => l.AccountId == h.SalesId).Amounts[1]);
        Assert.Equal(240m, view.Lines.Single(l => l.AccountId == h.ReturnsId).Amounts[1]);

        // The bank side of each posting is a balance-sheet account, and is not seeded.
        Assert.DoesNotContain(view.Lines, l => l.AccountTypeId == 1);
    }

    [SkippableFact]
    public async Task An_import_naming_unknown_codes_lists_them_and_writes_nothing()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        long id = await h.CreateAsync("Draft");
        await h.Budgets.SaveAsync(id, h.Rent(5_000m), default);
        h.Db.ChangeTracker.Clear();

        BudgetResult result = await h.Budgets.ImportAsync(id,
        [
            ["6100", "Rent", "1", "1", "1", "1", "1", "1", "1", "1", "1", "1", "1", "1"],
            ["9999", "Nobody", "1", "1", "1", "1", "1", "1", "1", "1", "1", "1", "1", "1"],
            ["8888", "Nobody either", "1", "1", "1", "1", "1", "1", "1", "1", "1", "1", "1", "1"],
        ], default);

        Assert.Equal(BudgetOutcome.UnknownCodes, result.Outcome);
        Assert.Equal(["Account 9999", "Account 8888"], result.UnknownCodes);

        h.Db.ChangeTracker.Clear();
        Assert.Equal(60_000m, (await h.Budgets.GetAsync(id, default))!.Total);
    }

    [SkippableFact]
    public async Task A_parent_account_or_a_negative_amount_is_refused()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        long id = await h.CreateAsync("Draft");

        BudgetResult parent = await h.Budgets.SaveAsync(id, new SaveBudgetRequest
        {
            Name = "Draft",
            Lines = [new SaveBudgetLineRequest { AccountId = h.ExpensesParentId, Amounts = [.. Enumerable.Repeat(1m, 12)] }],
        }, default);
        BudgetResult negative = await h.Budgets.SaveAsync(id, new SaveBudgetRequest
        {
            Name = "Draft",
            Lines = [new SaveBudgetLineRequest { AccountId = h.RentId, Amounts = [-1m, .. Enumerable.Repeat(0m, 11)] }],
        }, default);

        Assert.Equal(BudgetOutcome.Refused, parent.Outcome);
        Assert.Equal(BudgetOutcome.Refused, negative.Outcome);
    }

    [SkippableFact]
    public async Task Only_one_approved_budget_is_the_branch_default_for_a_year()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        long base_ = await h.CreateAsync("Base");
        long stretch = await h.CreateAsync("Stretch");

        await h.Budgets.ApproveAsync(base_, makeDefault: true, Approver, default);
        await h.Budgets.ApproveAsync(stretch, makeDefault: true, Approver, default);
        h.Db.ChangeTracker.Clear();

        Assert.False((await h.Budgets.GetAsync(base_, default))!.IsBranchDefault);
        Assert.True((await h.Budgets.GetAsync(stretch, default))!.IsBranchDefault);

        Assert.Equal(BudgetOutcome.Ok, (await h.Budgets.MakeDefaultAsync(base_, default)).Outcome);
        h.Db.ChangeTracker.Clear();
        Assert.True((await h.Budgets.GetAsync(base_, default))!.IsBranchDefault);
        Assert.False((await h.Budgets.GetAsync(stretch, default))!.IsBranchDefault);
    }

    // ---- over-budget warnings (TK-109) ------------------------------------------

    /// <summary>TK-109's second case: an expense over budget warns, and the journal still saves.</summary>
    [SkippableFact]
    public async Task An_expense_over_this_months_budget_warns_and_the_journal_still_saves()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        await h.ApprovedRentBudgetAsync(10_000m);
        await h.PostAsync(new DateOnly(2026, 4, 5), h.RentId, debit: 8_000m);

        SaveJournalResult saved = await h.Journals.CreateAsync(h.RentJournal(new DateOnly(2026, 4, 20), 3_000m), default);
        Assert.Equal(SaveJournalOutcome.Ok, saved.Outcome);

        List<string> warnings = await new BudgetCheckService(h.Db, new StubBranchSettings(yearToDate: false))
            .WarningsAsync(new DateOnly(2026, 4, 20), [h.RentSpend(3_000m)], default);

        Assert.Equal(["Rent is 1,000.00 over the Apr 2026 budget."], warnings);
        h.Db.ChangeTracker.Clear();
        Assert.True(await h.Db.Journals.AnyAsync(j => j.JournalId == saved.JournalId));
    }

    [SkippableFact]
    public async Task Spending_within_the_month_does_not_warn()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        await h.ApprovedRentBudgetAsync(10_000m);
        await h.PostAsync(new DateOnly(2026, 4, 5), h.RentId, debit: 8_000m);

        Assert.Empty(await new BudgetCheckService(h.Db, new StubBranchSettings(yearToDate: false))
            .WarningsAsync(new DateOnly(2026, 4, 20), [h.RentSpend(2_000m)], default));
    }

    [SkippableFact]
    public async Task The_year_to_date_setting_compares_the_year_rather_than_the_month()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        await h.ApprovedRentBudgetAsync(10_000m);
        await h.PostAsync(new DateOnly(2026, 4, 5), h.RentId, debit: 18_000m);
        DateOnly may = new(2026, 5, 10);

        List<string> monthly = await new BudgetCheckService(h.Db, new StubBranchSettings(yearToDate: false))
            .WarningsAsync(may, [h.RentSpend(3_000m)], default);
        List<string> yearToDate = await new BudgetCheckService(h.Db, new StubBranchSettings(yearToDate: true))
            .WarningsAsync(may, [h.RentSpend(3_000m)], default);

        Assert.Empty(monthly);
        Assert.Equal(["Rent is 1,000.00 over the budget for the year to May 2026."], yearToDate);
    }

    [SkippableFact]
    public async Task Income_and_unbudgeted_accounts_never_warn()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        await h.ApprovedRentBudgetAsync(10_000m);

        Assert.Empty(await new BudgetCheckService(h.Db, new StubBranchSettings(yearToDate: false)).WarningsAsync(
            new DateOnly(2026, 4, 20),
            [
                new BudgetSpendLine { AccountId = h.SalesId, Amount = 1_000_000m },
                new BudgetSpendLine { AccountId = h.ExpensesParentId, Amount = 1_000_000m },
            ],
            default));
    }

    [SkippableFact]
    public async Task Without_an_approved_branch_budget_nothing_warns()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        long draft = await h.CreateAsync("Still a draft");
        await h.Budgets.SaveAsync(draft, h.Rent(1m), default);

        Assert.Empty(await new BudgetCheckService(h.Db, new StubBranchSettings(yearToDate: false))
            .WarningsAsync(new DateOnly(2026, 4, 20), [h.RentSpend(5_000m)], default));
    }

    // ---- the pure parts ---------------------------------------------------------

    [Theory]
    [InlineData(5, false, 100, 30, 70)]
    [InlineData(4, false, 30, 100, 70)]
    [InlineData(4, true, 100, 30, 70)]
    [InlineData(1, true, 30, 100, 70)]
    public void An_amount_is_read_in_the_accounts_normal_direction(int type, bool contra, int debit, int credit, int expected) =>
        Assert.Equal(expected, BudgetMath.NormalAmount(type, contra, debit, credit));

    [Fact]
    public void Periods_count_from_the_financial_years_first_month()
    {
        DateOnly april = new(2026, 4, 1);

        Assert.Equal(1, BudgetMath.PeriodOf(april, new DateOnly(2026, 4, 30)));
        Assert.Equal(10, BudgetMath.PeriodOf(april, new DateOnly(2027, 1, 1)));
        Assert.Equal(12, BudgetMath.PeriodOf(april, new DateOnly(2027, 3, 31)));
        Assert.Equal(0, BudgetMath.PeriodOf(april, new DateOnly(2027, 4, 1)));
        Assert.Equal(0, BudgetMath.PeriodOf(april, new DateOnly(2026, 3, 31)));
        Assert.Equal(new DateOnly(2026, 4, 1), BudgetMath.YearStartOf(new DateOnly(2027, 2, 14), 4));
    }

    [Fact]
    public void A_sheet_with_a_word_where_an_amount_belongs_is_refused_with_its_place()
    {
        SheetParse parsed = BudgetMath.ParseSheet([["6100", "Rent", "100", "lots"]], byProject: false);

        Assert.Empty(parsed.Rows);
        Assert.Equal("Row 2, month 2: \"lots\" is not a number.", parsed.Error);
    }

    [Fact]
    public void A_sheet_by_project_reads_the_project_column_and_skips_blank_rows()
    {
        SheetParse parsed = BudgetMath.ParseSheet(
        [
            ["6100", "PRJ-0001", "Rent", "5"],
            ["", "", "Total", "5"],
        ], byProject: true);

        SheetRow row = Assert.Single(parsed.Rows);
        Assert.Equal("PRJ-0001", row.ProjectCode);
        Assert.Equal(5m, row.Amounts[0]);
        Assert.Equal(0m, row.Amounts[1]);
    }

    [Fact]
    public void The_sheet_header_names_the_months_from_the_years_start() =>
        Assert.Equal(
            ["Account code", "Account name", "Apr 2026", "May 2026", "Jun 2026", "Jul 2026", "Aug 2026", "Sep 2026",
             "Oct 2026", "Nov 2026", "Dec 2026", "Jan 2027", "Feb 2027", "Mar 2027", "Total"],
            BudgetMath.SheetHeader(new DateOnly(2026, 4, 1), byProject: false));

    private sealed class Harness : IAsyncDisposable
    {
        public required AccountingDbContext Db { get; init; }

        public required BudgetService Budgets { get; init; }

        public required LedgerPostingService Postings { get; init; }

        public required JournalService Journals { get; init; }

        public required long RentId { get; init; }

        public required long SalesId { get; init; }

        public required long ReturnsId { get; init; }

        public required long BankId { get; init; }

        public required long ExpensesParentId { get; init; }

        private long _transactionId = 7000;

        public async Task<long> CreateAsync(string name)
        {
            BudgetResult created = await Budgets.CreateAsync(new CreateBudgetRequest { Name = name, Year = 2026, Source = "Blank" }, default);
            Assert.Equal(BudgetOutcome.Ok, created.Outcome);
            Db.ChangeTracker.Clear();
            return created.BudgetId;
        }

        /// <summary>An approved branch budget for 2026-27 with rent at <paramref name="monthly"/> every month.</summary>
        public async Task ApprovedRentBudgetAsync(decimal monthly)
        {
            long id = await CreateAsync("Base");
            Assert.Equal(BudgetOutcome.Ok, (await Budgets.SaveAsync(id, Rent(monthly), default)).Outcome);
            Assert.Equal(BudgetOutcome.Ok, (await Budgets.ApproveAsync(id, makeDefault: true, Approver, default)).Outcome);
            Db.ChangeTracker.Clear();
        }

        public BudgetSpendLine RentSpend(decimal amount) => new() { AccountId = RentId, Amount = amount };

        public SaveJournalRequest RentJournal(DateOnly on, decimal amount) => new()
        {
            JournalDate = on,
            Memo = "Rent",
            Lines =
            [
                new SaveJournalLineRequest { AccountId = RentId, DebitAmount = amount },
                new SaveJournalLineRequest { AccountId = BankId, CreditAmount = amount },
            ],
        };

        public SaveBudgetRequest Rent(decimal monthly) => new()
        {
            Name = "Draft",
            Lines = [new SaveBudgetLineRequest { AccountId = RentId, Amounts = [.. Enumerable.Repeat(monthly, 12)] }],
        };

        /// <summary>One side against the bank, so the posting balances.</summary>
        public async Task PostAsync(DateOnly on, long accountId, decimal debit = 0m, decimal credit = 0m)
        {
            PostLedgerResult posted = await Postings.PostAsync(new PostLedgerRequest
            {
                TransactionTypeCode = "JNL",
                TransactionId = ++_transactionId,
                LedgerDate = on,
                Legs =
                [
                    new LedgerLegRequest { LedgerTypeId = 1, LedgerSourceId = 1, TransactionDetailId = 1, AccountId = accountId, DebitAmount = debit, CreditAmount = credit },
                    new LedgerLegRequest { LedgerTypeId = 1, LedgerSourceId = 1, TransactionDetailId = 2, AccountId = BankId, DebitAmount = credit, CreditAmount = debit },
                ],
            }, default);
            Assert.True(posted.Outcome == PostLedgerOutcome.Ok, posted.Detail);
            Db.ChangeTracker.Clear();
        }

        public static async Task<Harness> CreateAsync(PostgresFixture postgres)
        {
            Skip.If(postgres.SkipReason is not null, postgres.SkipReason ?? string.Empty);

            var orgId = Guid.NewGuid();
            var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = orgId };
            AccountingDbContext db = postgres.CreateContext(tenant.CustomerId!.Value, orgId);

            var parent = new Account { OrgId = orgId, AccountTypeId = 5, AccountCode = "6000", AccountName = "Premises", IsActive = true };
            var sales = new Account { OrgId = orgId, AccountTypeId = 4, AccountCode = "4000", AccountName = "Sales", IsActive = true };
            var returns = new Account { OrgId = orgId, AccountTypeId = 4, IsContra = true, AccountCode = "4100", AccountName = "Sales returns", IsActive = true };
            var bank = new Account { OrgId = orgId, AccountTypeId = 1, AccountCode = "1020", AccountName = "Bank", IsActive = true };
            db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
            db.Accounts.AddRange(parent, sales, returns, bank);
            await db.SaveChangesAsync();

            var rent = new Account { OrgId = orgId, AccountTypeId = 5, AccountCode = "6100", AccountName = "Rent", ParentAccountId = parent.AccountId, IsActive = true };
            db.Accounts.Add(rent);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            return new Harness
            {
                Db = db,
                Budgets = new BudgetService(db, new StubFinancialYear(4), TimeProvider.System),
                Postings = new LedgerPostingService(db, tenant, new StubBaseCurrency()),
                Journals = new JournalService(
                    db,
                    new LedgerPostingService(db, tenant, new StubBaseCurrency()),
                    new PeriodLockService(db, new StubCurrentUser()),
                    new NumberGenerator(db, Options.Create(new NumberingOptions()), new StubFinancialYear()),
                    new StubBaseCurrency(),
                    new StubCurrentUser(Approver),
                    tenant,
                    TimeProvider.System),
                RentId = rent.AccountId,
                SalesId = sales.AccountId,
                ReturnsId = returns.AccountId,
                BankId = bank.AccountId,
                ExpensesParentId = parent.AccountId,
            };
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}

/// <summary>Branch settings with only the budget warning scope that matters here.</summary>
internal sealed class StubBranchSettings(bool yearToDate) : IBranchSettingsProvider
{
    public Task<BranchSettings?> GetSettingsAsync(CancellationToken ct = default) =>
        Task.FromResult<BranchSettings?>(new BranchSettings("33", DiscountBeforeTax: true, BudgetWarningYearToDate: yearToDate));
}
