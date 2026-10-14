using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Shared.Kernel.Tenancy;

public class TenantDatabaseResolver : ITenantDatabaseResolver
{
    private const int StrictTimeoutSeconds = 180; // 3 min strict for all APIs
    private readonly string _adminDbConnectionString;
    private readonly IMemoryCache _cache;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public TenantDatabaseResolver(
        IConfiguration configuration,
        IMemoryCache cache,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        string rawConnectionString = configuration.GetConnectionString("AdminDatabase") 
                                     ?? throw new InvalidOperationException("ConnectionStrings:AdminDatabase is missing.");
        
        var builder = new NpgsqlConnectionStringBuilder(rawConnectionString);
        if (builder.Timeout < StrictTimeoutSeconds)
        {
            builder.Timeout = StrictTimeoutSeconds;
        }
        if (builder.CommandTimeout < StrictTimeoutSeconds)
        {
            builder.CommandTimeout = StrictTimeoutSeconds;
        }
        if (builder.KeepAlive == 0 || builder.KeepAlive > 5)
        {
            builder.KeepAlive = 5;
        }
        builder.TcpKeepAlive = true;
        builder.TcpKeepAliveTime = 5;
        builder.TcpKeepAliveInterval = 1;
        builder.ConnectionIdleLifetime = 20;
        builder.ConnectionPruningInterval = 5;
        builder.ConnectionLifetime = 120;
        if (builder.MaxPoolSize > 15)
        {
            builder.MaxPoolSize = 15;
        }
        _adminDbConnectionString = builder.ConnectionString;
        _cache = cache;
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetAdminConnectionString() => _adminDbConnectionString;

    public string GetConnectionString(Guid? customerId) =>
        GetConnectionString(customerId, null);

    public string GetConnectionString(Guid? customerId, string? databaseName)
    {
        // 1. If database name is explicitly supplied (e.g. from ITenantContext), use it immediately
        if (!string.IsNullOrWhiteSpace(databaseName))
        {
            return BuildTenantConnectionString(databaseName);
        }

        // 2. Check if the database name is provided directly in the request's access token claims
        var user = _httpContextAccessor?.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            string? tokenDb = user.FindFirst("database_name")?.Value
                              ?? user.FindFirst("db_name")?.Value;

            if (!string.IsNullOrWhiteSpace(tokenDb))
            {
                // Verify customer matches if customerId is passed
                string? customerIdClaim = user.FindFirst("customer_id")?.Value;
                if (customerId is null || customerId == Guid.Empty ||
                    (Guid.TryParse(customerIdClaim, out Guid tokenCustomerId) && tokenCustomerId == customerId.Value))
                {
                    return BuildTenantConnectionString(tokenDb);
                }
            }
        }

        if (customerId is null || customerId == Guid.Empty)
        {
            // Default to TenantDatabase connection string if no customer is in context (e.g. at startup/migrations)
            return BuildTenantConnectionString("IN000001");
        }

        // 3. Fallback: resolve from database (mst.Customers) with caching
        string cacheKey = $"CustomerDb_{customerId}";
        
        string dbName = _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            return FetchDatabaseNameFromAdmin(customerId.Value);
        })!;

        if (string.IsNullOrEmpty(dbName))
        {
            throw new InvalidOperationException($"Could not determine database name for customer {customerId}");
        }

        return BuildTenantConnectionString(dbName);
    }

    private string BuildTenantConnectionString(string dbName)
    {
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
                command.CommandTimeout = StrictTimeoutSeconds; // 3 min strict
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
