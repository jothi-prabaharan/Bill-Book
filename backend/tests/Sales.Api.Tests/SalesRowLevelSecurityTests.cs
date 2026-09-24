using Sales.Repository;
using Shared.Kernel.Errors;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// What the <c>sal</c> policies let through, asked as a role they bind.
///
/// The suite connects as <c>postgres</c>, a superuser, and a superuser bypasses
/// row-level security even when it is FORCEd, so no other test here can see a
/// policy work or fail. These tests seed as the superuser, then read and write
/// inside one uncommitted transaction under <c>SET LOCAL ROLE</c> to a role with
/// no bypass. The shape is <c>Accounting.Api.Tests.RowLevelSecurityTests</c>,
/// which TK-71 wrote as the template.
///
/// <b>What this does not cover:</b> Notification.Worker, which reads <c>sal</c>
/// with no tenant and so sees nothing under these policies until TK-20 gives it
/// one per branch.
///
/// Written for TK-03 and not run by the AI that wrote it (docs/TASKS.md 0.5).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class SalesRowLevelSecurityTests
{
    /// <summary>A role with no superuser and no BYPASSRLS, so policies bind it.</summary>
    private const string ProbeRole = "sal_rls_probe";

    private readonly PostgresFixture _postgres;

    public SalesRowLevelSecurityTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task With_no_tenant_set_queries_return_no_rows_and_do_not_throw()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = await SeedBranchAsync();

        await using NpgsqlConnection connection = await OpenAsProbeAsync(customerId, orgId);

        // '' is what the tenant interceptor sets when a request has none. Without
        // NULLIF in the policy, ''::uuid would throw here instead.
        await SetTenantAsync(connection, "", "");
        Assert.Equal(0, await CountAsync(connection, "ErrorLogs"));
        Assert.Equal(0, await CountAsync(connection, "SalesRegister"));
    }

    [SkippableFact]
    public async Task A_branch_sees_its_own_rows_and_no_one_elses()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = await SeedBranchAsync();

        await using NpgsqlConnection connection = await OpenAsProbeAsync(customerId, orgId);

        await SetTenantAsync(connection, customerId.ToString(), orgId.ToString());
        Assert.Equal(2, await CountAsync(connection, "ErrorLogs"));

        // Same customer, another branch: the OrgId half.
        await SetTenantAsync(connection, customerId.ToString(), Guid.NewGuid().ToString());
        Assert.Equal(0, await CountAsync(connection, "ErrorLogs"));

        // Same branch id under another customer: the CustomerId half.
        await SetTenantAsync(connection, Guid.NewGuid().ToString(), orgId.ToString());
        Assert.Equal(0, await CountAsync(connection, "ErrorLogs"));
    }

    [SkippableFact]
    public async Task A_row_cannot_be_written_into_another_branch()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = await SeedBranchAsync();

        await using NpgsqlConnection connection = await OpenAsProbeAsync(customerId, orgId);
        await SetTenantAsync(connection, customerId.ToString(), orgId.ToString());

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(
            () => InsertErrorLogAsync(connection, customerId, Guid.NewGuid()));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    [SkippableFact]
    public async Task A_branch_can_write_its_own_rows()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = await SeedBranchAsync();

        await using NpgsqlConnection connection = await OpenAsProbeAsync(customerId, orgId);
        await SetTenantAsync(connection, customerId.ToString(), orgId.ToString());

        int before = await CountAsync(connection, "ErrorLogs");
        await InsertErrorLogAsync(connection, customerId, orgId);

        Assert.Equal(before + 1, await CountAsync(connection, "ErrorLogs"));
    }

    private static IEnumerable<ErrorLog> ErrorLogs(Guid orgId, int count) =>
        Enumerable.Range(0, count).Select(_ => new ErrorLog
        {
            OrgId = orgId,
            ErrorReference = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            Source = ErrorSource.Api,
            ServiceName = "RlsProbe",
            Code = ApiErrorCode.Unexpected,
            ExceptionType = "RlsProbe",
            Message = "RLS probe row",
            FollowUpStatus = ErrorFollowUpStatus.Open,
        });

    /// <summary>
    /// A new branch with rows in it, written as the superuser through the context
    /// so CustomerId and OrgId are stamped the way the application stamps them.
    /// </summary>
    private async Task<(Guid CustomerId, Guid OrgId)> SeedBranchAsync()
    {
        var customerId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        await using SalesDbContext db = _postgres.CreateContext(customerId, orgId);

        // ErrorLogs because it is the one table here with no foreign keys to
        // satisfy; every table shares the one policy, so it stands for all of them.
        db.ErrorLogs.AddRange(ErrorLogs(orgId, 2));
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
        await using SalesDbContext db = _postgres.CreateContext(customerId, orgId);
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
            GRANT USAGE ON SCHEMA sal TO {ProbeRole};
            GRANT SELECT, INSERT ON ALL TABLES IN SCHEMA sal TO {ProbeRole};
            GRANT USAGE ON ALL SEQUENCES IN SCHEMA sal TO {ProbeRole};
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
        command.CommandText = $"SELECT count(*) FROM sal.\"{table}\"";

        return (int)(long)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>
    /// An error log row, the one table every tenant schema has. A savepoint around
    /// the insert, so a refusal leaves the transaction usable.
    /// </summary>
    private static async Task InsertErrorLogAsync(NpgsqlConnection connection, Guid customerId, Guid orgId)
    {
        await ExecuteAsync(connection, "SAVEPOINT probe_write;");

        try
        {
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO sal."ErrorLogs"
                    ("ErrorReference", "OccurredAt", "Source", "ServiceName", "Code",
                     "ExceptionType", "Message", "FollowUpStatus", "CustomerId", "OrgId")
                VALUES (gen_random_uuid(), now(), 'Api', 'RlsProbe', 'Unexpected',
                        'RlsProbe', 'RLS probe row', 'Open', @customer, @org)
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
