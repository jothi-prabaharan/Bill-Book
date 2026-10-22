using BillBook.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Sales.Repository.Repositories;
using Shared.Kernel.Documents;
using Shared.Kernel.Licensing;
using Shared.Kernel.Numbering;
using Shared.Kernel.Recurring;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// A Professional or trial branch may create 1,000 Sales documents a month
/// (D-69, TK-210). The count is taken by <c>DocumentQuotaInterceptor</c> on every
/// insert of a quote, sales order, invoice or credit note, inside the document's
/// own transaction, against a real PostgreSQL: the guarantees that matter are a
/// guarded <c>UPDATE</c>'s row count, a row lock, and a unique index, none of which
/// an in-memory provider has.
///
/// Written for TK-210 and not run by the AI that wrote it (docs/Task_Details.md 0.5).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class MonthlyQuotaTests
{
    private static readonly DateTimeOffset MidOctober = new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly October = new(2026, 10, 1);
    private static readonly DateOnly Today = new(2026, 10, 15);

    private readonly PostgresFixture _pg;

    public MonthlyQuotaTests(PostgresFixture pg) => _pg = pg;

    private QuotaHarness<SalesDbContext> Harness(int? sales, int? purchase = null, DateTimeOffset? now = null) =>
        new(_pg.ConnectionString, PlanLimitKind.SalesTransactions, UsageRepository.CountedDocuments,
            new PlanLimits("Professional", sales, purchase), now ?? MidOctober);

    private static string Number() => $"Q/{Guid.NewGuid():N}"[..20];

    private static Quote NewQuote() => new()
    {
        TransactionTypeCode = "QTE",
        DocumentNo = Number(),
        DocumentDate = Today,
        ValidUntil = Today.AddDays(30),
        ContactId = 1,
        CurrencyCode = "INR",
        ExchangeRate = 1m,
        Status = DocumentStatus.Draft,
    };

    private static SalesOrder NewOrder() => new()
    {
        TransactionTypeCode = "SOR",
        DocumentNo = Number(),
        DocumentDate = Today,
        ContactId = 1,
        CurrencyCode = "INR",
        ExchangeRate = 1m,
        Status = DocumentStatus.Draft,
    };

    private static Invoice NewInvoice(string type = "INV") => new()
    {
        TransactionTypeCode = type,
        DocumentNo = Number(),
        DocumentDate = Today,
        DueDate = Today,
        ContactId = 1,
        CurrencyCode = "INR",
        ExchangeRate = 1m,
        Status = DocumentStatus.Draft,
        // A till sale is an invoice row that also names its till and how it was paid.
        TillId = type == "POS" ? 1 : null,
        PaymentMode = type == "POS" ? "Cash" : null,
    };

    private static DeliveryChallan NewChallan() => new()
    {
        TransactionTypeCode = "DLC",
        DocumentNo = Number(),
        DocumentDate = Today,
        ContactId = 1,
        CurrencyCode = "INR",
        ExchangeRate = 1m,
        Status = DocumentStatus.Draft,
    };

    private static Task Quotes(SalesDbContext db, int count)
    {
        for (int i = 0; i < count; i++)
        {
            db.Quotes.Add(NewQuote());
        }

        return Task.CompletedTask;
    }

    [SkippableFact]
    public async Task QUOTA_S01_every_counted_sales_document_takes_a_place_and_a_delivery_challan_does_not()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = (Guid.NewGuid(), Guid.NewGuid());
        await using QuotaHarness<SalesDbContext> harness = Harness(sales: 100);

        Assert.Equal("ok", await harness.TryAsync(customerId, orgId, db => { db.Quotes.Add(NewQuote()); return Task.CompletedTask; }));
        Assert.Equal("ok", await harness.TryAsync(customerId, orgId, db => { db.SalesOrders.Add(NewOrder()); return Task.CompletedTask; }));
        Assert.Equal("ok", await harness.TryAsync(customerId, orgId, db => { db.Invoices.Add(NewInvoice("INV")); return Task.CompletedTask; }));
        Assert.Equal("ok", await harness.TryAsync(customerId, orgId, db => { db.Invoices.Add(NewInvoice("POS")); return Task.CompletedTask; }));
        Assert.Equal(4, await harness.UsedAsync(customerId, orgId, October));

        // A credit note is against an invoice, which is already in the books.
        long invoiceId = await harness.RequestAsync(customerId, orgId, db => db.Invoices.Select(i => i.InvoiceId).FirstAsync(), inTransaction: false);
        Assert.Equal("ok", await harness.TryAsync(customerId, orgId, db =>
        {
            db.CreditNotes.Add(new CreditNote
            {
                TransactionTypeCode = "CRN",
                DocumentNo = Number(),
                DocumentDate = Today,
                InvoiceId = invoiceId,
                ContactId = 1,
                CurrencyCode = "INR",
                ExchangeRate = 1m,
                Status = DocumentStatus.Draft,
            });
            return Task.CompletedTask;
        }));
        Assert.Equal(5, await harness.UsedAsync(customerId, orgId, October));

        // Not counted: a delivery challan, or an edit to a document already there.
        Assert.Equal("ok", await harness.TryAsync(customerId, orgId, db => { db.DeliveryChallans.Add(NewChallan()); return Task.CompletedTask; }));
        await harness.RequestAsync(customerId, orgId, async db =>
        {
            (await db.Quotes.FirstAsync()).Notes = "edited";
            await db.SaveChangesAsync();
            return 0;
        });
        Assert.Equal(5, await harness.UsedAsync(customerId, orgId, October));
    }

    [SkippableFact]
    public async Task QUOTA_S02_the_thousand_and_first_document_is_refused_with_the_plans_words()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = (Guid.NewGuid(), Guid.NewGuid());
        await using QuotaHarness<SalesDbContext> harness = Harness(sales: 1000);

        Assert.Equal("ok", await harness.TryAsync(customerId, orgId, db => Quotes(db, 1000)));
        Assert.Equal(1000, await harness.UsedAsync(customerId, orgId, October));

        PlanLimitReachedException refusal = await Assert.ThrowsAsync<PlanLimitReachedException>(() =>
            harness.RequestAsync(customerId, orgId, async db =>
            {
                db.Quotes.Add(NewQuote());
                await db.SaveChangesAsync();
                return 0;
            }));

        Assert.Equal(
            "Professional plan Sales transaction limit reached (maximum 1,000 Sales transactions this month). "
            + "Upgrade to the Elite Plan for unlimited transactions.",
            refusal.Message);
        Assert.Equal(PlanLimitResponse.ReachedCode, refusal.Limit.Code);
        Assert.Equal((PlanLimitKind.SalesTransactions, "Professional", 1000, 1000),
            (refusal.Limit.Kind, refusal.Limit.Plan, refusal.Limit.Limit, refusal.Limit.Used));

        // Nothing was written for the refused one.
        Assert.Equal(1000, await harness.RequestAsync(customerId, orgId, db => db.Quotes.CountAsync(), inTransaction: false));
        Assert.Equal(1000, await harness.UsedAsync(customerId, orgId, October));
    }

    [SkippableFact]
    public async Task QUOTA_S03_only_the_sales_limit_applies_to_sales_so_a_full_purchase_month_stops_nothing_here()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = (Guid.NewGuid(), Guid.NewGuid());
        await using QuotaHarness<SalesDbContext> harness = Harness(sales: 3, purchase: 0);

        for (int i = 0; i < 3; i++)
        {
            Assert.Equal("ok", await harness.TryAsync(customerId, orgId, db => Quotes(db, 1)));
        }

        Assert.Equal("refused:3/3", await harness.TryAsync(customerId, orgId, db => Quotes(db, 1)));
    }

    [SkippableFact]
    public async Task QUOTA_S04_a_document_that_does_not_save_gives_its_place_back()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = (Guid.NewGuid(), Guid.NewGuid());
        await using QuotaHarness<SalesDbContext> harness = Harness(sales: 3);
        Assert.Equal("ok", await harness.TryAsync(customerId, orgId, db => Quotes(db, 1)));

        // The request fails after the save, so its transaction is rolled back.
        await harness.RequestAsync(customerId, orgId, async db =>
        {
            db.Quotes.Add(NewQuote());
            await db.SaveChangesAsync();
            return 0;
        }, commit: false);
        Assert.Equal(1, await harness.UsedAsync(customerId, orgId, October));

        // And with no transaction open, a save that fails leaves the count alone: the
        // interceptor opens one for the save and rolls it back with the document.
        await Assert.ThrowsAsync<DbUpdateException>(() => harness.TryAsync(customerId, orgId, db =>
        {
            Quote bad = NewQuote();
            bad.DocumentNo = new string('x', 500);
            db.Quotes.Add(bad);
            return Task.CompletedTask;
        }, inTransaction: false));
        Assert.Equal(1, await harness.UsedAsync(customerId, orgId, October));

        // With no transaction open a save that works is counted and kept.
        Assert.Equal("ok", await harness.TryAsync(customerId, orgId, db => Quotes(db, 1), inTransaction: false));
        Assert.Equal(2, await harness.UsedAsync(customerId, orgId, October));
    }

    [SkippableFact]
    public async Task QUOTA_S05_documents_saved_together_are_counted_together_and_refused_together()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = (Guid.NewGuid(), Guid.NewGuid());
        await using QuotaHarness<SalesDbContext> harness = Harness(sales: 3);

        Assert.Equal("ok", await harness.TryAsync(customerId, orgId, db => Quotes(db, 2)));
        Assert.Equal(2, await harness.UsedAsync(customerId, orgId, October));

        // One place is left, and two documents want it: neither is saved.
        Assert.Equal("refused:2/3", await harness.TryAsync(customerId, orgId, db => Quotes(db, 2)));
        Assert.Equal(2, await harness.UsedAsync(customerId, orgId, October));
        Assert.Equal(2, await harness.RequestAsync(customerId, orgId, db => db.Quotes.CountAsync(), inTransaction: false));
    }

    [SkippableFact]
    public async Task QUOTA_S06_requests_racing_for_a_new_months_places_are_counted_once_each()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = (Guid.NewGuid(), Guid.NewGuid());
        await using QuotaHarness<SalesDbContext> harness = Harness(sales: 3);

        // Six at once, on a branch with no row for the month yet: they race to make it,
        // the loser of that race finds it there, and exactly three places are given.
        string[] answers = await Task.WhenAll(
            Enumerable.Range(0, 6).Select(_ => harness.TryAsync(customerId, orgId, db => Quotes(db, 1))));

        Assert.Equal(3, answers.Count(a => a == "ok"));
        Assert.Equal(3, answers.Count(a => a == "refused:3/3"));
        Assert.Equal(3, await harness.UsedAsync(customerId, orgId, October));
        Assert.Equal(3, await harness.RequestAsync(customerId, orgId, db => db.Quotes.CountAsync(), inTransaction: false));
    }

    [SkippableFact]
    public async Task QUOTA_S07_requests_racing_for_the_last_place_give_it_to_one()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = (Guid.NewGuid(), Guid.NewGuid());
        await using QuotaHarness<SalesDbContext> harness = Harness(sales: 3);
        await harness.TryAsync(customerId, orgId, db => Quotes(db, 2));

        string[] answers = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => harness.TryAsync(customerId, orgId, db => Quotes(db, 1))));

        Assert.Equal(1, answers.Count(a => a == "ok"));
        Assert.Equal(3, await harness.UsedAsync(customerId, orgId, October));
    }

    [SkippableFact]
    public async Task QUOTA_S08_the_month_turns_at_midnight_in_india_and_each_month_starts_from_none()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = (Guid.NewGuid(), Guid.NewGuid());
        await using QuotaHarness<SalesDbContext> harness = Harness(sales: 3);

        // 23:59 IST on 31 October.
        harness.SetNow(new DateTimeOffset(2026, 10, 31, 18, 29, 0, TimeSpan.Zero));
        Assert.Equal("ok", await harness.TryAsync(customerId, orgId, db => Quotes(db, 3)));
        Assert.Equal("refused:3/3", await harness.TryAsync(customerId, orgId, db => Quotes(db, 1)));

        // 00:00 IST on 1 November: a new month, with its own three.
        harness.SetNow(new DateTimeOffset(2026, 10, 31, 18, 30, 0, TimeSpan.Zero));
        Assert.Equal("ok", await harness.TryAsync(customerId, orgId, db => Quotes(db, 1)));
        Assert.Equal(1, await harness.UsedAsync(customerId, orgId, new DateOnly(2026, 11, 1)));
        Assert.Equal(3, await harness.UsedAsync(customerId, orgId, October));
    }

    [SkippableFact]
    public async Task QUOTA_S09_a_plan_with_no_limit_and_one_that_cannot_be_read_are_never_counted_or_refused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = (Guid.NewGuid(), Guid.NewGuid());
        await using QuotaHarness<SalesDbContext> harness = Harness(sales: null);

        for (int i = 0; i < 5; i++)
        {
            Assert.Equal("ok", await harness.TryAsync(customerId, orgId, db => Quotes(db, 1)));
        }

        // Elite, which has no limit: nothing counted, so nothing to lock either.
        Assert.Null(await harness.UsedAsync(customerId, orgId, October));

        // Master cannot be reached: unknown is not a limit.
        harness.SetLimits(null);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal("ok", await harness.TryAsync(customerId, orgId, db => Quotes(db, 1)));
        }

        Assert.Null(await harness.UsedAsync(customerId, orgId, October));
        Assert.Equal(10, await harness.RequestAsync(customerId, orgId, db => db.Quotes.CountAsync(), inTransaction: false));
    }

    [SkippableFact]
    public async Task QUOTA_S10_each_branch_is_counted_apart()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        (Guid first, Guid second) = (Guid.NewGuid(), Guid.NewGuid());
        await using QuotaHarness<SalesDbContext> harness = Harness(sales: 2);

        Assert.Equal("ok", await harness.TryAsync(customerId, first, db => Quotes(db, 2)));
        Assert.Equal("refused:2/2", await harness.TryAsync(customerId, first, db => Quotes(db, 1)));

        // The same customer's other branch has its own two.
        Assert.Equal("ok", await harness.TryAsync(customerId, second, db => Quotes(db, 2)));
        Assert.Equal(2, await harness.UsedAsync(customerId, second, October));
    }

    [SkippableFact]
    public async Task QUOTA_S11_the_usage_read_says_what_is_used_what_is_allowed_and_what_is_left()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = (Guid.NewGuid(), Guid.NewGuid());
        await using QuotaHarness<SalesDbContext> harness = Harness(sales: 1000);
        await harness.TryAsync(customerId, orgId, db => Quotes(db, 40));

        MonthlyUsageResponse usage = await harness.RequestAsync(customerId, orgId,
            db => new UsageRepository(db, harness.Limits, harness.Clock).ReadAsync(default), inTransaction: false);

        Assert.Equal((PlanLimitKind.SalesTransactions, "Professional", October, 40, 1000, 960),
            (usage.Kind, usage.Plan, usage.PeriodStart, usage.Used, usage.Limit, usage.Remaining));

        // No limit: the count is not kept and there is nothing left to count down.
        harness.SetLimits(new PlanLimits("Elite", null, null));
        MonthlyUsageResponse elite = await harness.RequestAsync(customerId, orgId,
            db => new UsageRepository(db, harness.Limits, harness.Clock).ReadAsync(default), inTransaction: false);

        Assert.Null(elite.Limit);
        Assert.Null(elite.Remaining);
    }

    [SkippableFact]
    public async Task QUOTA_S12_a_due_recurring_invoice_is_skipped_at_the_limit_and_raised_once_a_place_is_free()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = (Guid.NewGuid(), Guid.NewGuid());
        await using QuotaHarness<SalesDbContext> harness = Harness(sales: 2);

        await harness.RequestAsync(customerId, orgId, async db =>
        {
            db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
            await db.SaveChangesAsync();
            return 0;
        }, inTransaction: false);

        RunRecurringResponse skipped = new();
        RunRecurringResponse raised = new();
        string? lastError = null;
        DateOnly? nextRun = null;

        await harness.RequestAsync(customerId, orgId, async db =>
        {
            TenantContext tenant = new() { CustomerId = customerId, OrgId = orgId, CustomerCode = "0000000042" };
            StubNameLookup names = new();
            StubBaseCurrency baseCurrency = new();
            InvoiceRepository invoices = new(
                db, tenant,
                new NumberGenerator(db, Options.Create(new NumberingOptions()), new StubFinancialYear()),
                baseCurrency, new StubBranchSettings(), new StubTaxRates(),
                names, names, new StubCurrentUser(), TimeProvider.System, new RecordingInventory(), new RecordingLedger(),
                new StubCreditCheck(), new StubDocumentStorage(), new StubInvoicePdf(), new StubOrgIdentity(), new StubUqcLookup(), new StubEInvoicing());
            RecurringBillingRepository schedules = new(db, invoices, baseCurrency, TimeProvider.System);

            // The template is the month's first document; the March occurrence is the second.
            InvoiceResult template = await invoices.CreateAsync(new SaveInvoiceRequest
            {
                DocumentDate = new DateOnly(2026, 1, 31),
                DueDate = new DateOnly(2026, 2, 15),
                ContactId = 42,
                PlaceOfSupplyStateCode = "33",
                Lines =
                [
                    new SaveInvoiceLineRequest
                    {
                        ItemId = 7001, Description = "Monthly maintenance", Quantity = 1m, ConversionFactor = 1m,
                        UnitPrice = 1000m, TaxGroupId = 1, LineType = DocumentLineType.Stock,
                    },
                ],
            }, default);
            Assert.Equal(InvoiceOutcome.Ok, template.Outcome);

            RecurringScheduleResult schedule = await schedules.CreateAsync(new SaveRecurringInvoiceScheduleRequest
            {
                Name = "Maintenance",
                TemplateInvoiceId = template.InvoiceId,
                Frequency = RecurrenceFrequency.Monthly,
                Every = 1,
                StartDate = new DateOnly(2026, 3, 1),
                PlaceOfSupplyStateCode = "33",
            }, default);
            Assert.Equal(RecurringScheduleOutcome.Ok, schedule.Outcome);

            Assert.Equal(1, (await schedules.RunDueAsync(new DateOnly(2026, 3, 1), default)).Created);

            // The month is full: April's occurrence is skipped, not failed, and stays due.
            skipped = await schedules.RunDueAsync(new DateOnly(2026, 4, 1), default);
            db.ChangeTracker.Clear();
            RecurringInvoiceSchedule row = await db.RecurringInvoiceSchedules.AsNoTracking().SingleAsync();
            lastError = row.LastError;
            nextRun = row.NextRunDate;

            // A place is free: the skipped occurrence is raised on the next run.
            harness.SetLimits(new PlanLimits("Professional", 5, null));
            raised = await schedules.RunDueAsync(new DateOnly(2026, 4, 1), default);
            return 0;
        }, inTransaction: false);

        Assert.Equal((0, 0, 1), (skipped.Created, skipped.Failed, skipped.Skipped));
        Assert.Contains("Sales transaction limit reached", lastError);
        Assert.Equal(new DateOnly(2026, 4, 1), nextRun);
        Assert.Equal(1, raised.Created);
    }
}
