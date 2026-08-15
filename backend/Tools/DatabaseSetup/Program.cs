using System;
using System.Data;
using System.Threading.Tasks;
using Npgsql;

namespace DatabaseSetup;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: dotnet run [create|verify] [host] [port] [user] [password]");
            return 1;
        }

        string command = args[0];
        string host = args.Length > 1 ? args[1] : "localhost";
        string port = args.Length > 2 ? args[2] : "5432";
        string user = args.Length > 3 ? args[3] : "postgres";
        string password = args.Length > 4 ? args[4] : "123";

        try
        {
            if (command == "create")
            {
                await RunCreateDatabases(host, port, user, password);
            }
            else if (command == "verify")
            {
                await RunVerifySeedData(host, port, user, password);
            }
            else
            {
                Console.WriteLine($"Unknown command: {command}");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }

        return 0;
    }

    private static async Task RunCreateDatabases(string host, string port, string user, string password)
    {
        string connectionString = $"Host={host};Port={port};Username={user};Password={password};Database=postgres";
        
        string[] databases = { "EP_Admin", "EP_Design", "EP_Test", "IN0000000001" };

        Console.WriteLine("\n=== Creating databases ===");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var db in databases)
        {
            bool exists;
            await using (var checkCmd = new NpgsqlCommand($"SELECT 1 FROM pg_database WHERE datname = '{db}'", connection))
            {
                var result = await checkCmd.ExecuteScalarAsync();
                exists = result != null;
            }

            if (!exists)
            {
                Console.WriteLine($"Creating database {db}...");
                // Note: CREATE DATABASE cannot run in a transaction, and EF's auto-create on Windows
                // often defaults to WIN1252 template1. We explicitly enforce UTF8 via template0.
                string sql = $"CREATE DATABASE \"{db}\" ENCODING 'UTF8' TEMPLATE template0";
                await using var createCmd = new NpgsqlCommand(sql, connection);
                await createCmd.ExecuteNonQueryAsync();
            }
        }

        Console.WriteLine("    Databases ready");
    }

    private static async Task RunVerifySeedData(string host, string port, string user, string password)
    {
        string connectionString = $"Host={host};Port={port};Username={user};Password={password};Database=EP_Admin";

        Console.WriteLine("\n=== Verifying seed data ===");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        Console.WriteLine("Master reference data (mst):");
        await PrintTableCount(connection, "mst", "Countries");
        await PrintTableCount(connection, "mst", "Currencies");
        await PrintTableCount(connection, "mst", "States");
        await PrintTableCount(connection, "mst", "AccountTypes");
        await PrintTableCount(connection, "mst", "TransactionTypes");
        await PrintTableCount(connection, "mst", "LedgerTypes");
        await PrintTableCount(connection, "mst", "LedgerSources");
        await PrintTableCount(connection, "mst", "HsnSacCodes");

        Console.WriteLine("\nIdentity and Platform seed data (now in mst):");
        await PrintTableCount(connection, "mst", "Roles");
        await PrintTableCount(connection, "mst", "Permissions");
        await PrintTableCount(connection, "mst", "RolePermissions");
        await PrintTableCount(connection, "mst", "Configurations");
    }

    private static async Task PrintTableCount(NpgsqlConnection connection, string schema, string table)
    {
        try
        {
            string sql = $"SELECT count(*) FROM {schema}.\"{table}\"";
            await using var cmd = new NpgsqlCommand(sql, connection);
            var result = await cmd.ExecuteScalarAsync();
            Console.WriteLine($"    {table,-20} {result} rows");
        }
        catch
        {
            Console.WriteLine($"    {table,-20} [Table not found or error]");
        }
    }
}
