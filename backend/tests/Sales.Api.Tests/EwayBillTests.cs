using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Sales.Api.Services.EInvoicing;
using Sales.Api.Services.Printing;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Contacts;
using Shared.Kernel.Documents;
using Shared.Kernel.Persistence;
using Shared.Kernel.Printing;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>E-way bills (TK-93): the pure rules, and the service against a real database and the sandbox.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class EwayBillTests
{
    private static readonly DateOnly Dated = new(2026, 9, 20);

    private readonly PostgresFixture _postgres;

    public EwayBillTests(PostgresFixture postgres) => _postgres = postgres;

    // ---- Pure --------------------------------------------------------------

    [Theory]
    [InlineData(ChallanType.Sale, 1)]
    [InlineData(ChallanType.JobWork, 4)]
    [InlineData(ChallanType.BranchTransfer, 5)]
    [InlineData(ChallanType.Approval, 8)]
    [InlineData(ChallanType.Sample, 8)]
    public void A_challans_type_decides_why_the_goods_move(ChallanType type, int subSupply) =>
        Assert.Equal(subSupply, EwayBillMapper.SubSupplyTypeFor(type));

    [Fact]
    public void Part_b_needs_a_vehicle_or_a_transporter()
    {
        Assert.Contains(EwayBillMapper.ValidateTransport(new EwayTransport(TransportMode.Road, 100, null, null, null)), p => p.Code == "PART_B");
        Assert.Empty(EwayBillMapper.ValidateTransport(new EwayTransport(TransportMode.Road, 100, "TN01AB1234", null, null)));
        Assert.Empty(EwayBillMapper.ValidateTransport(new EwayTransport(TransportMode.Road, 100, null, EInvoiceMapperTests.InterBuyerGstin, "Fast Freight")));
    }

    [Theory]
    [InlineData("TN01AB1234", true)]
    [InlineData("KA05M1234", true)]
    [InlineData("TN-01", false)]
    [InlineData("1234", false)]
    public void A_road_vehicle_number_has_the_portals_shape(string vehicle, bool valid) =>
        Assert.Equal(valid, !EwayBillMapper.ValidateTransport(new EwayTransport(TransportMode.Road, 10, vehicle, null, null)).Any());

    [Fact]
    public void The_transport_block_carries_the_portals_mode_code()
    {
        Inv01EwayBill details = EwayBillMapper.Details(new EwayTransport(TransportMode.Rail, 450, null, "29AAGCB7383J1Z4", "Rail Co"));

        Assert.Equal("2", details.TransMode);
        Assert.Equal(450, details.Distance);
        Assert.Null(details.VehType);
    }

    // ---- The service -------------------------------------------------------

    [SkippableFact]
    public async Task A_challan_under_the_limit_asks_for_nothing()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using Harness h = Harness.Start(_postgres);
        DeliveryChallan challan = await h.AddChallanAsync(total: 40_000m);

        EwayBillResult result = await h.Service.GenerateAsync(EwayBillSource.DeliveryChallan, challan.DeliveryChallanId, Road(), default);

        Assert.Equal(EwayBillOutcome.Refused, result.Outcome);
        Assert.Contains("no e-way bill is needed", result.Detail);
        Assert.Equal(0, await h.Db.EwayBills.CountAsync());
        Assert.Empty(h.AfterCommit.Work);
    }

    [SkippableFact]
    public async Task A_challan_over_the_limit_gets_a_number_from_the_sandbox_and_prints_it()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using Harness h = Harness.Start(_postgres);
        DeliveryChallan challan = await h.AddChallanAsync(total: 118_000m);

        EwayBillResult result = await h.Service.GenerateAsync(EwayBillSource.DeliveryChallan, challan.DeliveryChallanId, Road(), default);
        Assert.Equal(EwayBillOutcome.Ok, result.Outcome);
        Assert.Equal(EwayBillStatus.Pending, result.EwayBill!.Status);

        await h.AfterCommit.RunAsync();

        Assert.Equal(EwayBillStatus.Generated, result.EwayBill.Status);
        Assert.Matches("^[0-9]{12}$", result.EwayBill.EwbNo);
        Assert.Equal(EwayBillOrigin.Standalone, result.EwayBill.Origin);
        DeliveryChallan saved = await h.Db.DeliveryChallans.AsNoTracking().Include(c => c.Lines).SingleAsync();
        Assert.Equal(result.EwayBill.EwbNo, saved.EwayBillNo);
        Assert.Equal("NOS", saved.Lines[0].UqcCode);
        // Its filed PDF is written again with the bill on it.
        Assert.Contains(h.Pdf.Rendered, m => m.Reference is string r && r.Contains(result.EwayBill.EwbNo!));
    }

    [SkippableFact]
    public async Task A_branch_that_does_not_generate_e_way_bills_is_refused()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using Harness h = Harness.Start(_postgres, ewayEnabled: false);
        DeliveryChallan challan = await h.AddChallanAsync(total: 118_000m);

        EwayBillResult result = await h.Service.GenerateAsync(EwayBillSource.DeliveryChallan, challan.DeliveryChallanId, Road(), default);

        Assert.Equal(EwayBillOutcome.Refused, result.Outcome);
        Assert.Contains("Settings", result.Detail);
    }

    [SkippableFact]
    public async Task A_second_bill_while_one_is_live_is_refused()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using Harness h = Harness.Start(_postgres);
        DeliveryChallan challan = await h.AddChallanAsync(total: 118_000m);
        await h.GeneratedAsync(challan);

        EwayBillResult again = await h.Service.GenerateAsync(EwayBillSource.DeliveryChallan, challan.DeliveryChallanId, Road(), default);

        Assert.Equal(EwayBillOutcome.Refused, again.Outcome);
        Assert.Contains("Cancel it", again.Detail);
    }

    [SkippableFact]
    public async Task A_cancel_within_24_hours_cancels_and_clears_the_challan()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using Harness h = Harness.Start(_postgres);
        DeliveryChallan challan = await h.AddChallanAsync(total: 118_000m);
        await h.GeneratedAsync(challan);

        h.Clock.Advance(TimeSpan.FromHours(23));
        EwayBillResult result = await h.Service.CancelAsync(EwayBillSource.DeliveryChallan, challan.DeliveryChallanId,
            new CancelEwayBillRequest { Reason = EwayBillCancelReason.DataEntryMistake, Remark = "Wrong vehicle" }, default);

        Assert.Equal(EwayBillOutcome.Ok, result.Outcome);
        Assert.Equal(EwayBillStatus.Cancelled, result.EwayBill!.Status);
        Assert.Null((await h.Db.DeliveryChallans.AsNoTracking().SingleAsync()).EwayBillNo);
    }

    [SkippableFact]
    public async Task A_cancel_after_24_hours_is_refused()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using Harness h = Harness.Start(_postgres);
        DeliveryChallan challan = await h.AddChallanAsync(total: 118_000m);
        await h.GeneratedAsync(challan);

        h.Clock.Advance(TimeSpan.FromHours(25));
        EwayBillResult result = await h.Service.CancelAsync(EwayBillSource.DeliveryChallan, challan.DeliveryChallanId,
            new CancelEwayBillRequest { Remark = "Late" }, default);

        Assert.Equal(EwayBillOutcome.Refused, result.Outcome);
        Assert.Contains("24 hours", result.Detail);
    }

    [SkippableFact]
    public async Task Part_b_changes_the_vehicle_on_a_live_bill()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using Harness h = Harness.Start(_postgres);
        DeliveryChallan challan = await h.AddChallanAsync(total: 118_000m);
        await h.GeneratedAsync(challan);

        EwayBillResult result = await h.Service.UpdatePartBAsync(EwayBillSource.DeliveryChallan, challan.DeliveryChallanId,
            new UpdatePartBRequest { VehicleNo = "ka05m 1234", FromPlace = "Hosur", FromStateCode = "33", Reason = "Breakdown" }, default);

        Assert.Equal(EwayBillOutcome.Ok, result.Outcome);
        Assert.Equal("KA05M1234", result.EwayBill!.VehicleNo);
    }

    [SkippableFact]
    public async Task A_typed_number_becomes_a_manual_row_and_clearing_it_removes_the_row()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using Harness h = Harness.Start(_postgres);
        DeliveryChallan challan = await h.AddChallanAsync(total: 118_000m, status: DocumentStatus.Draft);

        challan.EwayBillNo = "331000000001";
        challan.EwayBillDate = Dated;
        await EwayBillService.SyncManualAsync(h.Db, challan, default);

        EwayBill manual = await h.Db.EwayBills.AsNoTracking().SingleAsync();
        Assert.Equal(EwayBillOrigin.Manual, manual.Origin);
        Assert.Equal(EwayBillStatus.Generated, manual.Status);
        Assert.Equal("331000000001", manual.EwbNo);

        challan.EwayBillNo = null;
        await EwayBillService.SyncManualAsync(h.Db, challan, default);
        Assert.Equal(0, await h.Db.EwayBills.CountAsync());
    }

    [SkippableFact]
    public async Task An_invoice_with_its_irn_goes_by_irn()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using Harness h = Harness.Start(_postgres);
        Invoice invoice = await h.AddInvoiceAsync();
        h.Db.EInvoices.Add(new EInvoice { SourceType = EInvoiceSource.Invoice, SourceId = invoice.InvoiceId, Status = EInvoiceStatus.Registered, Irn = "x" });
        await h.Db.SaveChangesAsync();

        EwayBillResult result = await h.Service.GenerateAsync(EwayBillSource.Invoice, invoice.InvoiceId, Road(), default);

        Assert.Equal(EwayBillOrigin.ByIrn, result.EwayBill!.Origin);
    }

    [Fact]
    public void A_live_e_way_bill_reaches_the_invoice_payload()
    {
        var payload = new PrintPayload();
        InvoicePrintService.AddEwayBill(payload, new EwayBill
        {
            EwbNo = "331000000001",
            EwbDate = new DateTimeOffset(2026, 9, 20, 5, 0, 0, TimeSpan.Zero),
            ValidUntil = new DateTimeOffset(2026, 9, 22, 5, 0, 0, TimeSpan.Zero),
        });

        Assert.Equal("331000000001", payload.Singles["EInvoice.EwbNo"]);
        Assert.Equal(new DateOnly(2026, 9, 22), payload.Singles["EInvoice.EwbValidUntil"]);
    }

    private static GenerateEwayBillRequest Road() => new() { TransportMode = TransportMode.Road, DistanceKm = 450, VehicleNo = "tn01ab1234" };

    // ---- Harness -------------------------------------------------------------

    private sealed class Harness : IAsyncDisposable
    {
        public required SalesDbContext Db { get; init; }
        public required EwayBillService Service { get; init; }
        public required HeldAfterCommit AfterCommit { get; init; }
        public required RecordingSalesPdf Pdf { get; init; }
        public required ManualClock Clock { get; init; }

        public static Harness Start(PostgresFixture postgres, bool ewayEnabled = true)
        {
            var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid(), CustomerCode = "0000000042" };
            SalesDbContext db = postgres.CreateContext(tenant.CustomerId.Value, tenant.OrgId.Value);
            var clock = new ManualClock(new DateTimeOffset(2026, 9, 21, 4, 0, 0, TimeSpan.Zero));
            var identity = new StubOrgIdentity(new OrgIdentity(
                "Test Traders", EInvoiceMapperTests.SellerGstin, "1 Test Street", null, "Chennai", "33", "600001"));
            var pdf = new RecordingSalesPdf();
            var archive = new Sales.Api.Services.Pdf.SalesDocumentArchive(
                db, tenant, new RecordingDocumentStorage(), identity, new StubNameLookup(), new StubNameLookup(), pdf);
            var afterCommit = new HeldAfterCommit();

            return new Harness
            {
                Db = db,
                AfterCommit = afterCommit,
                Pdf = pdf,
                Clock = clock,
                Service = new EwayBillService(
                    db,
                    new Branch(ewayEnabled),
                    identity,
                    new Book(),
                    new StubNameLookup(),
                    new StubUqcLookup(),
                    new SandboxEInvoiceGateway(clock),
                    afterCommit,
                    archive,
                    clock,
                    NullLogger<EwayBillService>.Instance),
            };
        }

        public async Task<DeliveryChallan> AddChallanAsync(decimal total, DocumentStatus status = DocumentStatus.Posted)
        {
            decimal taxable = Math.Round(total / 1.18m, 2);
            decimal half = Math.Round((total - taxable) / 2, 2);
            var challan = new DeliveryChallan
            {
                TransactionTypeCode = "DLC",
                DocumentNo = $"DLC/{Guid.NewGuid():N}"[..14],
                DocumentDate = Dated,
                DispatchDate = Dated,
                ContactId = 42,
                ContactGstin = EInvoiceMapperTests.IntraBuyerGstin,
                CurrencyCode = "INR",
                SubTotal = taxable,
                TaxableAmount = taxable,
                CgstAmount = half,
                SgstAmount = half,
                RoundOffAmount = total - taxable - (2 * half),
                TotalAmount = total,
                Status = status,
                PostedAt = status == DocumentStatus.Posted ? DateTimeOffset.UtcNow : null,
                Lines =
                [
                    new DeliveryChallanDetail
                    {
                        LineNumber = 1,
                        ItemId = 7,
                        HsnSacCode = "72142090",
                        Quantity = 10m,
                        BaseQuantity = 10m,
                        UomId = 1,
                        UnitPrice = taxable / 10m,
                        GrossAmount = taxable,
                        TaxableAmount = taxable,
                        TaxAmount = 2 * half,
                        LineTotal = taxable + (2 * half),
                        Taxes =
                        [
                            new DeliveryChallanDetailTax { TaxComponent = TaxComponent.Cgst, SubAccountId = 1, Rate = 9m, TaxableAmount = taxable, Amount = half, AmountBase = half },
                            new DeliveryChallanDetailTax { TaxComponent = TaxComponent.Sgst, SubAccountId = 1, Rate = 9m, TaxableAmount = taxable, Amount = half, AmountBase = half },
                        ],
                    },
                ],
            };
            Db.DeliveryChallans.Add(challan);
            await Db.SaveChangesAsync();
            return challan;
        }

        public async Task<Invoice> AddInvoiceAsync()
        {
            var invoice = new Invoice
            {
                TransactionTypeCode = "INV",
                DocumentNo = $"INV/{Guid.NewGuid():N}"[..14],
                DocumentDate = Dated,
                DueDate = Dated.AddDays(30),
                ContactId = 42,
                ContactGstin = EInvoiceMapperTests.IntraBuyerGstin,
                CurrencyCode = "INR",
                SubTotal = 100_000m,
                TaxableAmount = 100_000m,
                CgstAmount = 9_000m,
                SgstAmount = 9_000m,
                TotalAmount = 118_000m,
                Status = DocumentStatus.Posted,
                PostedAt = DateTimeOffset.UtcNow,
                Lines =
                [
                    new InvoiceDetail
                    {
                        LineNumber = 1, ItemId = 7, HsnSacCode = "72142090", Quantity = 10m, BaseQuantity = 10m,
                        UnitPrice = 10_000m, GrossAmount = 100_000m, TaxableAmount = 100_000m, TaxAmount = 18_000m, LineTotal = 118_000m,
                    },
                ],
            };
            Db.Invoices.Add(invoice);
            await Db.SaveChangesAsync();
            return invoice;
        }

        public async Task GeneratedAsync(DeliveryChallan challan)
        {
            EwayBillResult result = await Service.GenerateAsync(EwayBillSource.DeliveryChallan, challan.DeliveryChallanId, Road(), default);
            await AfterCommit.RunAsync();
            Assert.Equal(EwayBillStatus.Generated, result.EwayBill!.Status);
        }

        public async ValueTask DisposeAsync() => await Db.DisposeAsync();
    }

    private sealed class Branch(bool ewayEnabled) : IBranchSettingsProvider
    {
        public Task<BranchSettings?> GetSettingsAsync(CancellationToken ct = default) =>
            Task.FromResult<BranchSettings?>(new BranchSettings("33", true, EwayBillEnabled: ewayEnabled));
    }

    private sealed class Book : IContactAddressBook
    {
        public Task<IReadOnlyDictionary<long, ContactPostalAddress>> FindAsync(IEnumerable<long> ids, CancellationToken ct)
        {
            IReadOnlyDictionary<long, ContactPostalAddress> found = new Dictionary<long, ContactPostalAddress>
            {
                [42] = new()
                {
                    ContactId = 42,
                    LegalName = "Kaveri Constructions Pvt Ltd",
                    Gstin = EInvoiceMapperTests.IntraBuyerGstin,
                    AddressLine1 = "12 Anna Salai",
                    City = "Chennai",
                    PostalCode = "600018",
                    StateCode = "33",
                    RegistrationType = "Regular",
                },
            };
            return Task.FromResult(found);
        }
    }

    private sealed class HeldAfterCommit : IAfterCommit
    {
        public List<Func<CancellationToken, Task>> Work { get; } = [];

        public void Enqueue(Func<CancellationToken, Task> work) => Work.Add(work);

        public async Task RunAsync()
        {
            foreach (Func<CancellationToken, Task> work in Work.ToList())
            {
                await work(default);
            }

            Work.Clear();
        }
    }

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
