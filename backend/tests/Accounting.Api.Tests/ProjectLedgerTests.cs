using System.ComponentModel.DataAnnotations;
using Accounting.Api.Services;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// The project ledger dimension (TK-104), against a real PostgreSQL: a project
/// is created with its PRJ code, a manual journal line carries it to the ledger,
/// and the posting API refuses a project that is not the branch's or whose job
/// is over.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ProjectLedgerTests
{
    private readonly PostgresFixture _postgres;

    public ProjectLedgerTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>The card's "done when": a manual journal line tagged with a project appears on that project's ledger rows.</summary>
    [SkippableFact]
    public async Task A_manual_journal_line_tagged_with_a_project_appears_on_its_ledger()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        long project = await h.ProjectAsync("Kitchen fit-out");

        SaveJournalResult draft = await h.Journals.CreateAsync(h.Entry(500m, project), default);
        Assert.Equal(SaveJournalOutcome.Ok, draft.Outcome);
        Assert.Equal(SaveJournalOutcome.Ok, (await h.Journals.PostAsync(draft.JournalId, default)).Outcome);

        List<ProjectLedgerRow> rows = (await h.Projects.LedgerAsync(project, null, null, default))!;

        ProjectLedgerRow row = Assert.Single(rows);
        Assert.Equal("6100", row.AccountCode);
        Assert.Equal(500m, row.Debit);

        // Only the tagged line carries the project; the cash line does not.
        Assert.Equal(1, await h.Db.JournalLedger.CountAsync(l => l.ProjectId == project));
        Assert.Equal(2, await h.Db.JournalLedger.CountAsync(l => l.JournalId == draft.JournalId));
    }

    [SkippableFact]
    public async Task A_leg_with_a_completed_project_is_refused_by_the_posting_api()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        long project = await h.ProjectAsync("Finished job", "Completed");

        PostLedgerResult result = await h.Postings.PostAsync(new PostLedgerRequest
        {
            CustomerId = h.Tenant.CustomerId!.Value,
            OrgId = h.Tenant.OrgId!.Value,
            TransactionTypeCode = "JRN",
            TransactionId = 999,
            LedgerDate = new DateOnly(2026, 8, 1),
            Legs =
            [
                new LedgerLegRequest { LedgerTypeId = 3, LedgerSourceId = 12, TransactionDetailId = 1, AccountId = h.RentId, DebitAmount = 100m, ProjectId = project },
                new LedgerLegRequest { LedgerTypeId = 3, LedgerSourceId = 12, TransactionDetailId = 2, AccountId = h.CashId, CreditAmount = 100m },
            ],
        }, default);

        Assert.Equal(PostLedgerOutcome.ProjectRefused, result.Outcome);
        Assert.Contains("completed", result.Detail);
        Assert.Empty(await h.Db.JournalLedger.Where(l => l.TransactionId == 999).ToListAsync());
    }

    [SkippableFact]
    public async Task A_project_that_is_not_the_branchs_is_refused()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        SaveJournalResult draft = await h.Journals.CreateAsync(h.Entry(500m, projectId: 987_654), default);

        Assert.Equal(SaveJournalOutcome.ProjectRefused, draft.Outcome);
        Assert.Contains("not a project of this branch", draft.Detail);
    }

    [SkippableFact]
    public async Task A_journal_naming_a_cancelled_project_cannot_be_saved()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        long project = await h.ProjectAsync("Abandoned", "Cancelled");

        SaveJournalResult draft = await h.Journals.CreateAsync(h.Entry(500m, project), default);

        Assert.Equal(SaveJournalOutcome.ProjectRefused, draft.Outcome);
    }

    [SkippableFact]
    public async Task A_new_project_takes_a_PRJ_code_and_a_dropped_task_is_deactivated_not_deleted()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        SaveProjectResult created = await h.Projects.SaveAsync(null, new SaveProjectRequest
        {
            ProjectName = "Office IT rollout",
            BillingMethod = "TimeAndMaterials",
            Tasks = [new SaveProjectTaskRequest { TaskName = "Cabling" }, new SaveProjectTaskRequest { TaskName = "Setup" }],
            Milestones = [new SaveProjectMilestoneRequest { Name = "Kick-off", Amount = 10_000m }],
        }, default);

        Assert.Equal(SaveProjectOutcome.Ok, created.Outcome);
        ProjectView view = (await h.Projects.GetAsync(created.ProjectId, default))!;
        Assert.StartsWith("PRJ", view.ProjectCode);
        Assert.Equal("ProjectRate", view.RateBasis);
        Assert.Equal(2, view.Tasks.Count);

        long kept = view.Tasks[0].ProjectTaskId;
        SaveProjectResult saved = await h.Projects.SaveAsync(created.ProjectId, new SaveProjectRequest
        {
            ProjectName = "Office IT rollout",
            BillingMethod = "TimeAndMaterials",
            Tasks = [new SaveProjectTaskRequest { ProjectTaskId = kept, TaskName = "Cabling" }],
        }, default);
        Assert.Equal(SaveProjectOutcome.Ok, saved.Outcome);

        h.Db.ChangeTracker.Clear();
        List<ProjectTask> tasks = await h.Db.ProjectTasks.AsNoTracking().Where(t => t.ProjectId == created.ProjectId).ToListAsync();
        Assert.Equal(2, tasks.Count);
        Assert.True(tasks.Single(t => t.ProjectTaskId == kept).IsActive);
        Assert.False(tasks.Single(t => t.ProjectTaskId != kept).IsActive);
        Assert.Empty(await h.Db.ProjectMilestones.AsNoTracking().Where(m => m.ProjectId == created.ProjectId).ToListAsync());
    }

    [SkippableFact]
    public async Task A_billed_milestone_survives_being_left_off_the_form()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        long project = await h.ProjectAsync("Fixed fee job");

        h.Db.ProjectMilestones.Add(new ProjectMilestone { ProjectId = project, Name = "Design", Amount = 5_000m, InvoiceId = 42 });
        await h.Db.SaveChangesAsync();
        h.Db.ChangeTracker.Clear();

        await h.Projects.SaveAsync(project, new SaveProjectRequest { ProjectName = "Fixed fee job", BillingMethod = "FixedFee" }, default);

        h.Db.ChangeTracker.Clear();
        Assert.Single(await h.Db.ProjectMilestones.AsNoTracking().Where(m => m.ProjectId == project).ToListAsync());
    }

    [SkippableFact]
    public async Task Another_service_learns_which_projects_it_may_post_to()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        long open = await h.ProjectAsync("Open");
        long done = await h.ProjectAsync("Done", "Completed");

        var found = await h.Projects.FindAsync([open, done, 123_456], default);

        Assert.Equal(2, found.Count);
        Assert.True(found.Single(p => p.ProjectId == open).IsPostable);
        Assert.False(found.Single(p => p.ProjectId == done).IsPostable);
    }

    /// <summary>
    /// A replacement that names no project keeps the one its key carried
    /// (TK-105): the costing worker settles an invoice line's cost of sales on
    /// the line's key and knows nothing of projects.
    /// </summary>
    [SkippableFact]
    public async Task A_replacement_without_a_project_keeps_the_one_its_key_carried()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        long project = await h.ProjectAsync("Job");

        PostLedgerRequest Cost(decimal amount, long? projectId) => new()
        {
            CustomerId = h.Tenant.CustomerId!.Value,
            OrgId = h.Tenant.OrgId!.Value,
            TransactionTypeCode = "INV",
            TransactionId = 777,
            LedgerDate = new DateOnly(2026, 8, 1),
            Legs =
            [
                new LedgerLegRequest { LedgerTypeId = 4, LedgerSourceId = 3, TransactionDetailId = 5, AccountId = h.RentId, DebitAmount = amount, ProjectId = projectId },
                new LedgerLegRequest { LedgerTypeId = 4, LedgerSourceId = 3, TransactionDetailId = 5, AccountId = h.CashId, CreditAmount = amount, ProjectId = projectId },
            ],
        };

        Assert.Equal(PostLedgerOutcome.Ok, (await h.Postings.PostAsync(Cost(100m, project), default)).Outcome);
        Assert.Equal(PostLedgerOutcome.Ok, (await h.Postings.PostAsync(Cost(120m, null), default)).Outcome);

        h.Db.ChangeTracker.Clear();
        List<JournalLedger> rows = await h.Db.JournalLedger.AsNoTracking().Where(l => l.TransactionId == 777).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(project, r.ProjectId));
        Assert.Equal(120m, rows.Sum(r => r.DebitAmountBase));
    }

    /// <summary>
    /// Ledger type 7 (GDNI, TK-90) passes the request's validation. The range
    /// said six, which refused every delivery challan's posting at the door.
    /// </summary>
    [Fact]
    public void A_gdni_leg_passes_validation()
    {
        var leg = new LedgerLegRequest { LedgerTypeId = 7, LedgerSourceId = 3, AccountSystemName = "Goods Delivered Not Invoiced", DebitAmount = 1m };
        var results = new List<ValidationResult>();

        Assert.True(Validator.TryValidateObject(leg, new ValidationContext(leg), results, validateAllProperties: true));
    }

    [Theory]
    [InlineData(ProjectStatus.Active, true)]
    [InlineData(ProjectStatus.OnHold, true)]
    [InlineData(ProjectStatus.Completed, false)]
    [InlineData(ProjectStatus.Cancelled, false)]
    public void Only_an_open_job_takes_postings(ProjectStatus status, bool postable) =>
        Assert.Equal(postable, ProjectRules.IsPostable(status));

    private sealed class Harness : IAsyncDisposable
    {
        public required AccountingDbContext Db { get; init; }

        public required TenantContext Tenant { get; init; }

        public required JournalService Journals { get; init; }

        public required LedgerPostingService Postings { get; init; }

        public required ProjectService Projects { get; init; }

        public required long RentId { get; init; }

        public required long CashId { get; init; }

        public SaveJournalRequest Entry(decimal amount, long? projectId) => new()
        {
            JournalDate = new DateOnly(2026, 8, 1),
            Reference = "Materials",
            Lines =
            [
                new SaveJournalLineRequest { AccountId = RentId, DebitAmount = amount, ProjectId = projectId },
                new SaveJournalLineRequest { AccountId = CashId, CreditAmount = amount },
            ],
        };

        public async Task<long> ProjectAsync(string name, string status = "Active")
        {
            SaveProjectResult created = await Projects.SaveAsync(null, new SaveProjectRequest
            {
                ProjectName = name,
                BillingMethod = "TimeAndMaterials",
                Status = status,
            }, default);
            Assert.Equal(SaveProjectOutcome.Ok, created.Outcome);
            Db.ChangeTracker.Clear();
            return created.ProjectId;
        }

        public static async Task<Harness> CreateAsync(PostgresFixture postgres)
        {
            Skip.If(postgres.SkipReason is not null, postgres.SkipReason ?? string.Empty);

            var orgId = Guid.NewGuid();
            var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = orgId };
            AccountingDbContext db = postgres.CreateContext(tenant.CustomerId!.Value, orgId);

            async Task<long> Account(string code, string name, int typeId)
            {
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

            db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
            await db.SaveChangesAsync();

            var numbers = new NumberGenerator(db, Options.Create(new NumberingOptions()), new StubFinancialYear());
            var postings = new LedgerPostingService(db, tenant, new StubBaseCurrency());

            return new Harness
            {
                Db = db,
                Tenant = tenant,
                Postings = postings,
                RentId = await Account("6100", "Rent", 5),
                CashId = await Account("1010", "Cash", 1),
                Projects = new ProjectService(db, numbers, new StubBaseCurrency(), TimeProvider.System, NullLogger<ProjectService>.Instance),
                Journals = new JournalService(
                    db,
                    postings,
                    new PeriodLockService(db, new StubCurrentUser()),
                    numbers,
                    new StubBaseCurrency(),
                    new StubCurrentUser(),
                    tenant,
                    TimeProvider.System),
            };
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
