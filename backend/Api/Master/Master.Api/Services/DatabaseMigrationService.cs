using System.Data.Common;
using Master.Repository;
using Master.Repository.SeedData;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Master.Api.Services;

/// <summary>
/// Runs EF Core database migrations on application startup.
/// Ensures that databases are created with UTF8 encoding explicitly (bypassing EF Core's Windows default of WIN1252),
/// runs EF migrations for Admin and Contacts contexts, and seeds the global geography data.
/// </summary>
public class DatabaseMigrationService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DatabaseMigrationService> _logger;
    private readonly IConfiguration _config;

    public DatabaseMigrationService(IServiceProvider services, ILogger<DatabaseMigrationService> logger, IConfiguration config)
    {
        _services = services;
        _logger = logger;
        _config = config;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting automated database setup and migrations...");

        using var scope = _services.CreateScope();

        // 1. Ensure databases exist with correct encoding
        string adminDbString = RequiredConnectionString("AdminDatabase");

        string tenantDbString = RequiredConnectionString("TenantDatabase");

        await EnsureDatabaseExistsAsync(adminDbString, cancellationToken);

        await EnsureDatabaseExistsAsync(tenantDbString, cancellationToken);

        // 2. Run EF Core Migrations
        var adminDb = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        _logger.LogInformation("Migrating Admin database...");
        await adminDb.Database.MigrateAsync(cancellationToken);

        var contactsDb = scope.ServiceProvider.GetRequiredService<ContactsDbContext>();
        _logger.LogInformation("Migrating tenant database...");
        await contactsDb.Database.MigrateAsync(cancellationToken);

        // 3. Seed Geography Data
        _logger.LogInformation("Seeding global geography data...");
        string jsonPath = Path.Combine(AppContext.BaseDirectory, "SeedData", "states.json");
        if (File.Exists(jsonPath))
        {
            await using var stream = File.OpenRead(jsonPath);
            await GeographyJsonLoader.ImportStatesAsync(adminDb, stream, cancellationToken);
            _logger.LogInformation("Geography seeding complete.");
        }
        else
        {
            _logger.LogWarning("states.json not found at {Path}. Skipping geography seeding.", jsonPath);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task EnsureDatabaseExistsAsync(string connectionString, CancellationToken ct)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        string targetDatabase = builder.Database!;

        // Connect to the default 'postgres' database to check/create the target
        builder.Database = "postgres";
        string rootConnection = builder.ConnectionString;

        await using var connection = new NpgsqlConnection(rootConnection);
        await connection.OpenAsync(ct);

        bool exists;
        await using (var checkCmd = new NpgsqlCommand($"SELECT 1 FROM pg_database WHERE datname = '{targetDatabase}'", connection))
        {
            var result = await checkCmd.ExecuteScalarAsync(ct);
            exists = result != null;
        }

        if (!exists)
        {
            _logger.LogInformation("Creating database {Database}...", targetDatabase);
            string sql = $"CREATE DATABASE \"{targetDatabase}\" ENCODING 'UTF8' TEMPLATE template0";
            await using var createCmd = new NpgsqlCommand(sql, connection);
            await createCmd.ExecuteNonQueryAsync(ct);
        }
    }

    private string GetConnectionStringForDb(string connectionString, string database)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { Database = database };
        return builder.ConnectionString;
    }

    private string RequiredConnectionString(string name) =>
        _config.GetConnectionString(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"ConnectionStrings:{name} is not configured.");
}
