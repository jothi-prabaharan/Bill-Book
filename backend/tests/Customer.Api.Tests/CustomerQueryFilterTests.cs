using Microsoft.EntityFrameworkCore;
using Customer.Entity.TableEntities;
using Customer.Repository;
using Shared.Kernel.Tenancy;
using Xunit;
using Shared.Kernel.Customer;

namespace Customer.Api.Tests;

[Collection(nameof(PostgresCollection))]
public sealed class CustomerQueryFilterTests
{
    private readonly PostgresFixture _postgres;

    public CustomerQueryFilterTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task Every_org_scoped_customer_entity_has_a_query_filter()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using CustomerDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        List<string> unfiltered = [.. db.Model.GetEntityTypes()
            .Where(e => typeof(OrgScopedEntity).IsAssignableFrom(e.ClrType))
            .Where(e => e.GetDeclaredQueryFilters() is not { Count: > 0 })
            .Select(e => e.ClrType.Name)
            .OrderBy(name => name)];

        Assert.Empty(unfiltered);
    }

    [SkippableFact]
    public async Task Every_org_scoped_customer_entity_maps_xmin_as_its_concurrency_token()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using CustomerDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

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
    public async Task One_branch_cannot_read_another_branchs_leads()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var customerId = Guid.NewGuid();
        var myOrgId = Guid.NewGuid();
        var theirOrgId = Guid.NewGuid();
        await using CustomerDbContext mine = _postgres.CreateContext(customerId, myOrgId);
        await using CustomerDbContext theirs = _postgres.CreateContext(customerId, theirOrgId);

        mine.Leads.Add(new Lead
        {
            Name = "Test Lead",
            CompanyName = "Test Company",
            Email = "test@example.com",
            Phone = "1234567890",
            Source = LeadSource.Website,
            Status = LeadStatus.New
        });

        await mine.SaveChangesAsync(CancellationToken.None);

        Assert.NotEmpty(await mine.Leads.ToListAsync(CancellationToken.None));
        Assert.Empty(await theirs.Leads.ToListAsync(CancellationToken.None));
    }

    [SkippableFact]
    public async Task One_branch_cannot_read_another_branchs_tickets()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var customerId = Guid.NewGuid();
        var myOrgId = Guid.NewGuid();
        var theirOrgId = Guid.NewGuid();
        
        var options = new DbContextOptionsBuilder<Master.Repository.ContactsDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("CUSTOMER_TEST_DB") ?? "Host=localhost;Port=5432;Database=customer_tests;Username=postgres;Password=123")
            .Options;
        await using var master = new Master.Repository.ContactsDbContext(options, new TenantContext { CustomerId = customerId, OrgId = myOrgId });
        var contact = new Master.Entity.TableEntities.Contact
        {
            ContactCode = "C001",
            DisplayName = "Test",
            CurrencyCode = "INR", IsCustomer = true
        };
        master.Contacts.Add(contact);
        await master.SaveChangesAsync();

        await using CustomerDbContext mine = _postgres.CreateContext(customerId, myOrgId);
        await using CustomerDbContext theirs = _postgres.CreateContext(customerId, theirOrgId);

        mine.Tickets.Add(new Ticket
        {
            ContactId = contact.ContactId,
            Subject = "Help",
            Description = "Please help me",
            Status = TicketStatus.Open,
            Priority = TicketPriority.High
        });

        await mine.SaveChangesAsync(CancellationToken.None);

        Assert.NotEmpty(await mine.Tickets.ToListAsync(CancellationToken.None));
        Assert.Empty(await theirs.Tickets.ToListAsync(CancellationToken.None));
    }

    [SkippableFact]
    public async Task One_customer_cannot_read_another_customers_leads()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        // Two different customers sharing the one test database — the case that
        // could never be tested before cus carried a real CustomerId column.
        await using CustomerDbContext mine = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        await using CustomerDbContext theirs = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        mine.Leads.Add(new Lead
        {
            Name = "Customer-scoped lead",
            CompanyName = "Test Company",
            Email = "test2@example.com",
            Phone = "1234567890",
            Source = LeadSource.Website,
            Status = LeadStatus.New
        });

        await mine.SaveChangesAsync(CancellationToken.None);

        Assert.NotEmpty(await mine.Leads.ToListAsync(CancellationToken.None));
        Assert.Empty(await theirs.Leads.ToListAsync(CancellationToken.None));
    }

    [SkippableFact]
    public async Task Row_level_security_covers_every_table_in_the_schema()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using CustomerDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        List<string> unprotected = [];
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText =
                "SELECT tablename FROM pg_tables WHERE schemaname = 'cus' AND NOT rowsecurity";

            await db.Database.OpenConnectionAsync(CancellationToken.None);
            await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);

            while (await reader.ReadAsync(CancellationToken.None))
            {
                unprotected.Add(reader.GetString(0));
            }
        }

        Assert.Empty(unprotected);
    }

    /// <summary>
    /// Row-level security on cus is on, FORCEd, and has a policy.
    ///
    /// <b>All three, because one was being checked and the other two matter
    /// more.</b> The existing assertion read <c>pg_tables.rowsecurity</c>, which
    /// says RLS is switched on. It does not say a policy exists — a squashed
    /// migration that dropped one would leave the flag set — and it does not say
    /// <c>FORCE</c> is set. Without FORCE, RLS does not apply to the table's
    /// owner, and the application connects as the role that owns these tables:
    /// every policy in the product would be inert, leaving the EF query filter
    /// as the only guard, which is exactly the single point of failure having
    /// both is meant to avoid.
    /// </summary>
    [SkippableFact]
    public async Task Row_level_security_is_enabled_forced_and_policied_on_every_table()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using CustomerDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(
            string.Empty,
            string.Join(
                "; ",
                await BillBook.Tests.Shared.RlsAudit.UnprotectedAsync(db, "cus")));
    }
}
