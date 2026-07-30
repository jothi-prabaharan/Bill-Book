using Shared.Kernel.Interfaces;

namespace Accounting.Api.Services;

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
        string? value = _configuration[$"Secrets:{name}"]
            ?? _configuration.GetConnectionString("TenantFallback");
        return value is null
            ? throw new KeyNotFoundException($"Secret '{name}' not found.")
            : Task.FromResult(value);
    }

    public Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Accounting does not write secrets.");
}
