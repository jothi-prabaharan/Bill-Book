using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Accounting.Repository.SeedData;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// What the <c>acc</c> policies actually let through, asked of PostgreSQL as a
/// role they apply to.
///
/// <b>Why a role switch.</b> The suite connects as <c>postgres</c>, a superuser,
/// and a superuser bypasses row-level security entirely, <c>FORCE</c> or not.
/// Every other test here therefore sees every row whatever the policies say,
/// which is also why none of them broke when the policies were restored. These
/// tests seed as the superuser, then run their reads and writes inside one
/// transaction under <c>SET LOCAL ROLE</c> to a role with no bypass, so the
/// policy is the only thing deciding.
///
/// <b>Raw SQL, deliberately.</b> <c>SET ROLE</c> and <c>set_config</c> have no
/// LINQ form, and the point is to ask the database without the EF query filter
/// in the way. <c>RlsAudit</c> checks the catalog: RLS on, FORCEd, a policy
/// present. This checks what the policy expression does.
///
/// Written for TK-71 and not run by the AI that wrote it (docs/TASKS.md 0.5).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class RowLevelSecurityTests
{
    /// <summary>A role with no superuser and no BYPASSRLS, so policies bind it.</summary>
    private const string ProbeRole = "acc_rls_probe";

    private readonly PostgresFixture _postgres;

    public RowLevelSecurityTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task With_no_tenant_set_accounts_return_no_rows_and_do_not_throw()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = await SeedBranchAsync();

        await using NpgsqlConnection connection = await OpenAsProbeAsync(customerId, orgId);

        // Both cleared the way RlsConnectionInterceptor clears them: an empty
        // string, which ''::uuid would throw on and NULLIF turns into NULL.
        await SetTenantAsync(connection, "", "");
        Assert.Equal(0, await CountAsync(connection, "Accounts"));
        Assert.Equal(0, await CountAsync(connection, "PaymentTerms"));
    }

    [SkippableFact]
    public async Task A_branch_sees_its_own_accounts_and_no_one_elses()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = await SeedBranchAsync();
        int seeded = ChartOfAccountsSeed.Build(orgId).Count;

        await using NpgsqlConnection connection = await OpenAsProbeAsync(customerId, orgId);

        await SetTenantAsync(connection, customerId.ToString(), orgId.ToString());
        Assert.Equal(seeded, await CountAsync(connection, "Accounts"));

        // Same customer, another branch: the OrgId half of the policy.
        await SetTenantAsync(connection, customerId.ToString(), Guid.NewGuid().ToString());
        Assert.Equal(0, await CountAsync(connection, "Accounts"));

        // Same branch id under another customer: the CustomerId half, which is
        // the defence in depth under an OrgId that is already globally unique.
        await SetTenantAsync(connection, Guid.NewGuid().ToString(), orgId.ToString());
        Assert.Equal(0, await CountAsync(connection, "Accounts"));
    }

    [SkippableFact]
    public async Task A_row_cannot_be_written_into_another_branch()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = await SeedBranchAsync();

        await using NpgsqlConnection connection = await OpenAsProbeAsync(customerId, orgId);
        await SetTenantAsync(connection, customerId.ToString(), orgId.ToString());

        // FOR ALL with no WITH CHECK reuses USING as the insert check.
        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(
            () => InsertPaymentTermAsync(connection, customerId, Guid.NewGuid()));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    [SkippableFact]
    public async Task With_no_tenant_set_nothing_can_be_written()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = await SeedBranchAsync();

        await using NpgsqlConnection connection = await OpenAsProbeAsync(customerId, orgId);
        await SetTenantAsync(connection, "", "");

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(
            () => InsertPaymentTermAsync(connection, customerId, orgId));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    [SkippableFact]
    public async Task A_branch_can_write_its_own_rows()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = await SeedBranchAsync();

        await using NpgsqlConnection connection = await OpenAsProbeAsync(customerId, orgId);
        await SetTenantAsync(connection, customerId.ToString(), orgId.ToString());

        int before = await CountAsync(connection, "PaymentTerms");
        await InsertPaymentTermAsync(connection, customerId, orgId);

        Assert.Equal(before + 1, await CountAsync(connection, "PaymentTerms"));
    }

    /// <summary>
    /// One new branch with the seeded chart of accounts and a payment term,
    /// written as the superuser the suite connects as, through the context
    /// so CustomerId and OrgId are stamped the way the application stamps them.
    /// </summary>
    private async Task<(Guid CustomerId, Guid OrgId)> SeedBranchAsync()
    {
        var customerId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        await using AccountingDbContext db = _postgres.CreateContext(customerId, orgId);

        db.Accounts.AddRange(ChartOfAccountsSeed.Build(orgId));
        db.PaymentTerms.Add(new PaymentTerm { TermName = "RLS probe term" });
        await db.SaveChangesAsync(CancellationToken.None);

        return (customerId, orgId);
    }

    /// <summary>
    /// A connection inside an open transaction, switched to the probe role. The
    /// transaction is never committed: disposing the connection rolls it back,
    /// so the role switch and anything written under it go with it.
    /// </summary>
    private async Task<NpgsqlConnection> OpenAsProbeAsync(Guid customerId, Guid orgId)
    {
        await using AccountingDbContext db = _postgres.CreateContext(customerId, orgId);
        var connection = new NpgsqlConnection(db.Database.GetConnectionString());

        await connection.OpenAsync();

        // CREATE ROLE is cluster-wide and has no IF NOT EXISTS, hence the block.
        // Both this and the grants are idempotent, so every test can run it.
        await ExecuteAsync(connection, $"""
            DO $$
            BEGIN
                CREATE ROLE {ProbeRole} NOLOGIN NOSUPERUSER NOBYPASSRLS;
            EXCEPTION WHEN duplicate_object THEN NULL;
            END
            $$;
            GRANT USAGE ON SCHEMA acc TO {ProbeRole};
            GRANT SELECT, INSERT ON ALL TABLES IN SCHEMA acc TO {ProbeRole};
            GRANT USAGE ON ALL SEQUENCES IN SCHEMA acc TO {ProbeRole};
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
        command.CommandText = $"SELECT count(*) FROM acc.\"{table}\"";

        return (int)(long)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>
    /// A savepoint around the insert, so a refused write leaves the transaction
    /// usable for the assertions after it.
    /// </summary>
    private static async Task InsertPaymentTermAsync(NpgsqlConnection connection, Guid customerId, Guid orgId)
    {
        await ExecuteAsync(connection, "SAVEPOINT probe_write;");

        try
        {
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO acc."PaymentTerms"
                    ("TermName", "TermType", "DueDays", "DiscountPercent", "DiscountDays",
                     "IsSales", "IsPurchase", "IsDefault", "IsSystem", "IsActive", "DisplayOrder",
                     "CustomerId", "OrgId")
                VALUES
                    ('RLS probe insert', 'Net', 0, 0, 0,
                     true, true, false, false, true, 0,
                     @customer, @org)
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
