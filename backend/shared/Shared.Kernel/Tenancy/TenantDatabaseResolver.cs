using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Shared.Kernel.Tenancy;

public class TenantDatabaseResolver : ITenantDatabaseResolver
{
    private readonly string _adminDbConnectionString;
    private readonly IMemoryCache _cache;

    public TenantDatabaseResolver(IConfiguration configuration, IMemoryCache cache)
    {
        string rawConnectionString = configuration.GetConnectionString("AdminDatabase") 
                                     ?? throw new InvalidOperationException("ConnectionStrings:AdminDatabase is missing.");
        
        var builder = new NpgsqlConnectionStringBuilder(rawConnectionString);
        if (builder.Timeout < 60)
        {
            builder.Timeout = 60;
        }
        if (builder.CommandTimeout < 120)
        {
            builder.CommandTimeout = 120;
        }
        if (builder.KeepAlive == 0)
        {
            builder.KeepAlive = 30;
        }
        _adminDbConnectionString = builder.ConnectionString;
        _cache = cache;
    }

    public string GetConnectionString(Guid? customerId)
    {
        if (customerId is null || customerId == Guid.Empty)
        {
            // Default to TenantDatabase connection string if no customer is in context (e.g. at startup/migrations)
            return new NpgsqlConnectionStringBuilder(_adminDbConnectionString) { Database = "IN000001" }.ConnectionString;
        }

        string cacheKey = $"CustomerDb_{customerId}";
        
        string dbName = _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return FetchDatabaseNameFromAdmin(customerId.Value);
        })!;

        if (string.IsNullOrEmpty(dbName))
        {
            throw new InvalidOperationException($"Could not determine database name for customer {customerId}");
        }

        var builder = new NpgsqlConnectionStringBuilder(_adminDbConnectionString) { Database = dbName };
        return builder.ConnectionString;
    }

    private string FetchDatabaseNameFromAdmin(Guid customerId)
    {
        const int maxAttempts = 5;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var connection = new NpgsqlConnection(_adminDbConnectionString);
                connection.Open();
                
                using var command = connection.CreateCommand();
                command.CommandTimeout = 120;
                command.CommandText = "SELECT \"DatabaseName\" FROM mst.\"Customers\" WHERE \"CustomerId\" = @customerId";
                command.Parameters.AddWithValue("customerId", customerId);
                
                var result = command.ExecuteScalar() as string;
                if (string.IsNullOrEmpty(result))
                {
                    throw new InvalidOperationException($"Customer {customerId} not found or DatabaseName is null.");
                }
                
                return result;
            }
            catch (InvalidOperationException)
            {
                // Logical failure: customer not found in mst.Customers
                throw;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransient(ex))
            {
                Thread.Sleep(TimeSpan.FromSeconds(Math.Min(attempt * 2, 10)));
            }
        }

        throw new InvalidOperationException($"Failed to resolve database name for customer {customerId} after {maxAttempts} attempts.");
    }

    private static bool IsTransient(Exception ex) =>
        ex is NpgsqlException
        || ex is TimeoutException
        || ex.InnerException is TimeoutException
        || ex is System.IO.IOException
        || ex.InnerException is System.IO.IOException
        || ex is System.Net.Sockets.SocketException
        || ex.InnerException is System.Net.Sockets.SocketException;
}
