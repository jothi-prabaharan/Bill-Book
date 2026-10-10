using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Shared.Kernel.Persistence;

/// <summary>
/// How every host migrates at startup (TK-186): each service's
/// <c>DatabaseMigrationService</c>, Master's own and its pass over every tenant
/// schema, the gateway and the worker.
///
/// <b>A long command timeout, for the migration only.</b> Npgsql takes
/// <c>LOCK TABLE {schema}."__EFMigrationsHistory" IN ACCESS EXCLUSIVE MODE</c>
/// for a migration, and Master migrates every tenant schema while each service
/// migrates its own, so on a fresh database one of the two waits for the other
/// to finish. Under the 30-second default that wait was cut off — over the
/// remote Development database one schema takes longer than that — and the
/// service stopped with <i>Timeout during reading attempt</i>. The timeout is
/// put back afterwards, so requests keep the connection string's.
///
/// <b>A wait for a database that is not there yet.</b> Master creates the
/// tenant database after migrating its own, which over a slow link is longer
/// than a few seconds.
///
/// <b>A retry on a transient failure</b>, a dropped connection for instance,
/// and on losing a race to create something another host was creating at the
/// same moment. EF creates a missing database itself, and the history table
/// and schema before it takes the lock, so on a fresh server two hosts can
/// both find the tenant database or a schema missing and both create it; the
/// loser gets 42P04, 42P06, 42P07 or 23505, and its next attempt finds it
/// there. A migration runs in one transaction, so a failed attempt leaves
/// nothing behind and the next one starts clean.
///
/// This is the startup resilience, deliberately not
/// <c>EnableRetryOnFailure</c>: a retrying execution strategy refuses every
/// <c>BeginTransactionAsync</c>, and every write endpoint opens one (hard rule 13).
/// </summary>
public static class StartupMigration
{
    /// <summary>The command timeout while migrating: long enough to wait out another host's migration of the same schema.</summary>
    public static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(10);

    /// <summary>The pause between attempts.</summary>
    public static readonly TimeSpan Pause = TimeSpan.FromSeconds(5);

    /// <summary>Attempts while the database does not exist yet: ten minutes at <see cref="Pause"/>.</summary>
    public const int MissingDatabaseAttempts = 120;

    /// <summary>Attempts after a transient failure.</summary>
    public const int TransientAttempts = 5;

    /// <summary>Applies the context's pending migrations, waiting and retrying as above.</summary>
    public static async Task MigrateAsync(DatabaseFacade database, ILogger logger, string name, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(database);

        int? previous = database.GetCommandTimeout();
        database.SetCommandTimeout(CommandTimeout);
        try
        {
            await RunAsync(database.MigrateAsync, logger, name, Pause, ct);
        }
        finally
        {
            database.SetCommandTimeout(previous);
        }
    }

    /// <summary>The attempt loop, apart from EF so it can be tested without a database.</summary>
    public static async Task RunAsync(
        Func<CancellationToken, Task> migrate, ILogger logger, string name, TimeSpan pause, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(migrate);
        ArgumentNullException.ThrowIfNull(logger);

        int missingDatabase = 0;
        int transient = 0;

        while (true)
        {
            try
            {
                logger.LogInformation("Migrating {Name}...", name);
                await migrate(ct);
                logger.LogInformation("{Name} migration complete.", name);
                return;
            }
            catch (Exception ex) when (IsMissingDatabase(ex) && ++missingDatabase < MissingDatabaseAttempts)
            {
                logger.LogWarning(
                    "The database for {Name} does not exist yet; waiting for Master.Api to create it (attempt {Attempt} of {Attempts}).",
                    name, missingDatabase, MissingDatabaseAttempts);
            }
            catch (Exception ex) when ((IsTransient(ex) || IsConcurrentCreate(ex))
                && !ct.IsCancellationRequested
                && ++transient < TransientAttempts)
            {
                logger.LogWarning(ex,
                    "Migrating {Name} failed on a transient error or a concurrent create; retrying (attempt {Attempt} of {Attempts}).",
                    name, transient, TransientAttempts);
            }

            await Task.Delay(pause, ct);
        }
    }

    /// <summary>The database named by the connection string does not exist (3D000).</summary>
    public static bool IsMissingDatabase(Exception ex) =>
        ex is PostgresException { SqlState: PostgresErrorCodes.InvalidCatalogName };

    /// <summary>
    /// Another host created the same database, schema or table at the same
    /// moment: 42P04, 42P06, 42P07, or 23505 when two <c>IF NOT EXISTS</c>
    /// creates collide in the catalog.
    /// </summary>
    public static bool IsConcurrentCreate(Exception ex) =>
        ex is PostgresException
        {
            SqlState: PostgresErrorCodes.DuplicateDatabase
                or PostgresErrorCodes.DuplicateSchema
                or PostgresErrorCodes.DuplicateTable
                or PostgresErrorCodes.UniqueViolation,
        };

    /// <summary>
    /// Worth another attempt: a failure Npgsql calls transient (a dropped or
    /// refused connection, a server starting up) or a timeout.
    /// </summary>
    public static bool IsTransient(Exception ex) =>
        ex is NpgsqlException { IsTransient: true }
        || ex is TimeoutException
        || ex.InnerException is TimeoutException;
}
