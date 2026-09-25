using Master.Api.Services;
using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// A branch's e-invoicing settings (TK-91): saved on the branch, refused
/// without a GSTIN, and carried on the org context Sales reads.
/// </summary>
[Collection(nameof(AdminCollection))]
public sealed class BranchEInvoicingTests
{
    private readonly AdminFixture _admin;

    public BranchEInvoicingTests(AdminFixture admin) => _admin = admin;

    [SkippableFact]
    public async Task E_invoicing_without_a_gstin_is_refused()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);
        await using AdminDbContext db = _admin.CreateContext();
        (Guid customerId, Guid orgId) = await NewBranchAsync(db);

        SaveOrganizationResult result = await Service(db).UpdateAsync(
            customerId, orgId, Request(gstin: null, from: new DateOnly(2026, 10, 1), eway: false), default);

        Assert.Equal(SaveOrganizationOutcome.EInvoiceNeedsGstin, result.Outcome);
    }

    [SkippableFact]
    public async Task E_way_bills_without_a_gstin_are_refused()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);
        await using AdminDbContext db = _admin.CreateContext();
        (Guid customerId, Guid orgId) = await NewBranchAsync(db);

        SaveOrganizationResult result = await Service(db).UpdateAsync(
            customerId, orgId, Request(gstin: " ", from: null, eway: true), default);

        Assert.Equal(SaveOrganizationOutcome.EInvoiceNeedsGstin, result.Outcome);
    }

    [SkippableFact]
    public async Task The_settings_save_and_reach_the_org_context()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);
        await using AdminDbContext db = _admin.CreateContext();
        (Guid customerId, Guid orgId) = await NewBranchAsync(db);

        SaveOrganizationResult result = await Service(db).UpdateAsync(
            customerId, orgId, Request(gstin: "33AAACH7409R1Z8", from: new DateOnly(2026, 10, 1), eway: true), default);
        OrgContextResponse? context = await new OrgContextService(db, TimeProvider.System).ResolveAsync(orgId, default);

        Assert.Equal(SaveOrganizationOutcome.Ok, result.Outcome);
        Organization saved = await db.Organizations.AsNoTracking().SingleAsync(o => o.OrgId == orgId);
        Assert.Equal(new DateOnly(2026, 10, 1), saved.EInvoiceFrom);
        Assert.True(saved.EwayBillEnabled);
        Assert.NotNull(context);
        Assert.Equal(new DateOnly(2026, 10, 1), context!.EInvoiceFrom);
        Assert.True(context.EwayBillEnabled);
    }

    [SkippableFact]
    public async Task A_branch_that_does_not_e_invoice_saves_without_a_gstin()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);
        await using AdminDbContext db = _admin.CreateContext();
        (Guid customerId, Guid orgId) = await NewBranchAsync(db);

        SaveOrganizationResult result = await Service(db).UpdateAsync(
            customerId, orgId, Request(gstin: null, from: null, eway: false), default);

        Assert.Equal(SaveOrganizationOutcome.Ok, result.Outcome);
    }

    private static OrganizationService Service(AdminDbContext db) => new(
        db,
        new NoSeeding(),
        new NoCurrencies(),
        new StateDirectory(db, new MemoryCache(new MemoryCacheOptions())),
        NullLogger<OrganizationService>.Instance);

    private static SaveOrganizationRequest Request(string? gstin, DateOnly? from, bool eway) => new()
    {
        OrgCode = "CHN",
        Name = "Chennai",
        BaseCurrency = "INR",
        FinancialYearStartMonth = 4,
        DiscountLevel = "Line",
        Vertical = Vertical.General,
        CountryId = 1,
        Gstin = gstin,
        EInvoiceFrom = from,
        EwayBillEnabled = eway,
    };

    private static async Task<(Guid CustomerId, Guid OrgId)> NewBranchAsync(AdminDbContext db)
    {
        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        string suffix = customerId.ToString("N")[..8];

        db.Customers.Add(new Master.Entity.TableEntities.Customer
        {
            CustomerId = customerId,
            CustomerCode = Random.Shared.NextInt64(1_000_000_000, 9_999_999_999).ToString(),
            CountryPrefix = "IN",
            Name = $"E-invoice Traders {suffix}",
            BillingEmail = $"billing-{suffix}@example.com",
            Status = TenantStatus.Active,
            PlanTier = PlanTier.Standard,
            DatabaseName = "IN000001",
        });
        db.Organizations.Add(new Organization
        {
            OrgId = orgId,
            CustomerId = customerId,
            OrgCode = "CHN",
            Name = "Chennai",
            BaseCurrency = "INR",
            FinancialYearStartMonth = 4,
            CountryId = 1,
            Status = TenantStatus.Active,
        });
        await db.SaveChangesAsync();
        return (customerId, orgId);
    }

    private sealed class NoSeeding : ITenantSeeder
    {
        public Task<IReadOnlyList<string>> SeedAsync(Guid customerId, Guid orgId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> SeedAsync(Guid customerId, Guid orgId, Shared.Kernel.Apps.App apps, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class NoCurrencies : IMasterCurrencies
    {
        public Task<IReadOnlyList<MasterCurrency>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MasterCurrency>>([]);

        public Task<int?> FindCurrencyIdAsync(string code, CancellationToken ct = default) =>
            Task.FromResult<int?>(1);
    }
}
