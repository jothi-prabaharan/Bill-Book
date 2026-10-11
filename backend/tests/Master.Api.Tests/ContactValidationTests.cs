using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Master.Repository.Clients;
using Master.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tax;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// Unit tests for Contact validations spanning aggregate rules, GSTIN format/state alignment,
/// TDS rules, and default child constraints.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ContactValidationTests
{
    private readonly PostgresFixture _postgres;

    public ContactValidationTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed class StubNumberGenerator : INumberGenerator
    {
        public Task<NumberAllocation> NextAsync(string seriesCode, DateOnly onDate, CancellationToken ct) =>
            Task.FromResult(new NumberAllocation(1, 1, "C-0001"));

        public Task<string?> PeekAsync(string seriesCode, DateOnly onDate, CancellationToken ct) =>
            Task.FromResult<string?>("C-0001");
    }

    private sealed class StubStateDirectory : IStateDirectory
    {
        public Task<string?> GetStateCodeAsync(int stateId, CancellationToken ct) =>
            Task.FromResult<string?>(stateId switch
            {
                29 => "29", // Karnataka
                33 => "33", // Tamil Nadu
                27 => "27", // Maharashtra
                _ => null,
            });
    }

    private sealed class StubSubAccounts : IAccountingSubAccounts
    {
        public Task<bool> ProvisionForContactAsync(long contactId, string displayName, CancellationToken ct) =>
            Task.FromResult(true);
    }

    private static ContactRepository CreateRepository(ContactsDbContext db, TenantContext tenant) =>
        new(
            db: db,
            admin: null!,
            numbers: new StubNumberGenerator(),
            states: new StubStateDirectory(),
            subAccounts: new StubSubAccounts(),
            clock: TimeProvider.System,
            tenant: tenant);

    private static async Task<long> EnsureRoleExistsAsync(ContactsDbContext db)
    {
        ContactPersonRole? existing = await db.ContactPersonRoles.FirstOrDefaultAsync();
        if (existing is not null)
        {
            return existing.ContactPersonRoleId;
        }

        var role = new ContactPersonRole
        {
            RoleName = "Primary",
            IsDefault = true,
            IsActive = true,
        };
        db.ContactPersonRoles.Add(role);
        await db.SaveChangesAsync();
        return role.ContactPersonRoleId;
    }

    private static SaveContactRequest BuildValidRequest(long roleId) => new()
    {
        DisplayName = "Reliable Corporation",
        ContactCategory = "Business",
        IsCustomer = true,
        GstRegistrationType = "Regular",
        Gstin = "29ABCDE1234F1Z5",
        PlaceOfSupplyStateId = 29,
        CurrencyCode = "INR",
        Persons =
        [
            new ContactPersonModel
            {
                ContactPersonRoleId = roleId,
                FirstName = "Anita",
                Email = "anita@example.com",
                IsDefault = true,
                IsActive = true,
            }
        ],
        Addresses =
        [
            new ContactAddressModel
            {
                AddressType = "Billing",
                AddressLine1 = "Brigade Road",
                City = "Bengaluru",
                IsDefault = true,
                IsActive = true,
            }
        ],
        BankDetails = [],
        Licences = [],
    };

    [SkippableFact]
    public async Task Create_WithoutRole_ReturnsNoRole()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid(), CustomerCode = "0000000042" };
        await using ContactsDbContext db = _postgres.CreateContext(tenant);
        long roleId = await EnsureRoleExistsAsync(db);
        ContactRepository repo = CreateRepository(db, tenant);

        SaveContactRequest req = BuildValidRequest(roleId);
        req.IsCustomer = false;
        req.IsVendor = false;
        req.IsJobWorker = false;
        req.IsPrescriber = false;
        req.IsGuardian = false;

        SaveContactResult result = await repo.CreateAsync(req, default);
        Assert.Equal(SaveContactOutcome.NoRole, result.Outcome);
    }

    [SkippableFact]
    public async Task Create_RegularWithoutGstin_ReturnsGstinRequired()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid(), CustomerCode = "0000000042" };
        await using ContactsDbContext db = _postgres.CreateContext(tenant);
        long roleId = await EnsureRoleExistsAsync(db);
        ContactRepository repo = CreateRepository(db, tenant);

        SaveContactRequest req = BuildValidRequest(roleId);
        req.GstRegistrationType = "Regular";
        req.Gstin = null;

        SaveContactResult result = await repo.CreateAsync(req, default);
        Assert.Equal(SaveContactOutcome.GstinRequired, result.Outcome);
    }

    [SkippableFact]
    public async Task Create_UnregisteredWithGstin_ReturnsGstinNotAllowed()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid(), CustomerCode = "0000000042" };
        await using ContactsDbContext db = _postgres.CreateContext(tenant);
        long roleId = await EnsureRoleExistsAsync(db);
        ContactRepository repo = CreateRepository(db, tenant);

        SaveContactRequest req = BuildValidRequest(roleId);
        req.GstRegistrationType = "Unregistered";
        req.Gstin = "29ABCDE1234F1Z5";

        SaveContactResult result = await repo.CreateAsync(req, default);
        Assert.Equal(SaveContactOutcome.GstinNotAllowed, result.Outcome);
    }

    [SkippableFact]
    public async Task Create_GstinStateMismatch_ReturnsGstinStateMismatch()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid(), CustomerCode = "0000000042" };
        await using ContactsDbContext db = _postgres.CreateContext(tenant);
        long roleId = await EnsureRoleExistsAsync(db);
        ContactRepository repo = CreateRepository(db, tenant);

        SaveContactRequest req = BuildValidRequest(roleId);
        // State 33 is Tamil Nadu, but GSTIN starts with 29 (Karnataka)
        req.PlaceOfSupplyStateId = 33;
        req.Gstin = "29ABCDE1234F1Z5";

        SaveContactResult result = await repo.CreateAsync(req, default);
        Assert.Equal(SaveContactOutcome.GstinStateMismatch, result.Outcome);
    }

    [SkippableFact]
    public async Task Create_TdsApplicableWithoutSection_ReturnsTdsSectionRequired()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid(), CustomerCode = "0000000042" };
        await using ContactsDbContext db = _postgres.CreateContext(tenant);
        long roleId = await EnsureRoleExistsAsync(db);
        ContactRepository repo = CreateRepository(db, tenant);

        SaveContactRequest req = BuildValidRequest(roleId);
        req.IsTdsApplicable = true;
        req.TdsSection = null;

        SaveContactResult result = await repo.CreateAsync(req, default);
        Assert.Equal(SaveContactOutcome.TdsSectionRequired, result.Outcome);
    }

    [SkippableFact]
    public async Task Create_MsmeWithoutUdyam_ReturnsUdyamNumberRequired()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid(), CustomerCode = "0000000042" };
        await using ContactsDbContext db = _postgres.CreateContext(tenant);
        long roleId = await EnsureRoleExistsAsync(db);
        ContactRepository repo = CreateRepository(db, tenant);

        SaveContactRequest req = BuildValidRequest(roleId);
        req.IsMsme = true;
        req.UdyamNumber = null;

        SaveContactResult result = await repo.CreateAsync(req, default);
        Assert.Equal(SaveContactOutcome.UdyamNumberRequired, result.Outcome);
    }

    [SkippableFact]
    public async Task Create_WithoutPersons_ReturnsNoContactPerson()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid(), CustomerCode = "0000000042" };
        await using ContactsDbContext db = _postgres.CreateContext(tenant);
        long roleId = await EnsureRoleExistsAsync(db);
        ContactRepository repo = CreateRepository(db, tenant);

        SaveContactRequest req = BuildValidRequest(roleId);
        req.Persons = [];

        SaveContactResult result = await repo.CreateAsync(req, default);
        Assert.Equal(SaveContactOutcome.NoContactPerson, result.Outcome);
    }

    [SkippableFact]
    public async Task Create_WithMultipleDefaultPersons_ReturnsNoDefaultPerson()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid(), CustomerCode = "0000000042" };
        await using ContactsDbContext db = _postgres.CreateContext(tenant);
        long roleId = await EnsureRoleExistsAsync(db);
        ContactRepository repo = CreateRepository(db, tenant);

        SaveContactRequest req = BuildValidRequest(roleId);
        req.Persons =
        [
            new ContactPersonModel
            {
                ContactPersonRoleId = roleId,
                FirstName = "Anita",
                Email = "anita@example.com",
                IsDefault = true,
                IsActive = true,
            },
            new ContactPersonModel
            {
                ContactPersonRoleId = roleId,
                FirstName = "Kiran",
                Email = "kiran@example.com",
                IsDefault = true,
                IsActive = true,
            }
        ];

        SaveContactResult result = await repo.CreateAsync(req, default);
        Assert.Equal(SaveContactOutcome.NoDefaultPerson, result.Outcome);
    }

    [SkippableFact]
    public async Task Create_DefaultPersonWithoutEmailOrMobile_ReturnsDefaultPersonNeedsContactDetail()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid(), CustomerCode = "0000000042" };
        await using ContactsDbContext db = _postgres.CreateContext(tenant);
        long roleId = await EnsureRoleExistsAsync(db);
        ContactRepository repo = CreateRepository(db, tenant);

        SaveContactRequest req = BuildValidRequest(roleId);
        req.Persons[0].Email = null;
        req.Persons[0].MobileNumber = null;

        SaveContactResult result = await repo.CreateAsync(req, default);
        Assert.Equal(SaveContactOutcome.DefaultPersonNeedsContactDetail, result.Outcome);
    }

    [SkippableFact]
    public async Task Create_MultipleDefaultBillingAddresses_ReturnsMultipleDefaultAddresses()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid(), CustomerCode = "0000000042" };
        await using ContactsDbContext db = _postgres.CreateContext(tenant);
        long roleId = await EnsureRoleExistsAsync(db);
        ContactRepository repo = CreateRepository(db, tenant);

        SaveContactRequest req = BuildValidRequest(roleId);
        req.Addresses =
        [
            new ContactAddressModel
            {
                AddressType = "Billing",
                AddressLine1 = "Office 1",
                City = "Bengaluru",
                IsDefault = true,
                IsActive = true,
            },
            new ContactAddressModel
            {
                AddressType = "Billing",
                AddressLine1 = "Office 2",
                City = "Bengaluru",
                IsDefault = true,
                IsActive = true,
            }
        ];

        SaveContactResult result = await repo.CreateAsync(req, default);
        Assert.Equal(SaveContactOutcome.MultipleDefaultAddresses, result.Outcome);
    }

    [SkippableFact]
    public async Task Create_MultipleDefaultBankDetails_ReturnsMultipleDefaultBankDetails()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid(), CustomerCode = "0000000042" };
        await using ContactsDbContext db = _postgres.CreateContext(tenant);
        long roleId = await EnsureRoleExistsAsync(db);
        ContactRepository repo = CreateRepository(db, tenant);

        SaveContactRequest req = BuildValidRequest(roleId);
        req.BankDetails =
        [
            new ContactBankDetailModel
            {
                AccountHolderName = "Reliable Corporation",
                BankName = "HDFC",
                AccountNumber = "1234567890",
                AccountKind = "Current",
                IsDefault = true,
                IsActive = true,
            },
            new ContactBankDetailModel
            {
                AccountHolderName = "Reliable Corporation",
                BankName = "ICICI",
                AccountNumber = "0987654321",
                AccountKind = "Current",
                IsDefault = true,
                IsActive = true,
            }
        ];

        SaveContactResult result = await repo.CreateAsync(req, default);
        Assert.Equal(SaveContactOutcome.MultipleDefaultBankDetails, result.Outcome);
    }
}
