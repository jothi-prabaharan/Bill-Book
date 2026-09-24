using Master.Entity.Enums;
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
    /// Claims one customer's place in a database and returns its name, or null
    /// when there is no room and no new shard could be provisioned.
    ///
    /// Null is not an error to swallow: it means the platform is out of
    /// capacity and an operator has to add a shard. A caller that invented a
    /// database name would put a customer's books somewhere no migration has run.
    /// </summary>
    Task<string?> AllocateAsync(PlanTier plan, CancellationToken ct);
}

/// <summary>
/// The allocation rules (H0.5, TK-46):
/// <list type="bullet">
/// <item><b>Capacity is counted in customers</b>, 100 to a pooled shard by
/// default (<c>Sharding:CustomersPerPool</c>).</item>
/// <item><b>Every plan but Elite shares the pools</b>, fullest first, so
/// customers pack into a database rather than spreading one per shard.</item>
/// <item><b>When the last pool fills, a new one is provisioned</b>, migrated,
/// registered, and then claimed, so a full pool no longer makes signup
/// answer 503.</item>
/// <item><b>Elite gets a shard of its own</b>, capacity one, provisioned for it.</item>
/// </list>
/// </summary>
public sealed class TenantDatabaseAllocator : ITenantDatabaseAllocator
{
    public const int DefaultCustomersPerPool = 100;

    private readonly AdminDbContext _db;
    private readonly ILogger<TenantDatabaseAllocator> _log;
    private readonly ITenantShardProvisioner? _provisioner;
    private readonly int _customersPerPool;

    public TenantDatabaseAllocator(
        AdminDbContext db,
        ILogger<TenantDatabaseAllocator> log,
        ITenantShardProvisioner? provisioner = null,
        IConfiguration? config = null)
    {
        _db = db;
        _log = log;
        _provisioner = provisioner;
        _customersPerPool = config?.GetValue<int?>("Sharding:CustomersPerPool") is int configured && configured > 0
            ? configured
            : DefaultCustomersPerPool;
    }

    public async Task<string?> AllocateAsync(PlanTier plan, CancellationToken ct)
    {
        if (plan == PlanTier.Elite)
        {
            // A database of its own: a new shard, holding this customer only.
            string? own = _provisioner is null ? null : await _provisioner.ProvisionAsync(PlanTier.Elite, 1, ct);
            return own is not null && await ClaimAsync(own, ct) ? own : NoCapacity(plan);
        }

        if (await ClaimInPoolsAsync(ct) is string pooled)
        {
            return pooled;
        }

        if (_provisioner is null)
        {
            return NoCapacity(plan);
        }

        // Every pool is full. Provision one and claim in it; a racing signup
        // may have provisioned one too, so the claim goes back through the
        // pools rather than straight at the new name.
        if (await _provisioner.ProvisionAsync(PlanTier.Pro, _customersPerPool, ct) is null)
        {
            return NoCapacity(plan);
        }

        return await ClaimInPoolsAsync(ct) ?? NoCapacity(plan);
    }

    /// <summary>A place in the fullest pooled shard with room, or null.</summary>
    private async Task<string?> ClaimInPoolsAsync(CancellationToken ct)
    {
        List<string> candidates = await _db.TenantDatabases
            .Where(d => d.PlanType != PlanTier.Elite && d.CurrentCustomers < d.MaxCustomers)
            .OrderByDescending(d => d.CurrentCustomers)
            .Select(d => d.DatabaseName)
            .ToListAsync(ct);

        foreach (string name in candidates)
        {
            if (await ClaimAsync(name, ct))
            {
                return name;
            }

            // Somebody else took the last place between the read and here.
            // Try the next shard rather than failing the signup.
            _log.LogInformation(
                "Tenant database {Database} filled while allocating; trying the next.", name);
        }

        return null;
    }

    /// <summary>
    /// The guard: claims a place only in a shard that still has one when the
    /// update runs, which is not necessarily when anything was read. The row
    /// count is the answer, so two signups cannot both take the last place.
    /// </summary>
    private async Task<bool> ClaimAsync(string name, CancellationToken ct) =>
        await _db.TenantDatabases
            .Where(d => d.DatabaseName == name && d.CurrentCustomers < d.MaxCustomers)
            .ExecuteUpdateAsync(
                set => set.SetProperty(d => d.CurrentCustomers, d => d.CurrentCustomers + 1),
                ct) == 1;

    private string? NoCapacity(PlanTier plan)
    {
        _log.LogError(
            "No tenant database with room for a {Plan} customer, and none could be provisioned. "
            + "Add a standby database (Sharding:StandbyDatabases) created by infrastructure.",
            plan);
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
    public NoTenantCapacityException(PlanTier planType)
        : base($"No database has room for a {planType} customer, and none could be provisioned.")
    {
    }
}
