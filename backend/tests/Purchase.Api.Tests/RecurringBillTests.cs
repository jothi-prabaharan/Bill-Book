using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Purchase.Entity.Models;
using Purchase.Entity.TableEntities;
using Purchase.Repository;
using Purchase.Repository.Repositories;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;
using Shared.Kernel.Recurring;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Purchase.Api.Tests;

/// <summary>
/// Recurring bills (TK-139): a due schedule enters one bill, and a rerun enters
/// none. Each copy gets a vendor bill number of its own.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class RecurringBillTests
{
    private readonly PostgresFixture _pg;

    public RecurringBillTests(PostgresFixture pg) => _pg = pg;

    private sealed record Books(PurchaseDbContext Db, BillRepository Bills, RecurringBillingRepository Schedules);

    private static async Task<(Guid CustomerId, Guid OrgId)> NewBranchAsync(PostgresFixture pg)
    {
        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        await using PurchaseDbContext db = pg.CreateContext(customerId, orgId);
        db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
        await db.SaveChangesAsync();
        return (customerId, orgId);
    }

    private static Books Open(PostgresFixture pg, Guid customerId, Guid orgId)
    {
        TenantContext tenant = new() { CustomerId = customerId, OrgId = orgId };
        PurchaseDbContext db = pg.CreateContext(customerId, orgId);
        StubNameLookup names = new();
        StubBaseCurrency baseCurrency = new();

        BillRepository bills = new(
            db, tenant, new NumberGenerator(db, Options.Create(new NumberingOptions()), new StubFinancialYear()),
            baseCurrency, new StubBranchSettings(), new StubTaxRates(), names, names,
            new RecordingInventory(), new RecordingLedger(), new StubPaymentTerms(), new RecordingFixedAssets(),
            new StubCurrentUser(), TimeProvider.System);

        return new Books(db, bills, new RecurringBillingRepository(db, bills, baseCurrency, TimeProvider.System));
    }

    private static async Task<long> TemplateAsync(Books books)
    {
        BillResult created = await books.Bills.CreateAsync(new SaveBillRequest
        {
            DocumentDate = new DateOnly(2026, 1, 1),
            ContactId = 42,
            VendorBillNo = "RENT-JAN",
            VendorBillDate = new DateOnly(2026, 1, 1),
            DueDate = new DateOnly(2026, 1, 11),
            PlaceOfSupplyStateCode = "33",
            Lines =
            [
                new SaveBillLineRequest
                {
                    ItemId = 7,
                    Description = "Shop rent",
                    Quantity = 1m,
                    ConversionFactor = 1m,
                    UnitPrice = 50000m,
                    TaxGroupId = 1,
                    LineType = DocumentLineType.Stock,
                },
            ],
        }, default);

        Assert.Equal(BillOutcome.Ok, created.Outcome);
        return created.BillId;
    }

    [SkippableFact]
    public async Task A_due_schedule_enters_one_bill_and_a_rerun_enters_none()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        (Guid customerId, Guid orgId) = await NewBranchAsync(_pg);
        Books books = Open(_pg, customerId, orgId);
        await using PurchaseDbContext _ = books.Db;

        long templateId = await TemplateAsync(books);
        RecurringScheduleResult schedule = await books.Schedules.CreateAsync(new SaveRecurringBillScheduleRequest
        {
            Name = "Rent",
            TemplateBillId = templateId,
            Frequency = RecurrenceFrequency.Monthly,
            StartDate = new DateOnly(2026, 2, 1),
            VendorBillNoPrefix = "RENT-",
            PlaceOfSupplyStateCode = "33",
        }, default);
        Assert.Equal(RecurringScheduleOutcome.Ok, schedule.Outcome);

        RunRecurringResponse first = await books.Schedules.RunDueAsync(new DateOnly(2026, 3, 1), default);
        RunRecurringResponse second = await books.Schedules.RunDueAsync(new DateOnly(2026, 3, 1), default);

        Assert.Equal(2, first.Created);
        Assert.Equal(0, second.Created);

        await using PurchaseDbContext check = _pg.CreateContext(customerId, orgId);
        List<Bill> raised = await check.Bills.AsNoTracking()
            .Where(b => b.BillId != templateId)
            .OrderBy(b => b.DocumentDate)
            .ToListAsync();

        Assert.Equal(["RENT-20260201", "RENT-20260301"], raised.Select(b => b.VendorBillNo));
        Assert.Equal(new DateOnly(2026, 2, 11), raised[0].DueDate);
        Assert.All(raised, b => Assert.Equal(DocumentStatus.Draft, b.Status));
        Assert.Equal(2, await check.RecurringBillRuns.CountAsync());
    }

    [Fact]
    public void The_vendor_bill_number_is_the_prefix_and_the_date_within_fifty_characters()
    {
        Assert.Equal("REC-20261101", RecurringBillingRepository.VendorBillNo("REC-", new DateOnly(2026, 11, 1)));
        Assert.Equal(50, RecurringBillingRepository.VendorBillNo(new string('X', 60), new DateOnly(2026, 11, 1)).Length);
    }
}
