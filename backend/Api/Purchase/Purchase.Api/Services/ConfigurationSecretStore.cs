using Shared.Kernel.Interfaces;

namespace Purchase.Api.Services;

/// <summary>
/// Development secret store reading from configuration under "Secrets".
/// Production must use Key Vault — this exists so the tenant resolver has
/// something to read locally, not as a real secret store.
/// </summary>
public sealed class ConfigurationSecretStore : ISecretStore
{
    private readonly IConfiguration _configuration;

    public ConfigurationSecretStore(IConfiguration configuration) => _configuration = configuration;

    public Task<string> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        string? value = _configuration[$"Secrets:{name}"];
        if (value is not null)
        {
            return Task.FromResult(value);
        }

        string? fallback = _configuration.GetConnectionString("TenantFallback");
        if (fallback is not null && name.StartsWith("tenant-db-"))
        {
            string dbName = name.Substring(10);
            var builder = new Npgsql.NpgsqlConnectionStringBuilder(fallback)
            {
                Database = dbName
            };
            return Task.FromResult(builder.ToString());
        }

        return fallback is null
            ? throw new KeyNotFoundException($"Secret '{name}' not found.")
            : Task.FromResult(fallback);
    }

    public Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Purchase does not write secrets.");
}
