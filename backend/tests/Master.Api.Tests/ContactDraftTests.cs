using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Master.Repository.Clients;
using Master.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// The contact form's three decisions (TK-188), against a real PostgreSQL:
/// a draft that is saved with only a name and a role and kept off every
/// picker; an address kind with a default billing and a default shipping flag;
/// and a contact person linked to an address, including one added in the
/// same save and so known only by its position.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ContactDraftTests
{
    private readonly PostgresFixture _postgres;

    public ContactDraftTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>Counts the calls, so a test can say a draft provisioned nothing.</summary>
    private sealed class CountingSubAccounts : IAccountingSubAccounts
    {
        public int Calls { get; private set; }

        public Task<bool> ProvisionForContactAsync(long contactId, string displayName, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(true);
        }
    }

    private async Task<(ContactsDbContext Db, TenantContext Tenant, long RoleId)> BranchAsync()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid(), CustomerCode = "0000000188" };
        ContactsDbContext db = _postgres.CreateContext(tenant);
        var role = new ContactPersonRole { RoleName = "Accounts", IsDefault = true };
        db.ContactPersonRoles.Add(role);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (db, tenant, role.ContactPersonRoleId);
    }

    private static ContactRepository Service(ContactsDbContext db, TenantContext tenant, IAccountingSubAccounts subAccounts) =>
        new(db, null!, null!, null!, subAccounts, TimeProvider.System, tenant);

    private static SaveContactRequest Draft(string code) => new()
    {
        ContactCode = code,
        DisplayName = "Kaveri Constructions",
        IsCustomer = true,
        IsDraft = true,
    };

    private static ContactAddressModel Address(string line1, bool billing = false, bool shipping = false, long id = 0) => new()
    {
        ContactAddressId = id,
        Kind = nameof(AddressKind.RegisteredOffice),
        IsDefaultBilling = billing,
        IsDefaultShipping = shipping,
        AddressLine1 = line1,
        City = "Coimbatore",
        CountryId = 1,
    };

    private static ContactPersonModel Person(long roleId, string name, bool isDefault = false) => new()
    {
        ContactPersonRoleId = roleId,
        FirstName = name,
        MobileNumber = "9843011234",
        IsDefault = isDefault,
    };

    [SkippableFact]
    public async Task A_draft_needs_only_a_name_and_a_role_and_gets_no_sub_ledger()
    {
        (ContactsDbContext db, TenantContext tenant, _) = await BranchAsync();
        var subAccounts = new CountingSubAccounts();

        // Regular registration with no GSTIN, TDS with no section, MSME with no
        // Udyam number and nobody to reach: each refuses a save, none a draft.
        SaveContactRequest request = Draft("D-0001");
        request.GstRegistrationType = nameof(GstRegistrationType.Regular);
        request.IsTdsApplicable = true;
        request.IsMsme = true;

        SaveContactResult result = await Service(db, tenant, subAccounts).CreateAsync(request, default);

        Assert.Equal(SaveContactOutcome.Ok, result.Outcome);
        Contact stored = await db.Contacts.AsNoTracking().SingleAsync(c => c.ContactId == result.ContactId);
        Assert.True(stored.IsDraft);
        Assert.Null(stored.SubLedgerProvisionedAt);
        Assert.Equal(0, subAccounts.Calls);
    }

    [SkippableFact]
    public async Task A_draft_still_needs_a_role()
    {
        (ContactsDbContext db, TenantContext tenant, _) = await BranchAsync();
        SaveContactRequest request = Draft("D-0002");
        request.IsCustomer = false;

        SaveContactResult result = await Service(db, tenant, new CountingSubAccounts()).CreateAsync(request, default);

        Assert.Equal(SaveContactOutcome.NoRole, result.Outcome);
    }

    [SkippableFact]
    public async Task Drafts_are_left_off_the_list_unless_asked_for()
    {
        (ContactsDbContext db, TenantContext tenant, _) = await BranchAsync();
        ContactRepository service = Service(db, tenant, new CountingSubAccounts());
        await service.CreateAsync(Draft("D-0003"), default);

        IReadOnlyList<ContactListItem> pickers = await service.ListAsync(null, null, false, default);
        IReadOnlyList<ContactListItem> screen = await service.ListAsync(null, null, false, default, includeDrafts: true);

        Assert.DoesNotContain(pickers, c => c.ContactCode == "D-0003");
        ContactListItem draft = Assert.Single(screen, c => c.ContactCode == "D-0003");
        Assert.True(draft.IsDraft);
    }

    [SkippableFact]
    public async Task Saving_a_draft_runs_the_full_checks_then_provisions_its_sub_ledger()
    {
        (ContactsDbContext db, TenantContext tenant, long roleId) = await BranchAsync();
        var subAccounts = new CountingSubAccounts();
        ContactRepository service = Service(db, tenant, subAccounts);
        SaveContactResult created = await service.CreateAsync(Draft("D-0004"), default);

        // Ending the draft with nobody to reach is the ordinary refusal.
        SaveContactRequest finished = Draft("D-0004");
        finished.IsDraft = false;
        Assert.Equal(SaveContactOutcome.NoContactPerson,
            (await service.UpdateAsync(created.ContactId!.Value, finished, default)).Outcome);

        finished.Persons = [Person(roleId, "Kavitha", isDefault: true)];
        SaveContactResult saved = await service.UpdateAsync(created.ContactId.Value, finished, default);

        Assert.Equal(SaveContactOutcome.Ok, saved.Outcome);
        Contact stored = await db.Contacts.AsNoTracking().SingleAsync(c => c.ContactId == created.ContactId);
        Assert.False(stored.IsDraft);
        Assert.NotNull(stored.SubLedgerProvisionedAt);
        Assert.Equal(1, subAccounts.Calls);
    }

    [SkippableFact]
    public async Task A_saved_contact_never_returns_to_draft()
    {
        (ContactsDbContext db, TenantContext tenant, long roleId) = await BranchAsync();
        ContactRepository service = Service(db, tenant, new CountingSubAccounts());
        SaveContactRequest request = Draft("D-0005");
        request.IsDraft = false;
        request.Persons = [Person(roleId, "Kavitha", isDefault: true)];
        SaveContactResult saved = await service.CreateAsync(request, default);

        request.IsDraft = true;
        SaveContactResult again = await service.UpdateAsync(saved.ContactId!.Value, request, default);

        Assert.Equal(SaveContactOutcome.CannotReturnToDraft, again.Outcome);
    }

    [SkippableFact]
    public async Task A_draft_has_no_sub_ledger_to_retry()
    {
        (ContactsDbContext db, TenantContext tenant, _) = await BranchAsync();
        ContactRepository service = Service(db, tenant, new CountingSubAccounts());
        SaveContactResult created = await service.CreateAsync(Draft("D-0006"), default);

        Assert.Equal(SaveContactOutcome.StillDraft, await service.RetrySubLedgerAsync(created.ContactId!.Value, default));
    }

    [SkippableFact]
    public async Task One_address_may_be_both_defaults_and_a_second_default_of_either_is_refused()
    {
        (ContactsDbContext db, TenantContext tenant, _) = await BranchAsync();
        ContactRepository service = Service(db, tenant, new CountingSubAccounts());

        SaveContactRequest both = Draft("D-0007");
        both.Addresses = [Address("12 Avinashi Road", billing: true, shipping: true), Address("Plot 44")];
        Assert.Equal(SaveContactOutcome.Ok, (await service.CreateAsync(both, default)).Outcome);

        SaveContactRequest twoBilling = Draft("D-0008");
        twoBilling.Addresses = [Address("12 Avinashi Road", billing: true), Address("Plot 44", billing: true)];
        Assert.Equal(SaveContactOutcome.MultipleDefaultAddresses, (await service.CreateAsync(twoBilling, default)).Outcome);
    }

    [SkippableFact]
    public async Task A_default_moves_from_one_address_to_another_in_one_save()
    {
        (ContactsDbContext db, TenantContext tenant, _) = await BranchAsync();
        ContactRepository service = Service(db, tenant, new CountingSubAccounts());
        SaveContactRequest request = Draft("D-0009");
        request.Addresses = [Address("12 Avinashi Road", billing: true, shipping: true), Address("Plot 44")];
        SaveContactResult created = await service.CreateAsync(request, default);
        List<ContactAddress> stored = await db.ContactAddresses.AsNoTracking()
            .Where(a => a.ContactId == created.ContactId).OrderBy(a => a.ContactAddressId).ToListAsync();

        // Both unique indexes would refuse the new default if the old one were
        // still set when it was written.
        request.Addresses =
        [
            Address("12 Avinashi Road", billing: true, id: stored[0].ContactAddressId),
            Address("Plot 44", shipping: true, id: stored[1].ContactAddressId),
        ];
        SaveContactResult updated = await service.UpdateAsync(created.ContactId!.Value, request, default);

        Assert.Equal(SaveContactOutcome.Ok, updated.Outcome);
        db.ChangeTracker.Clear();
        ContactAddress shipping = await db.ContactAddresses.AsNoTracking().SingleAsync(a => a.ContactId == created.ContactId && a.IsDefaultShipping);
        Assert.Equal(stored[1].ContactAddressId, shipping.ContactAddressId);
    }

    [SkippableFact]
    public async Task A_person_is_linked_to_an_address_added_in_the_same_save_by_its_position()
    {
        (ContactsDbContext db, TenantContext tenant, long roleId) = await BranchAsync();
        ContactRepository service = Service(db, tenant, new CountingSubAccounts());
        SaveContactRequest request = Draft("D-0010");
        request.Addresses = [Address("12 Avinashi Road", billing: true), Address("Plot 44")];
        ContactPersonModel stores = Person(roleId, "Suresh");
        stores.AddressIndex = 1;
        request.Persons = [Person(roleId, "Kavitha", isDefault: true), stores];

        SaveContactResult created = await service.CreateAsync(request, default);

        Assert.Equal(SaveContactOutcome.Ok, created.Outcome);
        ContactDetail detail = (await service.GetAsync(created.ContactId!.Value, default))!;
        ContactAddressModel plot = Assert.Single(detail.Addresses, a => a.AddressLine1 == "Plot 44");
        Assert.Equal(plot.ContactAddressId, Assert.Single(detail.Persons, p => p.FirstName == "Suresh").ContactAddressId);
        Assert.Null(Assert.Single(detail.Persons, p => p.FirstName == "Kavitha").ContactAddressId);
    }

    [SkippableFact]
    public async Task A_link_to_an_address_the_request_does_not_hold_is_refused()
    {
        (ContactsDbContext db, TenantContext tenant, long roleId) = await BranchAsync();
        ContactRepository service = Service(db, tenant, new CountingSubAccounts());

        SaveContactRequest byIndex = Draft("D-0011");
        byIndex.Addresses = [Address("12 Avinashi Road")];
        ContactPersonModel wrongIndex = Person(roleId, "Suresh");
        wrongIndex.AddressIndex = 3;
        byIndex.Persons = [wrongIndex];
        Assert.Equal(SaveContactOutcome.UnknownAddressLink, (await service.CreateAsync(byIndex, default)).Outcome);

        SaveContactRequest byId = Draft("D-0012");
        ContactPersonModel wrongId = Person(roleId, "Suresh");
        wrongId.ContactAddressId = 999_999;
        byId.Persons = [wrongId];
        Assert.Equal(SaveContactOutcome.UnknownAddressLink, (await service.CreateAsync(byId, default)).Outcome);
    }

    [SkippableFact]
    public async Task Removing_an_address_keeps_its_person_with_the_contact()
    {
        (ContactsDbContext db, TenantContext tenant, long roleId) = await BranchAsync();
        ContactRepository service = Service(db, tenant, new CountingSubAccounts());
        SaveContactRequest request = Draft("D-0013");
        request.Addresses = [Address("Plot 44")];
        ContactPersonModel stores = Person(roleId, "Suresh");
        stores.AddressIndex = 0;
        request.Persons = [stores];
        SaveContactResult created = await service.CreateAsync(request, default);
        ContactDetail detail = (await service.GetAsync(created.ContactId!.Value, default))!;

        request.Addresses = [];
        ContactPersonModel kept = detail.Persons.Single();
        kept.AddressIndex = null;
        kept.ContactAddressId = null;
        request.Persons = [kept];
        SaveContactResult updated = await service.UpdateAsync(created.ContactId.Value, request, default);

        Assert.Equal(SaveContactOutcome.Ok, updated.Outcome);
        ContactDetail after = (await service.GetAsync(created.ContactId.Value, default))!;
        Assert.Empty(after.Addresses);
        Assert.Null(Assert.Single(after.Persons).ContactAddressId);
    }
}
