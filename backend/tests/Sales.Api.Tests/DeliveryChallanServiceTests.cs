using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sales.Api.Services;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// The delivery challan's save path — against a real PostgreSQL, the same
/// discipline <c>SalesOrderServiceTests</c> uses.
///
/// <b>Two bugs, both only visible from the door the controller uses.</b>
/// <c>SaveAsync</c> never set <c>BaseQuantity</c>, <c>TaxableAmount</c>,
/// <c>GrossAmount</c> or <c>LineNumber</c> on a line, so
/// <c>chk_deliverychallandetails_base_quantity</c> refused every save
/// outright — no delivery challan could be created at all. And the tax split
/// never branched on <c>IsInterState</c>: CGST and SGST were always half the
/// document total, with IGST never populated, so a cross-state challan would
/// have violated <c>chk_deliverychallans_tax_split</c> the moment the first
/// bug was fixed without the second one going with it.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class DeliveryChallanServiceTests
{
    private readonly PostgresFixture _pg;

    public DeliveryChallanServiceTests(PostgresFixture pg) => _pg = pg;

    [SkippableFact]
    public async Task An_intra_state_challan_saves_with_cgst_and_sgst_and_a_real_base_quantity()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        long id = await h.Service.SaveAsync(
            null,
            Request(contactGstin: "33AAAAA0000A1Z5", [Line(quantity: 10m, unitPrice: 100m)]),
            default);

        DeliveryChallan saved = await h.Db.DeliveryChallans
            .Include(x => x.Lines).ThenInclude(l => l.Taxes)
            .SingleAsync(x => x.DeliveryChallanId == id);

        Assert.False(saved.IsInterState);

        // 10 × 100 = 1000 taxable, 18% = 180 split 90 CGST / 90 SGST, total 1180.
        Assert.Equal(1000m, saved.TaxableAmount);
        Assert.Equal(90m, saved.CgstAmount);
        Assert.Equal(90m, saved.SgstAmount);
        Assert.Equal(0m, saved.IgstAmount);
        Assert.Equal(1180m, saved.TotalAmount);

        // The line itself — the four columns SaveAsync used to leave at zero.
        DeliveryChallanDetail line = saved.Lines.Single();
        Assert.Equal(1, line.LineNumber);
        Assert.Equal(10m, line.BaseQuantity);
        Assert.Equal(1000m, line.GrossAmount);
        Assert.Equal(1000m, line.TaxableAmount);

        Assert.Equal(2, line.Taxes.Count);
        Assert.Contains(line.Taxes, t => t.TaxComponent == TaxComponent.Cgst && t.Amount == 90m);
        Assert.Contains(line.Taxes, t => t.TaxComponent == TaxComponent.Sgst && t.Amount == 90m);
    }

    [SkippableFact]
    public async Task An_inter_state_challan_saves_with_igst_only()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        // 07 is Delhi; the branch is seeded at 33 (Tamil Nadu) — see StubBranchSettings.
        long id = await h.Service.SaveAsync(
            null,
            Request(contactGstin: "07AAAAA0000A1Z5", [Line(quantity: 5m, unitPrice: 200m)]),
            default);

        DeliveryChallan saved = await h.Db.DeliveryChallans
            .Include(x => x.Lines).ThenInclude(l => l.Taxes)
            .SingleAsync(x => x.DeliveryChallanId == id);

        Assert.True(saved.IsInterState);
        Assert.Equal(0m, saved.CgstAmount);
        Assert.Equal(0m, saved.SgstAmount);
        Assert.Equal(180m, saved.IgstAmount); // 1000 taxable × 18%
        Assert.Equal(1180m, saved.TotalAmount);

        DeliveryChallanDetailTax tax = Assert.Single(saved.Lines.Single().Taxes);
        Assert.Equal(TaxComponent.Igst, tax.TaxComponent);
        Assert.Equal(180m, tax.Amount);
    }

    [SkippableFact]
    public async Task A_second_line_is_numbered_rather_than_colliding_at_zero()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        long id = await h.Service.SaveAsync(
            null,
            Request(
                contactGstin: "33AAAAA0000A1Z5",
                [Line(quantity: 1m, unitPrice: 50m), Line(quantity: 2m, unitPrice: 25m)]),
            default);

        DeliveryChallan saved = await h.Db.DeliveryChallans
            .Include(x => x.Lines)
            .SingleAsync(x => x.DeliveryChallanId == id);

        Assert.Equal(
            [1, 2], saved.Lines.OrderBy(l => l.LineNumber).Select(l => l.LineNumber));
    }

    [SkippableFact]
    public async Task A_gstin_that_contradicts_the_stated_place_of_supply_is_refused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        SaveDeliveryChallanRequest request = Request(
            contactGstin: "33AAAAA0000A1Z5", [Line(1m, 100m)]);
        request.PlaceOfSupplyStateCode = "07"; // disagrees with the GSTIN's own state

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => h.Service.SaveAsync(null, request, default));

        Assert.Contains("is registered in state", ex.Message);

        // Refused before anything was queued for insert.
        Assert.Equal(0, await h.Db.DeliveryChallans.CountAsync());
    }

    private static SaveDeliveryChallanRequest Request(
        string contactGstin, List<SaveDeliveryChallanLineRequest> lines) =>
        new()
        {
            DocumentDate = new DateOnly(2026, 6, 1),
            ContactId = 42,
            ContactGstin = contactGstin,
            DispatchDate = new DateOnly(2026, 6, 1),
            ExchangeRate = 1m,
            Lines = lines,
        };

    private static SaveDeliveryChallanLineRequest Line(decimal quantity, decimal unitPrice) =>
        new()
        {
            ItemId = 7,
            Quantity = quantity,
            UnitPrice = unitPrice,
            TaxGroupIds = [1],
        };

    /// <summary>One branch, its numbering series seeded, and the service wired to stubs.</summary>
    private sealed record Harness(SalesDbContext Db, DeliveryChallanService Service)
    {
        public static async Task<Harness> CreateAsync(PostgresFixture pg)
        {
            Guid customerId = Guid.NewGuid();
            Guid orgId = Guid.NewGuid();

            SalesDbContext db = pg.CreateContext(customerId, orgId);

            db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
            await db.SaveChangesAsync();

            StubNameLookup names = new();
            NumberGenerator numbering = new(
                db, Options.Create(new NumberingOptions()), new StubFinancialYear());

            DeliveryChallanService service = new(
                db,
                new TenantContext { CustomerId = customerId, OrgId = orgId },
                numbering,
                new StubBaseCurrency(),
                new StubBranchSettings(),
                new StubTaxRates(),
                names,
                names,
                new StubCurrentUser(),
                TimeProvider.System,
                new RecordingInventory(),
                new RecordingLedger());

            return new Harness(db, service);
        }
    }
}
