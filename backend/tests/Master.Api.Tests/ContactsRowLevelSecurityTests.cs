using Master.Api.Controllers;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shared.Kernel.Security;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// What the <c>con</c> policies let through, asked as a role they bind.
///
/// <b>Why a separate login.</b> The suite connects as <c>postgres</c>, a
/// superuser, and a superuser bypasses row-level security even when it is
/// FORCEd, so every other test here sees every row whatever the policies say.
/// These tests seed as the superuser, then read and write as
/// <see cref="ProbeRole"/>: a login with no superuser and no BYPASSRLS, so the
/// policy is the only thing deciding. A login rather than <c>SET ROLE</c>,
/// because the API-key test goes through the controller and its DbContext,
/// which opens connections of its own.
///
/// <b>ApiClients has its own policy</b> (see the con EnableRowLevelSecurity
/// migration): a request with a customer and no branch sees every branch of
/// that customer, because an API key names its customer but not its branch.
/// These tests pin that exception to exactly that shape.
///
/// Written for TK-03 and not run by the AI that wrote it (docs/TASKS.md 0.5).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ContactsRowLevelSecurityTests
{
    private const string ProbeRole = "con_rls_probe";
    private const string ProbePassword = "con_rls_probe";

    private readonly PostgresFixture _postgres;

    public ContactsRowLevelSecurityTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task With_no_tenant_set_contacts_and_keys_return_no_rows_and_do_not_throw()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (_, Guid orgA, _) = await SeedKeysAsync("bb_unused");

        await using NpgsqlConnection probe = await OpenAsProbeAsync();

        await SetTenantAsync(probe, "", "");
        Assert.Equal(0, await CountAsync(probe, "Contacts"));
        Assert.Equal(0, await CountAsync(probe, "ApiClients"));

        // The branch alone is not enough either: the customer half still fails.
        await SetTenantAsync(probe, "", orgA.ToString());
        Assert.Equal(0, await CountAsync(probe, "ApiClients"));
    }

    [SkippableFact]
    public async Task A_customer_with_no_branch_sees_every_branch_of_its_own_keys_and_no_one_elses()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (Guid customerId, _, _) = await SeedKeysAsync("bb_unused");
        (Guid otherCustomer, _, _) = await SeedKeysAsync("bb_unused");

        await using NpgsqlConnection probe = await OpenAsProbeAsync();

        await SetTenantAsync(probe, customerId.ToString(), "");
        Assert.Equal(2, await CountAsync(probe, "ApiClients", customerId));
        Assert.Equal(0, await CountAsync(probe, "ApiClients", otherCustomer));
    }

    [SkippableFact]
    public async Task A_request_with_a_branch_sees_only_that_branchs_keys()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgA, Guid orgB) = await SeedKeysAsync("bb_unused");

        await using NpgsqlConnection probe = await OpenAsProbeAsync();

        await SetTenantAsync(probe, customerId.ToString(), orgA.ToString());
        Assert.Equal(1, await CountAsync(probe, "ApiClients", customerId));

        await SetTenantAsync(probe, customerId.ToString(), orgB.ToString());
        Assert.Equal(1, await CountAsync(probe, "ApiClients", customerId));

        await SetTenantAsync(probe, customerId.ToString(), Guid.NewGuid().ToString());
        Assert.Equal(0, await CountAsync(probe, "ApiClients", customerId));
    }

    /// <summary>
    /// The card's own Done-when: API-key validation still works with RLS on. It
    /// runs the real controller, over a context whose connections log in as the
    /// probe role and go through <see cref="RlsConnectionInterceptor"/>, the way
    /// Master's own registration wires them.
    /// </summary>
    [SkippableFact]
    public async Task Api_key_validation_still_resolves_the_branch_under_row_level_security()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var customerId = Guid.NewGuid();
        string key = $"bb_{customerId:N}_{Guid.NewGuid():N}";

        (_, _, Guid orgB) = await SeedKeysAsync(key, customerId);

        ApiKeyValidationResult valid = await ValidateAsProbeAsync(key);

        Assert.True(valid.IsValid);
        Assert.Equal(customerId, valid.CustomerId);
        Assert.Equal(orgB, valid.OrgId);

        // Right format, right customer, wrong secret.
        ApiKeyValidationResult wrong = await ValidateAsProbeAsync($"bb_{customerId:N}_{Guid.NewGuid():N}");
        Assert.False(wrong.IsValid);
    }

    /// <summary>
    /// Two branches of one customer, each with one active key. The key that
    /// verifies against <paramref name="keyInBranchB"/> is in branch B; the one in
    /// branch A hashes something else.
    /// </summary>
    private async Task<(Guid CustomerId, Guid OrgA, Guid OrgB)> SeedKeysAsync(
        string keyInBranchB, Guid? customer = null)
    {
        Guid customerId = customer ?? Guid.NewGuid();
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();

        // TenantDbContext stamps CustomerId and OrgId on insert, so each branch's
        // key is written through a context bound to that branch.
        await using (ContactsDbContext a = _postgres.CreateContext(customerId, orgA))
        {
            a.ApiClients.Add(new ApiClient
            {
                Name = "Branch A client",
                HashedApiKey = BCrypt.Net.BCrypt.HashPassword($"bb_{customerId:N}_{Guid.NewGuid():N}"),
                RoleId = 1,
            });
            await a.SaveChangesAsync(CancellationToken.None);
        }

        await using (ContactsDbContext b = _postgres.CreateContext(customerId, orgB))
        {
            b.ApiClients.Add(new ApiClient
            {
                Name = "Branch B client",
                HashedApiKey = BCrypt.Net.BCrypt.HashPassword(keyInBranchB),
                RoleId = 1,
            });
            await b.SaveChangesAsync(CancellationToken.None);
        }

        return (customerId, orgA, orgB);
    }

    private async Task<ApiKeyValidationResult> ValidateAsProbeAsync(string key)
    {
        string probeConnection = await ProbeConnectionStringAsync();

        var services = new ServiceCollection();
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<RlsConnectionInterceptor>();
        services.AddDbContext<ContactsDbContext>((sp, options) => options
            .UseNpgsql(probeConnection)
            .AddInterceptors(sp.GetRequiredService<RlsConnectionInterceptor>()));

        await using ServiceProvider root = services.BuildServiceProvider();
        await using AsyncServiceScope scope = root.CreateAsyncScope();

        var controller = new InternalApiKeysController(
            scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<TenantContext>());

        ActionResult<ApiKeyValidationResult> result = await controller.Validate(
            new ValidateApiKeyRequest { ApiKey = key }, CancellationToken.None);

        return Assert.IsType<ApiKeyValidationResult>(result.Value);
    }

    /// <summary>
    /// The probe login, created if missing and granted what the application
    /// itself needs on con. Returned already open.
    /// </summary>
    private async Task<NpgsqlConnection> OpenAsProbeAsync()
    {
        var connection = new NpgsqlConnection(await ProbeConnectionStringAsync());
        await connection.OpenAsync();
        return connection;
    }

    private async Task<string> ProbeConnectionStringAsync()
    {
        await using ContactsDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        string admin = db.Database.GetConnectionString()!;

        await using (var connection = new NpgsqlConnection(admin))
        {
            await connection.OpenAsync();

            // CREATE ROLE is cluster-wide and has no IF NOT EXISTS, hence the
            // block. The grants are idempotent, so every test can run this.
            await ExecuteAsync(connection, $"""
                DO $$
                BEGIN
                    CREATE ROLE {ProbeRole} LOGIN PASSWORD '{ProbePassword}' NOSUPERUSER NOBYPASSRLS;
                EXCEPTION WHEN duplicate_object THEN NULL;
                END
                $$;
                GRANT USAGE ON SCHEMA con TO {ProbeRole};
                GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA con TO {ProbeRole};
                GRANT USAGE ON ALL SEQUENCES IN SCHEMA con TO {ProbeRole};
                """);
        }

        return new NpgsqlConnectionStringBuilder(admin)
        {
            Username = ProbeRole,
            Password = ProbePassword,
            // A pooled connection would carry one test's set_config into the next.
            Pooling = false,
        }.ConnectionString;
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, string customerId, string orgId)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT set_config('app.current_customer_id', @customer, false), "
            + "set_config('app.current_org_id', @org, false)";
        command.Parameters.AddWithValue("customer", customerId);
        command.Parameters.AddWithValue("org", orgId);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> CountAsync(NpgsqlConnection connection, string table, Guid? customerId = null)
    {
        await using NpgsqlCommand command = connection.CreateCommand();

        // Narrowed to one customer where the database is shared with other tests,
        // so a count means this test's rows and nothing else.
        command.CommandText = customerId is null
            ? $"SELECT count(*) FROM con.\"{table}\""
            : $"SELECT count(*) FROM con.\"{table}\" WHERE \"CustomerId\" = @customer";

        if (customerId is Guid id)
        {
            command.Parameters.AddWithValue("customer", id);
        }

        return (int)(long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
