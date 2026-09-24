using Customer.Api.Controllers;
using Customer.Entity.TableEntities;
using Customer.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Customer.Api.Tests;

/// <summary>
/// A lead saved with a blank phone stores NULL, not '' (D-04, TK-21).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class LeadPhoneTests
{
    private readonly PostgresFixture _postgres;

    public LeadPhoneTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>Saving a lead never asks Contacts anything, so there is no client.</summary>
    private static LeadsController Leads(CustomerDbContext db, Guid customerId, Guid orgId) =>
        new(db, new TenantContext { CustomerId = customerId, OrgId = orgId }, null!);

    [SkippableFact]
    public async Task A_blank_phone_is_stored_as_null_on_create_and_update()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        await using CustomerDbContext db = _postgres.CreateContext(customerId, orgId);
        LeadsController controller = Leads(db, customerId, orgId);

        await controller.Create(new SaveLeadRequest { Name = "Ravi", Phone = "  " }, default);

        Lead created = await db.Leads.AsNoTracking().SingleAsync();
        Assert.Null(created.Phone);

        await controller.Update(created.LeadId, new SaveLeadRequest { Name = "Ravi", Phone = " +919876543210 " }, default);
        Assert.Equal("+919876543210", (await db.Leads.AsNoTracking().SingleAsync()).Phone);

        await controller.Update(created.LeadId, new SaveLeadRequest { Name = "Ravi", Phone = "" }, default);
        Assert.Null((await db.Leads.AsNoTracking().SingleAsync()).Phone);
    }
}
