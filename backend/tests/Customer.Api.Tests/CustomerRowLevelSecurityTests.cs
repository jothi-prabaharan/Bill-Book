using Customer.Entity.TableEntities;
using Customer.Repository;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Customer.Api.Tests;

/// <summary>
/// What the <c>cus</c> policies let through, asked as a role they bind.
///
/// The suite connects as <c>postgres</c>, a superuser, and a superuser bypasses
/// row-level security even when it is FORCEd, so no other test here can see a
/// policy work or fail. These tests seed as the superuser, then read and write
/// inside one uncommitted transaction under <c>SET LOCAL ROLE</c> to a role with
/// no bypass. The shape is <c>Accounting.Api.Tests.RowLevelSecurityTests</c>,
/// which TK-02 wrote as the template.
///
/// Written for TK-04 and not run by the AI that wrote it (docs/TASKS.md 0.5).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class CustomerRowLevelSecurityTests
{
    /// <summary>A role with no superuser and no BYPASSRLS, so policies bind it.</summary>
    private const string ProbeRole = "cus_rls_probe";

    private const int SeededLeads = 2;

    private readonly PostgresFixture _postgres;

    public CustomerRowLevelSecurityTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task With_no_tenant_set_leads_return_no_rows_and_do_not_throw()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = await SeedBranchAsync();

        await using NpgsqlConnection connection = await OpenAsProbeAsync(customerId, orgId);

        await SetTenantAsync(connection, "", "");
        Assert.Equal(0, await CountAsync(connection, "Leads"));
        Assert.Equal(0, await CountAsync(connection, "Tickets"));
    }

    [SkippableFact]
    public async Task A_branch_sees_its_own_leads_and_no_one_elses()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = await SeedBranchAsync();

        await using NpgsqlConnection connection = await OpenAsProbeAsync(customerId, orgId);

        await SetTenantAsync(connection, customerId.ToString(), orgId.ToString());
        Assert.Equal(SeededLeads, await CountAsync(connection, "Leads"));

        // Same customer, another branch: the OrgId half.
        await SetTenantAsync(connection, customerId.ToString(), Guid.NewGuid().ToString());
        Assert.Equal(0, await CountAsync(connection, "Leads"));

        // Same branch id under another customer: the CustomerId half.
        await SetTenantAsync(connection, Guid.NewGuid().ToString(), orgId.ToString());
        Assert.Equal(0, await CountAsync(connection, "Leads"));
    }

    [SkippableFact]
    public async Task A_lead_cannot_be_written_into_another_branch()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = await SeedBranchAsync();

        await using NpgsqlConnection connection = await OpenAsProbeAsync(customerId, orgId);
        await SetTenantAsync(connection, customerId.ToString(), orgId.ToString());

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(
            () => InsertLeadAsync(connection, customerId, Guid.NewGuid()));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    [SkippableFact]
    public async Task A_branch_can_write_its_own_leads()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = await SeedBranchAsync();

        await using NpgsqlConnection connection = await OpenAsProbeAsync(customerId, orgId);
        await SetTenantAsync(connection, customerId.ToString(), orgId.ToString());

        await InsertLeadAsync(connection, customerId, orgId);

        Assert.Equal(SeededLeads + 1, await CountAsync(connection, "Leads"));
    }

    /// <summary>
    /// A new branch with two leads, written as the superuser through the context
    /// so CustomerId and OrgId are stamped the way the application stamps them.
    /// </summary>
    private async Task<(Guid CustomerId, Guid OrgId)> SeedBranchAsync()
    {
        var customerId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        await using CustomerDbContext db = _postgres.CreateContext(customerId, orgId);

        db.Leads.Add(new Lead { Name = "RLS probe lead one" });
        db.Leads.Add(new Lead { Name = "RLS probe lead two" });
        await db.SaveChangesAsync(CancellationToken.None);

        return (customerId, orgId);
    }

    /// <summary>
    /// A connection inside an open transaction, switched to the probe role. It is
    /// never committed: disposing the connection rolls back the role switch and
    /// anything written under it.
    /// </summary>
    private async Task<NpgsqlConnection> OpenAsProbeAsync(Guid customerId, Guid orgId)
    {
        await using CustomerDbContext db = _postgres.CreateContext(customerId, orgId);
        var connection = new NpgsqlConnection(db.Database.GetConnectionString());

        await connection.OpenAsync();

        // CREATE ROLE is cluster-wide and has no IF NOT EXISTS, hence the block.
        await ExecuteAsync(connection, $"""
            DO $$
            BEGIN
                CREATE ROLE {ProbeRole} NOLOGIN NOSUPERUSER NOBYPASSRLS;
            EXCEPTION WHEN duplicate_object THEN NULL;
            END
            $$;
            GRANT USAGE ON SCHEMA cus TO {ProbeRole};
            GRANT SELECT, INSERT ON ALL TABLES IN SCHEMA cus TO {ProbeRole};
            GRANT USAGE ON ALL SEQUENCES IN SCHEMA cus TO {ProbeRole};
            """);

        await ExecuteAsync(connection, $"BEGIN; SET LOCAL ROLE {ProbeRole};");

        return connection;
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, string customerId, string orgId)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT set_config('app.current_customer_id', @customer, true), "
            + "set_config('app.current_org_id', @org, true)";
        command.Parameters.AddWithValue("customer", customerId);
        command.Parameters.AddWithValue("org", orgId);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> CountAsync(NpgsqlConnection connection, string table)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT count(*) FROM cus.\"{table}\"";

        return (int)(long)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>A savepoint around the insert, so a refusal leaves the transaction usable.</summary>
    private static async Task InsertLeadAsync(NpgsqlConnection connection, Guid customerId, Guid orgId)
    {
        await ExecuteAsync(connection, "SAVEPOINT probe_write;");

        try
        {
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO cus."Leads" ("Name", "Source", "Status", "CustomerId", "OrgId")
                VALUES ('RLS probe insert', 'Other', 'New', @customer, @org)
                """;
            command.Parameters.AddWithValue("customer", customerId);
            command.Parameters.AddWithValue("org", orgId);

            await command.ExecuteNonQueryAsync();
            await ExecuteAsync(connection, "RELEASE SAVEPOINT probe_write;");
        }
        catch (PostgresException)
        {
            await ExecuteAsync(connection, "ROLLBACK TO SAVEPOINT probe_write;");
            throw;
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
