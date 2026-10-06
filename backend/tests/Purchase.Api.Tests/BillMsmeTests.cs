using Microsoft.Extensions.Options;
using Purchase.Entity.Enums;
using Purchase.Entity.Models;
using Purchase.Entity.TableEntities;
using Purchase.Repository;
using Purchase.Repository.Clients;
using Purchase.Repository.Repositories;
using Shared.Kernel.Contacts;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Purchase.Api.Tests;

[Collection(nameof(PostgresCollection))]
public class BillMsmeTests
{
    private readonly PostgresFixture _pg;

    public BillMsmeTests(PostgresFixture pg) => _pg = pg;

    private sealed class FakeContactDirectory : IContactDirectory
    {
        private readonly Dictionary<long, ContactSummary> _contacts = [];

        public void Add(ContactSummary c) => _contacts[c.ContactId] = c;

        public Task<IReadOnlyDictionary<long, ContactSummary>> FindAsync(IEnumerable<long> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<long, ContactSummary>>(_contacts.Where(k => ids.Contains(k.Key)).ToDictionary(k => k.Key, k => k.Value));

        public Task<EnsureGuardianResponse> EnsureGuardianAsync(string displayName, string mobileNumber, string? email, CancellationToken ct) =>
            throw new NotImplementedException();
    }

    [SkippableFact]
    public async Task Msme_Vendor_Bill_Refuses_Due_Date_Past_45_Days()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        TenantContext tenant = new() { CustomerId = customerId, OrgId = orgId };
        using PurchaseDbContext db = _pg.CreateContext(customerId, orgId);

        db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
        await db.SaveChangesAsync();

        StubNameLookup names = new();
        RecordingInventory inventory = new();
        RecordingLedger ledger = new();
        StubPaymentTerms terms = new();
        RecordingFixedAssets fixedAssets = new();
        var numbering = new NumberGenerator(db, Options.Create(new NumberingOptions()), new StubFinancialYear());

        FakeContactDirectory contacts = new();
        long msmeVendorId = 101;
        contacts.Add(new ContactSummary
        {
            ContactId = msmeVendorId,
            ContactCode = "V-MSME",
            DisplayName = "Micro Tech Spares",
            IsVendor = true,
            IsMsme = true,
            UdyamNumber = "UDYAM-TN-01-0012345",
        });

        BillRepository bills = new(
            db, tenant, numbering, new StubBaseCurrency(), new StubBranchSettings(),
            new StubTaxRates(), names, names, inventory, ledger, terms, fixedAssets,
            new StubCurrentUser(), TimeProvider.System,
            contacts: contacts);

        DateOnly docDate = new(2026, 9, 1);
        DateOnly dueDate60 = docDate.AddDays(60); // 60 days past acceptance

        SaveBillRequest request = new()
        {
            ContactId = msmeVendorId,
            DocumentDate = docDate,
            DueDate = dueDate60,
            VendorBillNo = "INV/MSME/01",
            VendorBillDate = docDate,
            PlaceOfSupplyStateCode = "33",
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            Lines =
            [
                new SaveBillLineRequest
                {
                    ItemId = 1,
                    Quantity = 10,
                    ConversionFactor = 1m,
                    UnitPrice = 500m,
                    TaxGroupId = 1,
                    LineType = DocumentLineType.Stock,
                }
            ]
        };

        BillResult result = await bills.CreateAsync(request, CancellationToken.None);

        Assert.Equal(BillOutcome.MsmeDueDateExceeded45Days, result.Outcome);
        Assert.Contains("cannot exceed 45 days", result.Detail);
    }

    [SkippableFact]
    public async Task Msme_Vendor_Bill_Accepts_Due_Date_Within_45_Days()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        TenantContext tenant = new() { CustomerId = customerId, OrgId = orgId };
        using PurchaseDbContext db = _pg.CreateContext(customerId, orgId);

        db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
        await db.SaveChangesAsync();

        StubNameLookup names = new();
        RecordingInventory inventory = new();
        RecordingLedger ledger = new();
        StubPaymentTerms terms = new();
        RecordingFixedAssets fixedAssets = new();
        var numbering = new NumberGenerator(db, Options.Create(new NumberingOptions()), new StubFinancialYear());

        FakeContactDirectory contacts = new();
        long msmeVendorId = 102;
        contacts.Add(new ContactSummary
        {
            ContactId = msmeVendorId,
            ContactCode = "V-MSME2",
            DisplayName = "Small Tools Mfg",
            IsVendor = true,
            IsMsme = true,
            UdyamNumber = "UDYAM-TN-02-0054321",
        });

        BillRepository bills = new(
            db, tenant, numbering, new StubBaseCurrency(), new StubBranchSettings(),
            new StubTaxRates(), names, names, inventory, ledger, terms, fixedAssets,
            new StubCurrentUser(), TimeProvider.System,
            contacts: contacts);

        DateOnly docDate = new(2026, 9, 1);
        DateOnly dueDate45 = docDate.AddDays(45); // Exactly 45 days

        SaveBillRequest request = new()
        {
            ContactId = msmeVendorId,
            DocumentDate = docDate,
            DueDate = dueDate45,
            VendorBillNo = "INV/MSME/02",
            VendorBillDate = docDate,
            PlaceOfSupplyStateCode = "33",
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            Lines =
            [
                new SaveBillLineRequest
                {
                    ItemId = 1,
                    Quantity = 5,
                    ConversionFactor = 1m,
                    UnitPrice = 300m,
                    TaxGroupId = 1,
                    LineType = DocumentLineType.Stock,
                }
            ]
        };

        BillResult result = await bills.CreateAsync(request, CancellationToken.None);

        Assert.Equal(BillOutcome.Ok, result.Outcome);
    }

    [SkippableFact]
    public async Task Non_Msme_Vendor_Bill_Permits_60_Day_Due_Date()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        TenantContext tenant = new() { CustomerId = customerId, OrgId = orgId };
        using PurchaseDbContext db = _pg.CreateContext(customerId, orgId);

        db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
        await db.SaveChangesAsync();

        StubNameLookup names = new();
        RecordingInventory inventory = new();
        RecordingLedger ledger = new();
        StubPaymentTerms terms = new();
        RecordingFixedAssets fixedAssets = new();
        var numbering = new NumberGenerator(db, Options.Create(new NumberingOptions()), new StubFinancialYear());

        FakeContactDirectory contacts = new();
        long nonMsmeVendorId = 103;
        contacts.Add(new ContactSummary
        {
            ContactId = nonMsmeVendorId,
            ContactCode = "V-CORP",
            DisplayName = "Large Corp Supplies Ltd",
            IsVendor = true,
            IsMsme = false,
        });

        BillRepository bills = new(
            db, tenant, numbering, new StubBaseCurrency(), new StubBranchSettings(),
            new StubTaxRates(), names, names, inventory, ledger, terms, fixedAssets,
            new StubCurrentUser(), TimeProvider.System,
            contacts: contacts);

        DateOnly docDate = new(2026, 9, 1);
        DateOnly dueDate60 = docDate.AddDays(60); // 60 days is valid for non-MSME

        SaveBillRequest request = new()
        {
            ContactId = nonMsmeVendorId,
            DocumentDate = docDate,
            DueDate = dueDate60,
            VendorBillNo = "INV/CORP/01",
            VendorBillDate = docDate,
            PlaceOfSupplyStateCode = "33",
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            Lines =
            [
                new SaveBillLineRequest
                {
                    ItemId = 1,
                    Quantity = 20,
                    ConversionFactor = 1m,
                    UnitPrice = 100m,
                    TaxGroupId = 1,
                    LineType = DocumentLineType.Stock,
                }
            ]
        };

        BillResult result = await bills.CreateAsync(request, CancellationToken.None);

        Assert.Equal(BillOutcome.Ok, result.Outcome);
    }
}
