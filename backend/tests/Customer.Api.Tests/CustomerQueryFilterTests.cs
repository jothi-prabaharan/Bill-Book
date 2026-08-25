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
}
