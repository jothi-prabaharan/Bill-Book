using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Shared.Kernel.Persistence;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// The startup migration loop (TK-186): it waits for a database that is not
/// there yet, retries a transient failure or a lost create race a few times,
/// and gives up on anything else at once. Before it, a service waiting behind
/// another host's migration lock stopped after the 30-second default timeout,
/// and a service losing the race to create the tenant database stopped on 42P04.
/// </summary>
public class StartupMigrationTests
{
    private static Task Run(Func<CancellationToken, Task> migrate) =>
        StartupMigration.RunAsync(migrate, NullLogger.Instance, "Test", TimeSpan.Zero, CancellationToken.None);

    private static PostgresException Postgres(string sqlState) =>
        new("test", "ERROR", "ERROR", sqlState);

    /// <summary>Throws each of <paramref name="failures"/> in turn, then succeeds.</summary>
    private sealed class Script
    {
        private readonly Queue<Exception> _failures;

        public Script(params Exception[] failures) => _failures = new Queue<Exception>(failures);

        public int Calls { get; private set; }

        public Task MigrateAsync(CancellationToken ct)
        {
            Calls++;
            return _failures.TryDequeue(out Exception? failure) ? Task.FromException(failure) : Task.CompletedTask;
        }
    }

    [Fact]
    public async Task A_migration_that_succeeds_runs_once()
    {
        var script = new Script();

        await Run(script.MigrateAsync);

        Assert.Equal(1, script.Calls);
    }

    [Fact]
    public async Task A_missing_database_is_waited_for()
    {
        var script = new Script(
            Postgres(PostgresErrorCodes.InvalidCatalogName),
            Postgres(PostgresErrorCodes.InvalidCatalogName),
            Postgres(PostgresErrorCodes.InvalidCatalogName));

        await Run(script.MigrateAsync);

        Assert.Equal(4, script.Calls);
    }

    [Fact]
    public async Task A_missing_database_is_waited_for_longer_than_a_transient_failure_is_retried()
    {
        Exception[] failures = Enumerable.Range(0, StartupMigration.TransientAttempts + 2)
            .Select(_ => (Exception)Postgres(PostgresErrorCodes.InvalidCatalogName))
            .ToArray();
        var script = new Script(failures);

        await Run(script.MigrateAsync);

        Assert.Equal(failures.Length + 1, script.Calls);
    }

    [Fact]
    public async Task A_database_that_never_appears_is_given_up_on()
    {
        int calls = 0;

        PostgresException thrown = await Assert.ThrowsAsync<PostgresException>(() => Run(_ =>
        {
            calls++;
            throw Postgres(PostgresErrorCodes.InvalidCatalogName);
        }));

        Assert.Equal(PostgresErrorCodes.InvalidCatalogName, thrown.SqlState);
        Assert.Equal(StartupMigration.MissingDatabaseAttempts, calls);
    }

    [Theory]
    [InlineData(PostgresErrorCodes.DuplicateDatabase)]
    [InlineData(PostgresErrorCodes.DuplicateSchema)]
    [InlineData(PostgresErrorCodes.DuplicateTable)]
    [InlineData(PostgresErrorCodes.UniqueViolation)]
    public async Task Losing_a_create_race_to_another_host_is_retried(string sqlState)
    {
        var script = new Script(Postgres(sqlState));

        await Run(script.MigrateAsync);

        Assert.Equal(2, script.Calls);
    }

    [Fact]
    public async Task A_timeout_waiting_for_another_hosts_migration_lock_is_retried()
    {
        // What Npgsql throws when the command timeout ends a wait: the timeout
        // as the inner exception.
        var script = new Script(new NpgsqlException("Exception while reading from stream", new TimeoutException()));

        await Run(script.MigrateAsync);

        Assert.Equal(2, script.Calls);
    }

    [Fact]
    public async Task A_transient_failure_is_retried_a_few_times_then_thrown()
    {
        int calls = 0;

        await Assert.ThrowsAsync<TimeoutException>(() => Run(_ =>
        {
            calls++;
            throw new TimeoutException();
        }));

        Assert.Equal(StartupMigration.TransientAttempts, calls);
    }

    [Fact]
    public async Task Any_other_failure_is_thrown_at_once()
    {
        int calls = 0;

        PostgresException thrown = await Assert.ThrowsAsync<PostgresException>(() => Run(_ =>
        {
            calls++;
            throw Postgres(PostgresErrorCodes.SyntaxError);
        }));

        Assert.Equal(PostgresErrorCodes.SyntaxError, thrown.SqlState);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Cancellation_stops_the_wait()
    {
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => StartupMigration.RunAsync(
            _ =>
            {
                cts.Cancel();
                throw Postgres(PostgresErrorCodes.InvalidCatalogName);
            },
            NullLogger.Instance, "Test", TimeSpan.FromMinutes(1), cts.Token));
    }

    [Fact]
    public void The_predicates_name_what_they_should()
    {
        Assert.True(StartupMigration.IsMissingDatabase(Postgres(PostgresErrorCodes.InvalidCatalogName)));
        Assert.False(StartupMigration.IsMissingDatabase(Postgres(PostgresErrorCodes.DuplicateDatabase)));

        Assert.True(StartupMigration.IsConcurrentCreate(Postgres(PostgresErrorCodes.DuplicateDatabase)));
        Assert.False(StartupMigration.IsConcurrentCreate(Postgres(PostgresErrorCodes.InvalidCatalogName)));
        Assert.False(StartupMigration.IsConcurrentCreate(new TimeoutException()));

        Assert.True(StartupMigration.IsTransient(new TimeoutException()));
        Assert.True(StartupMigration.IsTransient(new NpgsqlException("read", new TimeoutException())));
        Assert.False(StartupMigration.IsTransient(Postgres(PostgresErrorCodes.SyntaxError)));
        Assert.False(StartupMigration.IsTransient(new InvalidOperationException()));
    }

    [Fact]
    public void The_migration_timeout_outlasts_the_default_by_a_wide_margin()
    {
        // Npgsql's default is 30 seconds, which is what a service waiting
        // behind another host's migration lock ran out of.
        Assert.True(StartupMigration.CommandTimeout >= TimeSpan.FromMinutes(5));
    }
}
