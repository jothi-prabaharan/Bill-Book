using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Sales.Repository.Repositories;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;
using Shared.Kernel.Recurring;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// Recurring invoices (TK-139). The card's Done-when line: a due schedule
/// creates one invoice, and a rerun creates none.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class RecurringInvoiceTests
{
    private const long CustomerContactId = 42;

    private readonly PostgresFixture _pg;

    public RecurringInvoiceTests(PostgresFixture pg) => _pg = pg;

    private sealed record Books(SalesDbContext Db, InvoiceRepository Invoices, RecurringBillingRepository Schedules, RecordingLedger Ledger);

    private static Books Open(PostgresFixture pg, Guid customerId, Guid orgId)
    {
        TenantContext tenant = new() { CustomerId = customerId, OrgId = orgId, CustomerCode = "0000000042" };
        SalesDbContext db = pg.CreateContext(customerId, orgId);
        StubNameLookup names = new();
        RecordingLedger ledger = new();
        StubBaseCurrency baseCurrency = new();

        InvoiceRepository invoices = new(
            db, tenant,
            new NumberGenerator(db, Options.Create(new NumberingOptions()), new StubFinancialYear()),
            baseCurrency, new StubBranchSettings(), new StubTaxRates(),
            names, names, new StubCurrentUser(), TimeProvider.System, new RecordingInventory(), ledger,
            new StubCreditCheck(), new StubDocumentStorage(), new StubInvoicePdf(), new StubOrgIdentity(), new StubUqcLookup(), new StubEInvoicing());

        return new Books(db, invoices, new RecurringBillingRepository(db, invoices, baseCurrency, TimeProvider.System), ledger);
    }

    private static async Task<(Guid CustomerId, Guid OrgId)> NewBranchAsync(PostgresFixture pg)
    {
        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        await using SalesDbContext db = pg.CreateContext(customerId, orgId);
        db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
        await db.SaveChangesAsync();
        return (customerId, orgId);
    }

    /// <summary>An invoice for 1,000 + 18% GST, due in 15 days.</summary>
    private static async Task<long> TemplateAsync(Books books)
    {
        InvoiceResult created = await books.Invoices.CreateAsync(new SaveInvoiceRequest
        {
            DocumentDate = new DateOnly(2026, 1, 31),
            DueDate = new DateOnly(2026, 2, 15),
            ContactId = CustomerContactId,
            PlaceOfSupplyStateCode = "33",
            Lines =
            [
                new SaveInvoiceLineRequest
                {
                    ItemId = 7001,
                    Description = "Monthly maintenance",
                    Quantity = 1m,
                    ConversionFactor = 1m,
                    UnitPrice = 1000m,
                    TaxGroupId = 1,
                    LineType = DocumentLineType.Stock,
                },
            ],
        }, default);

        Assert.Equal(InvoiceOutcome.Ok, created.Outcome);
        return created.InvoiceId;
    }

    private static SaveRecurringInvoiceScheduleRequest Monthly(long templateId, DateOnly start) => new()
    {
        Name = "Maintenance",
        TemplateInvoiceId = templateId,
        Frequency = RecurrenceFrequency.Monthly,
        Every = 1,
        StartDate = start,
        PlaceOfSupplyStateCode = "33",
    };

    [SkippableFact]
    public async Task A_due_schedule_creates_one_invoice_and_a_rerun_creates_none()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        (Guid customerId, Guid orgId) = await NewBranchAsync(_pg);
        Books books = Open(_pg, customerId, orgId);
        await using SalesDbContext _ = books.Db;

        long templateId = await TemplateAsync(books);
        RecurringScheduleResult schedule = await books.Schedules.CreateAsync(Monthly(templateId, new DateOnly(2026, 3, 1)), default);
        Assert.Equal(RecurringScheduleOutcome.Ok, schedule.Outcome);

        RunRecurringResponse first = await books.Schedules.RunDueAsync(new DateOnly(2026, 3, 1), default);
        RunRecurringResponse second = await books.Schedules.RunDueAsync(new DateOnly(2026, 3, 1), default);

        Assert.Equal(1, first.Created);
        Assert.Equal(0, second.Created);

        await using SalesDbContext check = _pg.CreateContext(customerId, orgId);
        RecurringInvoiceRun run = await check.RecurringInvoiceRuns.AsNoTracking().SingleAsync();
        Invoice raised = await check.Invoices.AsNoTracking().Include(i => i.Lines).SingleAsync(i => i.InvoiceId == run.InvoiceId);

        Assert.Equal(new DateOnly(2026, 3, 1), raised.DocumentDate);
        Assert.Equal(new DateOnly(2026, 3, 16), raised.DueDate);
        Assert.Equal(DocumentStatus.Draft, raised.Status);
        Assert.Equal("Monthly maintenance", Assert.Single(raised.Lines).Description);
        Assert.Equal(1180m, raised.TotalAmount);
        Assert.Equal(2, await check.Invoices.CountAsync());

        RecurringInvoiceSchedule after = await check.RecurringInvoiceSchedules.AsNoTracking().SingleAsync();
        Assert.Equal(new DateOnly(2026, 4, 1), after.NextRunDate);
        Assert.Equal(1, after.OccurrencesCreated);
    }

    [SkippableFact]
    public async Task Two_runs_at_once_raise_the_occurrence_once()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        (Guid customerId, Guid orgId) = await NewBranchAsync(_pg);
        Books setup = Open(_pg, customerId, orgId);
        await using (setup.Db)
        {
            long templateId = await TemplateAsync(setup);
            await setup.Schedules.CreateAsync(Monthly(templateId, new DateOnly(2026, 3, 1)), default);
        }

        Books a = Open(_pg, customerId, orgId);
        Books b = Open(_pg, customerId, orgId);
        await using SalesDbContext _1 = a.Db;
        await using SalesDbContext _2 = b.Db;

        RunRecurringResponse[] results = await Task.WhenAll(
            a.Schedules.RunDueAsync(new DateOnly(2026, 3, 1), default),
            b.Schedules.RunDueAsync(new DateOnly(2026, 3, 1), default));

        Assert.Equal(1, results.Sum(r => r.Created));

        await using SalesDbContext check = _pg.CreateContext(customerId, orgId);
        Assert.Equal(1, await check.RecurringInvoiceRuns.CountAsync());
        Assert.Equal(2, await check.Invoices.CountAsync());
    }

    [SkippableFact]
    public async Task A_late_run_catches_up_each_missed_occurrence_on_its_own_date()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        (Guid customerId, Guid orgId) = await NewBranchAsync(_pg);
        Books books = Open(_pg, customerId, orgId);
        await using SalesDbContext _ = books.Db;

        long templateId = await TemplateAsync(books);
        await books.Schedules.CreateAsync(Monthly(templateId, new DateOnly(2026, 1, 31)), default);

        RunRecurringResponse run = await books.Schedules.RunDueAsync(new DateOnly(2026, 4, 15), default);

        Assert.Equal(3, run.Created);

        await using SalesDbContext check = _pg.CreateContext(customerId, orgId);
        List<DateOnly> dates = await check.RecurringInvoiceRuns.AsNoTracking()
            .OrderBy(r => r.OccurrenceDate).Select(r => r.OccurrenceDate).ToListAsync();

        // Counted from the start date, so March is the 31st again, not the 28th.
        Assert.Equal([new DateOnly(2026, 1, 31), new DateOnly(2026, 2, 28), new DateOnly(2026, 3, 31)], dates);
    }

    [SkippableFact]
    public async Task Auto_post_posts_each_new_invoice()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        (Guid customerId, Guid orgId) = await NewBranchAsync(_pg);
        Books books = Open(_pg, customerId, orgId);
        await using SalesDbContext _ = books.Db;

        long templateId = await TemplateAsync(books);
        SaveRecurringInvoiceScheduleRequest request = Monthly(templateId, new DateOnly(2026, 3, 1));
        request.AutoPost = true;
        await books.Schedules.CreateAsync(request, default);

        RunRecurringResponse run = await books.Schedules.RunDueAsync(new DateOnly(2026, 3, 1), default);

        Assert.Equal(1, run.Created);
        Assert.Equal(1, run.Posted);

        await using SalesDbContext check = _pg.CreateContext(customerId, orgId);
        RecurringInvoiceRun raised = await check.RecurringInvoiceRuns.AsNoTracking().SingleAsync();
        Assert.True(raised.Posted);
        Assert.Equal(DocumentStatus.Posted, (await check.Invoices.AsNoTracking().SingleAsync(i => i.InvoiceId == raised.InvoiceId)).Status);
        Assert.Single(books.Ledger.Posts);
    }

    [SkippableFact]
    public async Task A_paused_schedule_skips_what_fell_due_while_it_was_paused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        (Guid customerId, Guid orgId) = await NewBranchAsync(_pg);
        Books books = Open(_pg, customerId, orgId);
        await using SalesDbContext _ = books.Db;

        long templateId = await TemplateAsync(books);
        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        RecurringScheduleResult created = await books.Schedules.CreateAsync(Monthly(templateId, today.AddMonths(-6)), default);

        await books.Schedules.PauseAsync(created.ScheduleId, default);
        Assert.Equal(0, (await books.Schedules.RunDueAsync(today, default)).Created);

        await books.Schedules.ResumeAsync(created.ScheduleId, default);

        await using SalesDbContext check = _pg.CreateContext(customerId, orgId);
        RecurringInvoiceSchedule after = await check.RecurringInvoiceSchedules.AsNoTracking().SingleAsync();
        Assert.True(after.NextRunDate >= today);
    }

    [SkippableFact]
    public async Task A_voided_or_till_invoice_cannot_be_a_template()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        (Guid customerId, Guid orgId) = await NewBranchAsync(_pg);
        Books books = Open(_pg, customerId, orgId);
        await using SalesDbContext _ = books.Db;

        long templateId = await TemplateAsync(books);
        Invoice template = await books.Db.Invoices.SingleAsync(i => i.InvoiceId == templateId);
        template.Status = DocumentStatus.Void;
        template.VoidedAt = DateTimeOffset.UtcNow;
        template.VoidReason = "Test void";
        await books.Db.SaveChangesAsync();

        RecurringScheduleResult result = await books.Schedules.CreateAsync(Monthly(templateId, new DateOnly(2026, 3, 1)), default);

        Assert.Equal(RecurringScheduleOutcome.Invalid, result.Outcome);
    }
}
