using Master.Api.Services;
using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// Every branch has one walk-in customer, coded <c>WALKIN</c> (TK-17): the till's
/// default customer, found by that code alone, with no setup on a new branch.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class WalkInContactTests
{
    private readonly PostgresFixture _postgres;

    public WalkInContactTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>Accounting, recorded: which contacts were given a sub-ledger.</summary>
    private sealed class RecordingSubAccounts : IAccountingSubAccounts
    {
        public bool Succeed { get; set; } = true;

        public List<long> Provisioned { get; } = [];

        public Task<bool> ProvisionForContactAsync(long contactId, string displayName, CancellationToken ct)
        {
            Provisioned.Add(contactId);
            return Task.FromResult(Succeed);
        }
    }

    /// <summary>
    /// Only the walk-in seed and the create path's code check are exercised. The
    /// admin database, numbering, states and tokens are never reached by them.
    /// </summary>
    private static ContactService Service(ContactsDbContext db, TenantContext tenant, IAccountingSubAccounts subAccounts) =>
        new(db, null!, null!, null!, subAccounts, TimeProvider.System, tenant);

    private (ContactsDbContext Db, TenantContext Tenant) Branch(Guid? customerId = null, Guid? orgId = null)
    {
        var tenant = new TenantContext
        {
            CustomerId = customerId ?? Guid.NewGuid(),
            OrgId = orgId ?? Guid.NewGuid(),
            CustomerCode = "0000000042",
        };

        return (_postgres.CreateContext(tenant), tenant);
    }

    [SkippableFact]
    public async Task A_new_branch_gets_exactly_one_walk_in_customer_and_a_second_seed_adds_none()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (ContactsDbContext db, TenantContext tenant) = Branch();
        await using ContactsDbContext _ = db;
        var accounting = new RecordingSubAccounts();
        ContactService service = Service(db, tenant, accounting);

        Assert.Equal(1, await service.SeedWalkInAsync("INR", default));
        Assert.Equal(0, await service.SeedWalkInAsync("INR", default));

        Contact walkIn = await db.Contacts.AsNoTracking().SingleAsync(c => c.ContactCode == "WALKIN");

        Assert.Equal("Walk-in Customer", walkIn.DisplayName);
        Assert.True(walkIn.IsCustomer);
        Assert.False(walkIn.IsVendor);
        Assert.Equal(GstRegistrationType.Consumer, walkIn.GstRegistrationType);
        Assert.Null(walkIn.Gstin);

        // No place of supply of its own, so a B2C sale to it is taxed at the
        // branch's own state.
        Assert.Null(walkIn.PlaceOfSupplyStateId);

        // Provisioned once, then left alone.
        Assert.Equal([walkIn.ContactId], accounting.Provisioned);
        Assert.NotNull(walkIn.SubLedgerProvisionedAt);
    }

    [SkippableFact]
    public async Task The_walk_in_takes_the_branchs_currency()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (ContactsDbContext db, TenantContext tenant) = Branch();
        await using ContactsDbContext _ = db;

        await Service(db, tenant, new RecordingSubAccounts()).SeedWalkInAsync("AED", default);

        Assert.Equal("AED", (await db.Contacts.AsNoTracking().SingleAsync(c => c.ContactCode == "WALKIN")).CurrencyCode);
    }

    [SkippableFact]
    public async Task A_walk_in_whose_sub_ledger_failed_is_repaired_by_the_next_seed()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (ContactsDbContext db, TenantContext tenant) = Branch();
        await using ContactsDbContext _ = db;
        var accounting = new RecordingSubAccounts { Succeed = false };
        ContactService service = Service(db, tenant, accounting);

        await service.SeedWalkInAsync("INR", default);
        Assert.Null((await db.Contacts.AsNoTracking().SingleAsync(c => c.ContactCode == "WALKIN")).SubLedgerProvisionedAt);

        accounting.Succeed = true;
        Assert.Equal(0, await service.SeedWalkInAsync("INR", default));

        Assert.NotNull((await db.Contacts.AsNoTracking().SingleAsync(c => c.ContactCode == "WALKIN")).SubLedgerProvisionedAt);
        Assert.Equal(2, accounting.Provisioned.Count);
    }

    [SkippableFact]
    public async Task Each_branch_has_its_own_walk_in()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        (ContactsDbContext a, TenantContext tenantA) = Branch(customerId);
        (ContactsDbContext b, TenantContext tenantB) = Branch(customerId);
        await using ContactsDbContext _a = a;
        await using ContactsDbContext _b = b;

        Assert.Equal(1, await Service(a, tenantA, new RecordingSubAccounts()).SeedWalkInAsync("INR", default));
        Assert.Equal(1, await Service(b, tenantB, new RecordingSubAccounts()).SeedWalkInAsync("INR", default));

        Assert.Equal(1, await a.Contacts.CountAsync(c => c.ContactCode == "WALKIN"));
        Assert.Equal(1, await b.Contacts.CountAsync(c => c.ContactCode == "WALKIN"));
    }

    [SkippableTheory]
    [InlineData("WALKIN")]
    [InlineData("walkin")]
    [InlineData(" WalkIn ")]
    public async Task A_user_cannot_create_a_contact_with_the_walk_in_code(string code)
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (ContactsDbContext db, TenantContext tenant) = Branch();
        await using ContactsDbContext _ = db;

        // Refused before the branch even has its walk-in: the till matches the
        // code without regard to case, so no other contact may ever carry it.
        SaveContactResult result = await Service(db, tenant, new RecordingSubAccounts()).CreateAsync(
            new SaveContactRequest
            {
                ContactCode = code,
                IsCustomer = true,
                ContactCategory = "Individual",
                GstRegistrationType = "Consumer",
                DisplayName = "Someone else",
            },
            default);

        Assert.Equal(SaveContactOutcome.DuplicateCode, result.Outcome);
        Assert.Equal(0, await db.Contacts.CountAsync());
    }
}
