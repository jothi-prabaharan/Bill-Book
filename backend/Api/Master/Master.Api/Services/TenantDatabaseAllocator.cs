using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;

namespace Master.Api.Services;

/// <summary>
/// Picks the physical database a new customer's books will live in.
///
/// <b>This is the step the sharded-tenancy work left out.</b> It built the
/// registry — <c>mst.TenantDatabases</c>, with a plan type and a capacity — and
/// it built <see cref="Shared.Kernel.Tenancy.TenantDatabaseResolver"/>, which
/// reads <c>mst.Customers.DatabaseName</c> to choose the connection for a
/// request. Nothing wrote the value in between. So every signup died on the
/// not-null column, no customer row ever carried a database name, and the column
/// looked dead enough to delete — which would have broken tenant resolution at
/// run time and nowhere else, because the resolver reads it in raw SQL that no
/// compiler checks.
///
/// <b>Capacity is claimed with a guarded update, not a read followed by a
/// write.</b> Two signups landing together would otherwise both see the last
/// free slot on a shard and both take it, and the second customer's books would
/// go into a database that is over its plan's limit. The row count of the
/// <c>ExecuteUpdate</c> is the answer: one means this caller claimed the slot,
/// zero means somebody else did and the next shard is tried. Same shape as the
/// numbering allocator and the stock decrement.
/// </summary>
public interface ITenantDatabaseAllocator
{
    /// <summary>
    /// Claims one organization's worth of capacity and returns the database
    /// name, or null when every shard for the plan is full.
    ///
    /// Null is not an error to swallow: it means the platform is out of
    /// provisioned capacity and an operator has to add a shard. A caller that
    /// invented a database name would put a customer's books somewhere no
    /// migration has run.
    /// </summary>
    Task<string?> AllocateAsync(string planType, CancellationToken ct);
}

public sealed class TenantDatabaseAllocator : ITenantDatabaseAllocator
{
    private readonly AdminDbContext _db;
    private readonly ILogger<TenantDatabaseAllocator> _log;

    public TenantDatabaseAllocator(AdminDbContext db, ILogger<TenantDatabaseAllocator> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<string?> AllocateAsync(string planType, CancellationToken ct)
    {
        // Fullest-first among the shards that still have room, so customers pack
        // into a database rather than spreading one per shard — a half-empty
        // shard per customer is the cost this registry exists to avoid.
        List<string> candidates = await _db.TenantDatabases
            .Where(d => d.PlanType == planType && d.CurrentOrganizations < d.MaxOrganizations)
            .OrderByDescending(d => d.CurrentOrganizations)
            .Select(d => d.DatabaseName)
            .ToListAsync(ct);

        foreach (string name in candidates)
        {
            // The guard: only claims a slot on a shard that still has one when
            // the update runs, which is not necessarily when the read above ran.
            int claimed = await _db.TenantDatabases
                .Where(d => d.DatabaseName == name
                    && d.CurrentOrganizations < d.MaxOrganizations)
                .ExecuteUpdateAsync(
                    set => set.SetProperty(
                        d => d.CurrentOrganizations, d => d.CurrentOrganizations + 1),
                    ct);

            if (claimed == 1)
            {
                return name;
            }

            // Somebody else took the last slot between the read and here. Try
            // the next shard rather than failing the signup.
            _log.LogInformation(
                "Tenant database {Database} filled while allocating; trying the next.", name);
        }

        _log.LogError(
            "No tenant database with capacity for plan {PlanType}. A shard must be provisioned "
            + "before further customers on this plan can be created.",
            planType);

        return null;
    }
}

/// <summary>
/// Thrown when there is no provisioned shard left for the plan.
///
/// A distinct type so signup answers 503 rather than 500: the request was
/// correct, the platform is out of capacity, and retrying after an operator adds
/// a shard will work.
/// </summary>
public sealed class NoTenantCapacityException : Exception
{
    public NoTenantCapacityException(string planType)
        : base($"No database is provisioned with capacity for the {planType} plan.")
    {
    }
}
