using Accounting.Api.Services;
using Accounting.Repository.Repositories;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shared.Kernel.Numbering;
using Shared.Kernel.Projects;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// Timesheets and billing a project (TK-106), against a real PostgreSQL: the
/// claim whose row count is the answer, its release, the markup on a
/// re-billed expense, and the timesheet's rules.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ProjectBillingTests
{
    private static readonly Guid Worker = Guid.NewGuid();
    private static readonly Guid Colleague = Guid.NewGuid();

    private readonly PostgresFixture _postgres;

    public ProjectBillingTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>The card's "done when": hours billed on one invoice cannot be billed on another, and come back on its void.</summary>
    [SkippableFact]
    public async Task Two_invoices_claiming_the_same_hours_exactly_one_wins_and_a_release_frees_them()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        long entry = await h.LogAsync(Worker, 3m);
        ProjectItemRef[] hours = [new() { Kind = ProjectItemKind.TimeEntry, ItemId = entry }];

        ClaimProjectItemsResponse first = await h.Billing.ClaimAsync(1001, hours, default);
        ClaimProjectItemsResponse second = await h.Billing.ClaimAsync(1002, hours, default);

        Assert.True(first.Claimed);
        Assert.False(second.Claimed);
        Assert.Equal(0, second.Touched);
        Assert.Equal(ProjectBillingMessages.AlreadyBilled, second.Detail);

        TimeEntry billed = await h.ReadAsync(entry);
        Assert.Equal(TimeBillingStatus.Billed, billed.BillingStatus);
        Assert.Equal(1001, billed.InvoiceId);

        // A retried post of the winning invoice claims again without a fuss.
        Assert.True((await h.Billing.ClaimAsync(1001, hours, default)).Claimed);

        Assert.Equal(1, await h.Billing.ReleaseAsync(1001, default));
        TimeEntry released = await h.ReadAsync(entry);
        Assert.Equal(TimeBillingStatus.Unbilled, released.BillingStatus);
        Assert.Null(released.InvoiceId);

        Assert.True((await h.Billing.ClaimAsync(1002, hours, default)).Claimed);
    }

    [SkippableFact]
    public async Task A_claim_that_loses_one_item_claims_none_of_them()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        long taken = await h.LogAsync(Worker, 2m);
        long free = await h.LogAsync(Worker, 1m, new DateOnly(2026, 9, 2));
        await h.Billing.ClaimAsync(1001, [new() { Kind = ProjectItemKind.TimeEntry, ItemId = taken }], default);

        ClaimProjectItemsResponse both = await h.Billing.ClaimAsync(1002,
            [new() { Kind = ProjectItemKind.TimeEntry, ItemId = taken }, new() { Kind = ProjectItemKind.TimeEntry, ItemId = free }], default);

        Assert.False(both.Claimed);
        Assert.Equal(TimeBillingStatus.Unbilled, (await h.ReadAsync(free)).BillingStatus);
    }

    [SkippableFact]
    public async Task Unbilled_time_is_priced_by_the_project_rate_and_milestones_are_listed()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        await h.LogAsync(Worker, 2.5m);
        h.Db.ProjectMilestones.Add(new ProjectMilestone { ProjectId = h.ProjectId, Name = "Design", Amount = 5_000m });
        await h.Db.SaveChangesAsync();

        ProjectUnbilledView view = (await h.Billing.UnbilledAsync(h.ProjectId, default))!;

        UnbilledTimeItem hour = Assert.Single(view.Time);
        Assert.Equal(800m, hour.Rate);
        Assert.Equal(2_000m, hour.Amount);
        Assert.Equal(5_000m, Assert.Single(view.Milestones).Amount);
    }

    /// <summary>The card's third case: a markup applies to a re-billed expense.</summary>
    [SkippableFact]
    public async Task A_rebilled_expense_carries_its_markup()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        long line = await h.PaidExpenseAsync(1_000m, markupPercent: 10m);

        ProjectUnbilledView view = (await h.Billing.UnbilledAsync(h.ProjectId, default))!;

        UnbilledExpenseItem expense = Assert.Single(view.Expenses);
        Assert.Equal(line, expense.SpendMoneyDetailId);
        Assert.Equal(1_000m, expense.Amount);
        Assert.Equal(1_100m, expense.BillableAmount);

        Assert.True((await h.Billing.ClaimAsync(1001, [new() { Kind = ProjectItemKind.Expense, ItemId = line }], default)).Claimed);
        Assert.Empty((await h.Billing.UnbilledAsync(h.ProjectId, default))!.Expenses);
    }

    [SkippableFact]
    public async Task A_user_logs_only_their_own_time_unless_they_may_edit_projects()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        TimeEntryResult forOther = await h.Time.SaveAsync(null, h.Entry(Colleague, 1m), Worker, mayLogForOthers: false, default);
        Assert.Equal(TimeEntryOutcome.NotYours, forOther.Outcome);

        TimeEntryResult asManager = await h.Time.SaveAsync(null, h.Entry(Colleague, 1m), Worker, mayLogForOthers: true, default);
        Assert.Equal(TimeEntryOutcome.Ok, asManager.Outcome);
    }

    [SkippableFact]
    public async Task A_day_holds_at_most_24_hours()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        await h.LogAsync(Worker, 20m);

        TimeEntryResult over = await h.Time.SaveAsync(null, h.Entry(Worker, 5m), Worker, false, default);

        Assert.Equal(TimeEntryOutcome.Refused, over.Outcome);
        Assert.Contains("24", over.Detail);
    }

    [SkippableFact]
    public async Task Billed_hours_cannot_be_changed()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        long entry = await h.LogAsync(Worker, 2m);
        await h.Billing.ClaimAsync(1001, [new() { Kind = ProjectItemKind.TimeEntry, ItemId = entry }], default);
        h.Db.ChangeTracker.Clear();

        Assert.Equal(TimeEntryOutcome.Billed, (await h.Time.SaveAsync(entry, h.Entry(Worker, 3m), Worker, false, default)).Outcome);
        Assert.Equal(TimeEntryOutcome.Billed, (await h.Time.DeleteAsync(entry, Worker, false, default)).Outcome);
    }

    [Theory]
    [InlineData(ProjectRateBasis.ProjectRate, 100)]
    [InlineData(ProjectRateBasis.TaskRate, 200)]
    [InlineData(ProjectRateBasis.UserRate, 300)]
    public void The_rate_basis_picks_the_rate(ProjectRateBasis basis, int expected) =>
        Assert.Equal(expected, ProjectBillingRepository.RateFor(basis, 100m, 200m, 300m));

    [Fact]
    public void A_markup_rounds_to_two_decimals() =>
        Assert.Equal(123.46m, ProjectBillingRepository.WithMarkup(112.235m, 10m));

    private sealed class Harness : IAsyncDisposable
    {
        public required AccountingDbContext Db { get; init; }

        public required TimeEntryRepository Time { get; init; }

        public required ProjectBillingRepository Billing { get; init; }

        public required SpendMoneyRepository Payments { get; init; }

        public required long ProjectId { get; init; }

        public required long TaskId { get; init; }

        public required long BankAccountId { get; init; }

        public SaveTimeEntryRequest Entry(Guid user, decimal hours, DateOnly? on = null) => new()
        {
            ProjectId = ProjectId,
            ProjectTaskId = TaskId,
            UserId = user,
            WorkDate = on ?? new DateOnly(2026, 9, 1),
            Hours = hours,
        };

        public async Task<long> LogAsync(Guid user, decimal hours, DateOnly? on = null)
        {
            TimeEntryResult logged = await Time.SaveAsync(null, Entry(user, hours, on), user, false, default);
            Assert.Equal(TimeEntryOutcome.Ok, logged.Outcome);
            Db.ChangeTracker.Clear();
            return logged.TimeEntryId;
        }

        public async Task<TimeEntry> ReadAsync(long id)
        {
            Db.ChangeTracker.Clear();
            return await Db.TimeEntries.AsNoTracking().SingleAsync(t => t.TimeEntryId == id);
        }

        /// <summary>A posted supplier deposit whose one line is billable to the project.</summary>
        public async Task<long> PaidExpenseAsync(decimal amount, decimal markupPercent)
        {
            MoneyDocumentResult draft = await Payments.CreateAsync(new SaveMoneyDocumentRequest
            {
                TransactionDate = new DateOnly(2026, 9, 1),
                BankAccountId = BankAccountId,
                ContactId = 42,
                Amount = amount,
                Lines =
                [
                    new SaveMoneyLineRequest
                    {
                        LedgerSourceId = 8,
                        Amount = amount,
                        LineMemo = "Tiles for the client",
                        ProjectId = ProjectId,
                        IsBillable = true,
                        MarkupPercent = markupPercent,
                    },
                ],
            }, default);
            Assert.Equal(MoneyDocumentOutcome.Ok, draft.Outcome);
            Assert.Equal(MoneyDocumentOutcome.Ok, (await Payments.PostAsync(draft.DocumentId, default)).Outcome);
            Db.ChangeTracker.Clear();

            return await Db.SpendMoneyDetails.AsNoTracking()
                .Where(d => d.SpendMoneyId == draft.DocumentId)
                .Select(d => d.SpendMoneyDetailId)
                .SingleAsync();
        }

        public static async Task<Harness> CreateAsync(PostgresFixture postgres)
        {
            Skip.If(postgres.SkipReason is not null, postgres.SkipReason ?? string.Empty);

            var orgId = Guid.NewGuid();
            var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = orgId };
            AccountingDbContext db = postgres.CreateContext(tenant.CustomerId!.Value, orgId);

            db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
            var bankLedger = new Account { OrgId = orgId, AccountTypeId = 1, AccountCode = "1020", AccountName = "Bank", IsActive = true };
            db.Accounts.Add(bankLedger);
            await db.SaveChangesAsync();

            var bankAccount = new BankAccount
            {
                OrgId = orgId,
                AccountName = "Current",
                AccountNumber = "000111",
                CurrencyCode = "INR",
                LedgerAccountId = bankLedger.AccountId,
                IsActive = true,
            };
            db.BankAccounts.Add(bankAccount);
            await db.SaveChangesAsync();

            var numbers = new NumberGenerator(db, Options.Create(new NumberingOptions()), new StubFinancialYear());
            var projects = new ProjectRepository(db, numbers, new StubBaseCurrency(), TimeProvider.System, NullLogger<ProjectRepository>.Instance);

            SaveProjectResult created = await projects.SaveAsync(null, new SaveProjectRequest
            {
                ProjectName = "Kitchen fit-out",
                BillingMethod = "TimeAndMaterials",
                RateBasis = "ProjectRate",
                HourlyRate = 800m,
                Tasks = [new SaveProjectTaskRequest { TaskName = "Installation" }],
            }, default);
            Assert.Equal(SaveProjectOutcome.Ok, created.Outcome);

            long taskId = await db.ProjectTasks.Where(t => t.ProjectId == created.ProjectId).Select(t => t.ProjectTaskId).SingleAsync();
            db.ChangeTracker.Clear();

            return new Harness
            {
                Db = db,
                ProjectId = created.ProjectId,
                TaskId = taskId,
                BankAccountId = bankAccount.BankAccountId,
                Time = new TimeEntryRepository(db),
                Billing = new ProjectBillingRepository(db),
                Payments = new SpendMoneyRepository(db, new RecordingLedger(), numbers, new StubBaseCurrency(), new StubCurrentUser(), TimeProvider.System),
            };
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
