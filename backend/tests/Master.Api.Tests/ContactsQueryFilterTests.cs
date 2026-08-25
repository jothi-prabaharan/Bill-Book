using Microsoft.EntityFrameworkCore;
using Master.Entity.TableEntities;
using Master.Repository;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// Asserts the con schema's tenant isolation the same way Sales.Api.Tests and
/// Customer.Api.Tests assert theirs: over the whole model and the whole schema,
/// not just the first table a test happens to touch, and backed by a real
/// PostgreSQL so the RLS half — which the model knows nothing about — is
/// actually checked.
///
/// Includes the cross-customer case specifically, since con was the first
/// schema to carry a real CustomerId column: until now, two customers' rows
/// could never sit in the same database, so there was nothing for a
/// "one customer cannot read another customer's row" test to prove.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ContactsQueryFilterTests
{
    private readonly PostgresFixture _postgres;

    public ContactsQueryFilterTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task Every_org_scoped_contacts_entity_has_a_query_filter()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using ContactsDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        List<string> unfiltered = [.. db.Model.GetEntityTypes()
            .Where(e => typeof(OrgScopedEntity).IsAssignableFrom(e.ClrType))
            .Where(e => e.GetDeclaredQueryFilters() is not { Count: > 0 })
            .Select(e => e.ClrType.Name)
            .OrderBy(name => name)];

        Assert.Empty(unfiltered);
    }

    [SkippableFact]
    public async Task Every_org_scoped_contacts_entity_maps_xmin_as_its_concurrency_token()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using ContactsDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        List<string> wrong = [.. db.Model.GetEntityTypes()
            .Where(e => typeof(OrgScopedEntity).IsAssignableFrom(e.ClrType))
            .Where(e => e.FindProperty(nameof(OrgScopedEntity.Version)) is not { } version
                || version.GetColumnName() != "xmin"
                || !version.IsConcurrencyToken)
            .Select(e => e.ClrType.Name)
            .OrderBy(name => name)];

        Assert.Empty(wrong);
    }

    [SkippableFact]
    public async Task One_branch_cannot_read_another_branchs_contacts()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var customerId = Guid.NewGuid();
        await using ContactsDbContext mine = _postgres.CreateContext(customerId, Guid.NewGuid());
        await using ContactsDbContext theirs = _postgres.CreateContext(customerId, Guid.NewGuid());

        mine.Contacts.Add(new Contact
        {
            ContactCode = "C001",
            DisplayName = "Branch-scoped contact",
            CurrencyCode = "INR",
            IsCustomer = true,
        });

        await mine.SaveChangesAsync(CancellationToken.None);

        Assert.NotEmpty(await mine.Contacts.ToListAsync(CancellationToken.None));
        Assert.Empty(await theirs.Contacts.ToListAsync(CancellationToken.None));
    }

    [SkippableFact]
    public async Task One_customer_cannot_read_another_customers_contacts()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        // Two different customers, sharing the one test database the same way
        // production now shares the one tenant database. CustomerId is what
        // keeps them apart here — OrgId alone would too, since it is already
        // globally unique, but this is the test that exercises the column this
        // schema was the first to carry.
        await using ContactsDbContext mine = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        await using ContactsDbContext theirs = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        mine.Contacts.Add(new Contact
        {
            ContactCode = "C002",
            DisplayName = "Customer-scoped contact",
            CurrencyCode = "INR",
            IsCustomer = true,
        });

        await mine.SaveChangesAsync(CancellationToken.None);

        Assert.NotEmpty(await mine.Contacts.ToListAsync(CancellationToken.None));
        Assert.Empty(await theirs.Contacts.ToListAsync(CancellationToken.None));
    }

    [SkippableFact]
    public async Task Row_level_security_covers_every_table_in_the_schema()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using ContactsDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        List<string> unprotected = [];
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText =
                "SELECT tablename FROM pg_tables WHERE schemaname = 'con' AND NOT rowsecurity";

            await db.Database.OpenConnectionAsync(CancellationToken.None);
            await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);

            while (await reader.ReadAsync(CancellationToken.None))
            {
                unprotected.Add(reader.GetString(0));
            }
        }

        Assert.Empty(unprotected);
    }
}
