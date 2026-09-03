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
        _adminDbConnectionString = configuration.GetConnectionString("AdminDatabase") 
                                   ?? throw new InvalidOperationException("ConnectionStrings:AdminDatabase is missing.");
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
        using var connection = new NpgsqlConnection(_adminDbConnectionString);
        connection.Open();
        
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"DatabaseName\" FROM mst.\"Customers\" WHERE \"CustomerId\" = @customerId";
        command.Parameters.AddWithValue("customerId", customerId);
        
        var result = command.ExecuteScalar() as string;
        if (string.IsNullOrEmpty(result))
        {
            throw new InvalidOperationException($"Customer {customerId} not found or DatabaseName is null.");
        }
        
        return result;
    }
}
