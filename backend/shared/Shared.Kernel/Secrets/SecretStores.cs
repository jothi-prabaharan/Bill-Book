using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.Kernel.Interfaces;

namespace Shared.Kernel.Secrets;

/// <summary>
/// Secrets resolved from <see cref="IConfiguration"/>, under <c>Secrets:</c> or environment variables.
/// </summary>
public sealed class ConfigurationSecretStore : ISecretStore
{
    private readonly IConfiguration _configuration;

    public ConfigurationSecretStore(IConfiguration configuration) =>
        _configuration = configuration;

    public Task<string> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        if (_configuration[$"Secrets:{name}"] is { Length: > 0 } value)
        {
            return Task.FromResult(value);
        }

        if (_configuration[name] is { Length: > 0 } envValue)
        {
            return Task.FromResult(envValue);
        }

        if (_configuration.GetConnectionString("TenantDatabase") is { Length: > 0 } tenant)
        {
            return Task.FromResult(tenant);
        }

        throw new KeyNotFoundException($"Secret '{name}' is not configured in Secrets:{name} or environment.");
    }

    public Task SetSecretAsync(
        string name, string value, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Configuration is read-only. Secrets are supplied via environment variables or configuration.");
}

/// <summary>
/// Registers the secret store.
/// </summary>
public static class SecretStoreRegistration
{
    public static IServiceCollection AddSecretStore(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddSingleton<ISecretStore, ConfigurationSecretStore>();
        return services;
    }
}
