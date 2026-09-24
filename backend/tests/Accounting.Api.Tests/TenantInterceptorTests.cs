using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// <see cref="RlsConnectionInterceptor"/> against a role the policies bind
/// (TK-08): the tenant is set transaction-locally, on every command, and
/// never outlives the command's transaction.
///
/// <see cref="RowLevelSecurityTests"/> proves what the policy expression lets
/// through, with <c>set_config</c> called by hand. This proves the application's
/// own path: a context wired the way every host wires it, logging in as a
/// <c>NOSUPERUSER NOBYPASSRLS</c> role, with no explicit transaction around the
/// reads, which is how most of the product reads.
///
/// Written for TK-08 and not run by the AI that wrote it (docs/TASKS.md 0.5).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class TenantInterceptorTests
{
    private const string ProbeRole = "acc_tenant_probe";

    private const string ProbePassword = "acc_tenant_probe_pw";

    private readonly PostgresFixture _postgres;

    public TenantInterceptorTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>Two accounts in one branch, written as the superuser.</summary>
    private async Task<(Guid CustomerId, Guid OrgId)> SeedBranchAsync()
    {
        var customerId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        await using AccountingDbContext db = _postgres.CreateContext(customerId, orgId);
        db.Accounts.AddRange(
            new Account { OrgId = orgId, AccountTypeId = 1, AccountCode = "1010", AccountName = "Cash", IsActive = true },
            new Account { OrgId = orgId, AccountTypeId = 5, AccountCode = "6100", AccountName = "Rent", IsActive = true });
        await db.SaveChangesAsync();

        return (customerId, orgId);
    }

    private async Task<string> ProbeConnectionStringAsync()
    {
        await using AccountingDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        string admin = db.Database.GetConnectionString()!;

        await using (var connection = new NpgsqlConnection(admin))
        {
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand($"""
                DO $$
                BEGIN
                    CREATE ROLE {ProbeRole} LOGIN PASSWORD '{ProbePassword}' NOSUPERUSER NOBYPASSRLS;
                EXCEPTION WHEN duplicate_object THEN NULL;
                END
                $$;
                GRANT USAGE ON SCHEMA acc TO {ProbeRole};
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA acc TO {ProbeRole};
                GRANT USAGE ON ALL SEQUENCES IN SCHEMA acc TO {ProbeRole};
                """, connection);
            await command.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(admin)
        {
            Username = ProbeRole,
            Password = ProbePassword,

            // Pooled on purpose: a setting that outlived its transaction would
            // be carried to the next context that took this connection, which
            // is the leak transaction-local exists to prevent.
            Pooling = true,
            MaxPoolSize = 1,
        }.ConnectionString;
    }

    /// <summary>A context as the hosts build it: the interceptor reads the tenant at each command.</summary>
    private static AccountingDbContext Context(string connectionString, TenantContext tenant) =>
        new(
            new DbContextOptionsBuilder<AccountingDbContext>()
                .UseNpgsql(connectionString)
                .AddInterceptors(new RlsConnectionInterceptor(tenant))
                .Options,
            tenant);

    [SkippableFact]
    public async Task A_read_with_no_explicit_transaction_sees_its_own_branch()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = await SeedBranchAsync();
        string probe = await ProbeConnectionStringAsync();

        await using AccountingDbContext db = Context(probe, new TenantContext { CustomerId = customerId, OrgId = orgId });

        Assert.Equal(2, await db.Accounts.CountAsync());
    }

    /// <summary>
    /// The query filter is bypassed on purpose, so the policy is the only thing
    /// between this context and the other branch's rows.
    /// </summary>
    [SkippableFact]
    public async Task Another_branch_reads_zero_rows_even_past_the_query_filter()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = await SeedBranchAsync();
        string probe = await ProbeConnectionStringAsync();

        await using AccountingDbContext db = Context(probe, new TenantContext { CustomerId = customerId, OrgId = Guid.NewGuid() });

        Assert.Equal(0, await db.Accounts.IgnoreQueryFilters().CountAsync(a => a.OrgId == orgId));
    }

    [SkippableFact]
    public async Task No_tenant_sees_no_rows_and_does_not_throw()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (_, Guid orgId) = await SeedBranchAsync();
        string probe = await ProbeConnectionStringAsync();

        await using AccountingDbContext db = Context(probe, new TenantContext());

        Assert.Equal(0, await db.Accounts.IgnoreQueryFilters().CountAsync(a => a.OrgId == orgId));
    }

    /// <summary>
    /// The branch as it stands when the command runs is the one that applies.
    /// An internal route takes its branch from the request after the reliability
    /// filter has already opened the transaction; a setting taken once at BEGIN
    /// would have held no branch at all.
    /// </summary>
    [SkippableFact]
    public async Task A_tenant_set_after_the_transaction_opened_applies_to_the_next_command()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = await SeedBranchAsync();
        string probe = await ProbeConnectionStringAsync();

        var tenant = new TenantContext();
        await using AccountingDbContext db = Context(probe, tenant);
        await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync();

        Assert.Equal(0, await db.Accounts.IgnoreQueryFilters().CountAsync(a => a.OrgId == orgId));

        tenant.CustomerId = customerId;
        tenant.OrgId = orgId;

        Assert.Equal(2, await db.Accounts.CountAsync());
    }

    /// <summary>
    /// Nothing survives the command's transaction. With a pool of one, the next
    /// context gets the very same connection, and must see no tenant on it.
    /// </summary>
    [SkippableFact]
    public async Task The_setting_does_not_outlive_its_transaction_on_a_pooled_connection()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = await SeedBranchAsync();
        string probe = await ProbeConnectionStringAsync();

        await using (AccountingDbContext first = Context(probe, new TenantContext { CustomerId = customerId, OrgId = orgId }))
        {
            Assert.Equal(2, await first.Accounts.CountAsync());

            // Inside one explicit transaction as well, committed.
            await using IDbContextTransaction tx = await first.Database.BeginTransactionAsync();
            Assert.Equal(2, await first.Accounts.CountAsync());
            await tx.CommitAsync();
        }

        await using var connection = new NpgsqlConnection(probe);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT coalesce(current_setting('app.current_org_id', true), '')", connection);

        Assert.Equal("", await command.ExecuteScalarAsync());
    }

    [SkippableFact]
    public async Task A_write_into_another_branch_is_refused()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (Guid customerId, _) = await SeedBranchAsync();
        string probe = await ProbeConnectionStringAsync();

        // TenantDbContext refuses a row for another branch in C# before it is
        // sent, so the context is bound to the row's branch and only the
        // interceptor names a different one. The policy is then the only thing
        // that can refuse the insert, which is what this is asking.
        var rowBranch = new TenantContext { CustomerId = customerId, OrgId = Guid.NewGuid() };
        var sessionBranch = new TenantContext { CustomerId = customerId, OrgId = Guid.NewGuid() };

        await using var db = new AccountingDbContext(
            new DbContextOptionsBuilder<AccountingDbContext>()
                .UseNpgsql(probe)
                .AddInterceptors(new RlsConnectionInterceptor(sessionBranch))
                .Options,
            rowBranch);

        db.Accounts.Add(new Account
        {
            AccountTypeId = 1,
            AccountCode = "9999",
            AccountName = "Planted",
            IsActive = true,
        });

        DbUpdateException error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        PostgresException pg = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, pg.SqlState);
    }
}
