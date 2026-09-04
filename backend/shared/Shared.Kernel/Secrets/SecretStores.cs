using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.Kernel.Interfaces;

namespace Shared.Kernel.Secrets;

/// <summary>
/// Key Vault, for deployments. <see cref="ConfigurationSecretStore"/> is the
/// development counterpart; both exist so nothing has to choose between running
/// locally and running for real — the same shape
/// <see cref="Storage.AzureBlobFileStorage"/> and
/// <c>LocalDiskFileStorage</c> already use for files.
///
/// <b>This replaces six copies of a class documented as "development only".</b>
/// Every per-customer service carried its own <c>ConfigurationSecretStore</c>
/// under a comment saying Key Vault must be used before production, and none of
/// them could be, because there was no Key Vault-backed implementation to
/// switch to. The packages were pinned; only this was missing.
///
/// <b>Rotation.</b> A secret is fetched on first use and cached for
/// <see cref="CacheFor"/>, so rotating one in the vault takes effect within that
/// window without a restart. Caching at all is deliberate: a tenant connection
/// string is read on the way into a request, and a network round trip per
/// request would put Key Vault's availability in front of every query.
///
/// <b>Nothing here logs a value.</b> The name of a secret is safe to say and its
/// contents never are, so a failure names the secret and reports the vault's own
/// status code, never what came back.
/// </summary>
public sealed class KeyVaultSecretStore : ISecretStore
{
    /// <summary>
    /// Long enough that a busy service is not calling out constantly, short
    /// enough that a rotated credential takes hold the same hour.
    /// </summary>
    public static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);

    private readonly SecretClient _client;
    private readonly TimeProvider _clock;

    private readonly Dictionary<string, (string Value, DateTimeOffset FetchedAt)> _cache =
        new(StringComparer.Ordinal);

    private readonly SemaphoreSlim _gate = new(1, 1);

    public KeyVaultSecretStore(SecretClient client, TimeProvider clock)
    {
        _client = client;
        _clock = clock;
    }

    public async Task<string> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (_cache.TryGetValue(name, out (string Value, DateTimeOffset FetchedAt) hit)
                && _clock.GetUtcNow() - hit.FetchedAt < CacheFor)
            {
                return hit.Value;
            }

            try
            {
                Response<KeyVaultSecret> response =
                    await _client.GetSecretAsync(name, cancellationToken: cancellationToken);

                string value = response.Value.Value;
                _cache[name] = (value, _clock.GetUtcNow());

                return value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // The name, never a value, and never the vault's URI — which is
                // not secret but is a detail worth not scattering through logs.
                throw new KeyNotFoundException($"Secret '{name}' is not in the vault.");
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetSecretAsync(
        string name, string value, CancellationToken cancellationToken = default)
    {
        await _client.SetSecretAsync(name, value, cancellationToken);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            // Written through rather than invalidated, so the caller that just
            // set it does not read the old one back for up to ten minutes.
            _cache[name] = (value, _clock.GetUtcNow());
        }
        finally
        {
            _gate.Release();
        }
    }
}

/// <summary>
/// Secrets from <see cref="IConfiguration"/>, under <c>Secrets:</c>.
///
/// <b>Development, and deliberately not silently usable in production.</b>
/// <see cref="SecretStoreRegistration"/> refuses to register it when the
/// environment is Production and no vault is configured, so a deployment that
/// forgot its vault fails at startup with a message rather than serving
/// requests off whatever happened to be in <c>appsettings.json</c>.
///
/// Configuration is not a poor stand-in for a vault so much as a different
/// trust model: environment variables and mounted files both arrive through it,
/// which is how secrets reach a container. What it cannot do is rotate on its
/// own or record who read what, which is what the vault is for.
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

        // The one fallback worth keeping: the tenant database. Every service
        // opens it at startup, and a developer with a local Postgres should not
        // have to write the same connection string twice.
        if (_configuration.GetConnectionString("TenantDatabase") is { Length: > 0 } tenant)
        {
            return Task.FromResult(tenant);
        }

        throw new KeyNotFoundException(
            $"Secret '{name}' is not configured. Set Secrets:{name}, or configure "
            + "KeyVault:Uri to read it from the vault.");
    }

    public Task SetSecretAsync(
        string name, string value, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            "Configuration is read-only. Writing a secret needs the vault — set KeyVault:Uri.");
}

/// <summary>
/// Chooses the secret store, and refuses to guess.
/// </summary>
public static class SecretStoreRegistration
{
    /// <summary>
    /// Key Vault when <c>KeyVault:Uri</c> is set, configuration otherwise — and
    /// a startup failure if the environment is Production and neither is true.
    ///
    /// <b>Failing at startup is the point.</b> A service that started anyway
    /// would serve requests reading secrets out of whatever configuration it
    /// happened to have, and the first sign of it would be a credential in a
    /// place nobody meant to put one. The exception names the setting to fix
    /// and nothing else.
    ///
    /// Authentication is <c>DefaultAzureCredential</c>: a managed identity in a
    /// deployment, the developer's own signed-in credential locally. No secret
    /// is needed to read the secrets, which is the property that makes this
    /// worth doing at all — a vault reached with a connection string in
    /// configuration would have moved the problem rather than solved it.
    /// </summary>
    public static IServiceCollection AddSecretStore(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        string? uri = configuration["KeyVault:Uri"];

        if (!string.IsNullOrWhiteSpace(uri))
        {
            services.AddSingleton<ISecretStore>(provider => new KeyVaultSecretStore(
                new SecretClient(new Uri(uri), new DefaultAzureCredential()),
                provider.GetRequiredService<TimeProvider>()));

            return services;
        }

        if (environment.IsProduction())
        {
            throw new InvalidOperationException(
                "KeyVault:Uri is not configured. A production deployment must read its "
                + "secrets from a vault; starting without one would serve requests off "
                + "whatever is in configuration.");
        }

        services.AddSingleton<ISecretStore, ConfigurationSecretStore>();

        return services;
    }
}
