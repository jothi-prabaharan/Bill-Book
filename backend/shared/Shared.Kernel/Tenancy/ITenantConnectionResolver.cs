using Microsoft.Extensions.Caching.Memory;
using Shared.Kernel.Interfaces;

namespace Shared.Kernel.Tenancy;

/// <summary>
/// Resolves a customer's physical database connection string, cached — the
/// tenant directory is read on every request, so it must not be a round-trip
/// each time.
/// </summary>
public interface ITenantConnectionResolver
{
    Task<string> ResolveAsync(Guid customerId, CancellationToken ct = default);
}

/// <summary>
/// Where the tenant directory lives. Implemented per service as a call to
/// Platform's internal API — no service reads the mst schema directly.
///
/// It returns the Key Vault <em>reference</em>, never the connection string, so
/// the credential never travels over HTTP: each service resolves it from the
/// secret store itself.
/// </summary>
public interface ITenantDirectory
{
    Task<TenantDatabase?> LookupAsync(Guid customerId, CancellationToken ct = default);
}

public sealed class TenantDatabase
{
    public required string DatabaseName { get; init; }

    /// <summary>Key Vault secret name holding the connection string.</summary>
    public required string ConnectionSecretRef { get; init; }

    /// <summary>False until provisioning finished — queries must not run yet.</summary>
    public required bool IsReady { get; init; }
}

public sealed class TenantConnectionResolver : ITenantConnectionResolver
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(10);

    private readonly ITenantDirectory _directory;
    private readonly ISecretStore _secrets;
    private readonly IMemoryCache _cache;

    public TenantConnectionResolver(
        ITenantDirectory directory, ISecretStore secrets, IMemoryCache cache)
    {
        _directory = directory;
        _secrets = secrets;
        _cache = cache;
    }

    public async Task<string> ResolveAsync(Guid customerId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(Key(customerId), out string? cached) && cached is not null)
        {
            return cached;
        }

        TenantDatabase database = await _directory.LookupAsync(customerId, ct)
            ?? throw new TenantNotFoundException(customerId);

        if (!database.IsReady)
        {
            throw new TenantNotReadyException(customerId);
        }

        string connectionString = await _secrets.GetSecretAsync(database.ConnectionSecretRef, ct);
        _cache.Set(Key(customerId), connectionString, CacheLifetime);
        return connectionString;
    }

    private static string Key(Guid customerId) => $"tenant-conn:{customerId}";
}

public sealed class TenantNotFoundException : Exception
{
    public TenantNotFoundException(Guid customerId)
        : base($"No tenant database is registered for customer {customerId}.")
    {
    }
}

public sealed class TenantNotReadyException : Exception
{
    public TenantNotReadyException(Guid customerId)
        : base($"The database for customer {customerId} is still being provisioned.")
    {
    }
}
