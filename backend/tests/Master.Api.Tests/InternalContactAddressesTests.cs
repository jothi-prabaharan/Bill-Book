using Master.Api.Controllers;
using Master.Api.Services;
using Master.Entity.Enums;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Shared.Kernel.Contacts;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// <c>POST internal/contacts/addresses</c> (TK-91): a contact's legal name and
/// billing address as fields, for the IRP's buyer block.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class InternalContactAddressesTests
{
    private readonly PostgresFixture _postgres;

    public InternalContactAddressesTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task The_default_billing_address_comes_back_as_fields()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();

        long contactId;
        await using (ContactsDbContext db = _postgres.CreateContext(customerId, orgId))
        {
            var contact = new Contact
            {
                ContactCode = "C001",
                DisplayName = "Kaveri",
                LegalName = "Kaveri Constructions Pvt Ltd",
                Gstin = "33AABCT1332L1ZL",
                GstRegistrationType = GstRegistrationType.Regular,
                CurrencyCode = "INR",
                IsCustomer = true,
            };
            db.Contacts.Add(contact);
            await db.SaveChangesAsync();
            contactId = contact.ContactId;

            db.ContactAddresses.AddRange(
                new ContactAddress
                {
                    ContactId = contactId, AddressType = AddressType.Shipping, IsDefault = true,
                    AddressLine1 = "Site 4", City = "Hosur", CountryId = 1, PostalCode = "635109",
                },
                new ContactAddress
                {
                    ContactId = contactId, AddressType = AddressType.Billing, IsDefault = true,
                    AddressLine1 = "12 Anna Salai", AddressLine2 = "Teynampet", City = "Chennai",
                    CountryId = 1, PostalCode = "600018", MobileNumber = "9840012345",
                });
            await db.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        var tenant = new TenantContext();
        services.AddScoped(_ => _postgres.CreateContext(tenant));
        services.AddSingleton<IStateDirectory>(new NoStates());
        await using ServiceProvider provider = services.BuildServiceProvider();

        IActionResult result = await new InternalContactLookupController(tenant, provider).Addresses(
            new ContactLookupRequest { CustomerId = customerId, OrgId = orgId, Ids = [contactId] }, default);

        List<ContactPostalAddress> found = Assert.IsType<List<ContactPostalAddress>>(Assert.IsType<OkObjectResult>(result).Value);
        ContactPostalAddress address = Assert.Single(found);
        Assert.Equal("Kaveri Constructions Pvt Ltd", address.LegalName);
        Assert.Equal("33AABCT1332L1ZL", address.Gstin);
        Assert.Equal("12 Anna Salai", address.AddressLine1);
        Assert.Equal("Chennai", address.City);
        Assert.Equal("600018", address.PostalCode);
        Assert.Equal("9840012345", address.PhoneNumber);
        Assert.Equal("Regular", address.RegistrationType);
    }

    [SkippableFact]
    public async Task A_request_with_no_branch_is_refused()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        var tenant = new TenantContext();
        await using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();

        IActionResult result = await new InternalContactLookupController(tenant, provider).Addresses(
            new ContactLookupRequest { Ids = [1] }, default);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private sealed class NoStates : IStateDirectory
    {
        public Task<string?> GetStateCodeAsync(int stateId, CancellationToken ct) => Task.FromResult<string?>(null);
    }
}
