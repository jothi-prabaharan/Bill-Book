using System.Text.Json;
using Master.Api.Services;
using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.CustomFields;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// Custom fields on contacts (TK-111), against a real PostgreSQL: the search
/// that reaches into the jsonb column, and a deactivated field's value that
/// stays stored and shown.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ContactCustomFieldTests
{
    private readonly PostgresFixture _postgres;

    public ContactCustomFieldTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed class Definitions(params CustomFieldDefinitionDto[] fields) : ICustomFieldDefinitions
    {
        public Task<IReadOnlyList<CustomFieldDefinitionDto>?> GetAsync(CustomFieldEntityKind kind, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CustomFieldDefinitionDto>?>(kind == CustomFieldEntityKind.Contact ? fields : []);
    }

    private sealed class NoSubAccounts : IAccountingSubAccounts
    {
        public Task<bool> ProvisionForContactAsync(long contactId, string displayName, CancellationToken ct) => Task.FromResult(true);
    }

    private static CustomFieldDefinitionDto SiteRef(bool searchable = true, bool active = true) => new()
    {
        EntityKind = CustomFieldEntityKind.Contact,
        Key = "site_ref",
        Label = "Site reference",
        DataType = CustomFieldDataType.Text,
        IsSearchable = searchable,
        IsActive = active,
    };

    private async Task<(ContactsDbContext Db, TenantContext Tenant)> BranchWithContactAsync(string siteRef)
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid(), CustomerCode = "0000000042" };
        ContactsDbContext db = _postgres.CreateContext(tenant);

        db.Contacts.AddRange(
            new Contact
            {
                ContactCode = "C-0001",
                DisplayName = "Acme Interiors",
                IsCustomer = true,
                ContactCategory = ContactCategory.Business,
                GstRegistrationType = GstRegistrationType.Unregistered,
                CurrencyCode = "INR",
                IsActive = true,
                CustomFields = JsonDocument.Parse($$"""{ "site_ref": "{{siteRef}}" }"""),
            },
            new Contact
            {
                ContactCode = "C-0002",
                DisplayName = "Bharat Traders",
                IsCustomer = true,
                ContactCategory = ContactCategory.Business,
                GstRegistrationType = GstRegistrationType.Unregistered,
                CurrencyCode = "INR",
                IsActive = true,
            });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return (db, tenant);
    }

    private static ContactService Service(ContactsDbContext db, TenantContext tenant, ICustomFieldDefinitions definitions) =>
        new(db, null!, null!, null!, new NoSubAccounts(), TimeProvider.System, tenant, definitions);

    /// <summary>The card's "done when".</summary>
    [SkippableFact]
    public async Task A_contact_is_found_by_searching_a_searchable_custom_value()
    {
        (ContactsDbContext db, TenantContext tenant) = await BranchWithContactAsync("TOWER-B-17");
        await using ContactsDbContext _ = db;

        IReadOnlyList<ContactListItem> found = await Service(db, tenant, new Definitions(SiteRef())).ListAsync("tower-b", null, false, default);

        ContactListItem acme = Assert.Single(found);
        Assert.Equal("Acme Interiors", acme.DisplayName);
        Assert.Equal("TOWER-B-17", acme.CustomFields!["site_ref"].GetString());
    }

    [SkippableFact]
    public async Task A_field_not_marked_searchable_is_not_searched()
    {
        (ContactsDbContext db, TenantContext tenant) = await BranchWithContactAsync("TOWER-B-17");
        await using ContactsDbContext _ = db;

        Assert.Empty(await Service(db, tenant, new Definitions(SiteRef(searchable: false))).ListAsync("tower-b", null, false, default));
    }

    [SkippableFact]
    public async Task A_deactivated_field_keeps_its_stored_value_and_it_is_still_shown()
    {
        (ContactsDbContext db, TenantContext tenant) = await BranchWithContactAsync("TOWER-B-17");
        await using ContactsDbContext _ = db;
        ContactService service = Service(db, tenant, new Definitions(SiteRef(active: false)));

        IReadOnlyList<ContactListItem> all = await service.ListAsync(null, null, false, default);
        ContactListItem acme = all.Single(c => c.ContactCode == "C-0001");

        Assert.Equal("TOWER-B-17", acme.CustomFields!["site_ref"].GetString());

        // No longer searchable once inactive, and the validator refuses a new value
        // while accepting the stored one unchanged.
        Assert.Empty(await service.ListAsync("tower-b", null, false, default));
        Dictionary<string, JsonElement> stored = acme.CustomFields;
        Assert.Empty(CustomFieldValidator.Validate([SiteRef(active: false)], stored, previous: stored));
        Assert.NotEmpty(CustomFieldValidator.Validate([SiteRef(active: false)],
            JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""{ "site_ref": "NEW" }""")!, previous: stored));
    }

    [Fact]
    public void Empty_values_are_not_stored_and_nothing_left_stores_null()
    {
        Dictionary<string, JsonElement> values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            """{ "a": "", "b": [], "c": null, "d": "kept" }""")!;

        using JsonDocument? written = CustomFieldStore.Write(values);
        Assert.Equal("""{"d":"kept"}""", written!.RootElement.GetRawText());
        Assert.Null(CustomFieldStore.Write(JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""{ "a": "" }""")!));
        Assert.Equal("kept", CustomFieldStore.Read(written)["d"].GetString());
        Assert.Empty(CustomFieldStore.Read(null));
    }
}
