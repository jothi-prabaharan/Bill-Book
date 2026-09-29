using Master.Entity.Enums;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;

namespace Master.Repository.Repositories;

/// <summary>
/// Adds a tenant database to the shard registry when the pools are full, or
/// for an Elite customer (H0.5, TK-46).
/// </summary>
public interface ITenantShardProvisioner
{
    /// <summary>
    /// Makes a new shard ready and registers it with room for
    /// <paramref name="capacity"/> customers, or returns null when none can be
    /// made here. Returns the database's name.
    /// </summary>
    Task<string?> ProvisionAsync(PlanTier plan, int capacity, CancellationToken ct);
}
