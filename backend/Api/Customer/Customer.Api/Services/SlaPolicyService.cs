using Customer.Entity.TableEntities;
using Customer.Repository;
using Customer.Repository.SeedData;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Customer;

namespace Customer.Api.Services;

/// <summary>
/// A branch's SLA policies (D-18, TK-18): seeding them, and working out when a
/// ticket falls due.
/// </summary>
public sealed class SlaPolicyService
{
    private readonly CustomerDbContext _db;

    public SlaPolicyService(CustomerDbContext db) => _db = db;

    /// <summary>
    /// Adds whichever priorities the branch is missing, and changes none it has —
    /// so seeding twice adds nothing, and a branch's own hours survive a re-seed.
    /// Returns how many rows it added.
    /// </summary>
    public async Task<int> SeedAsync(Guid orgId, CancellationToken ct)
    {
        HashSet<TicketPriority> held = [.. await _db.SlaPolicies.Select(p => p.Priority).ToListAsync(ct)];

        List<SlaPolicy> missing = [.. SlaPolicySeed.Build(orgId).Where(p => !held.Contains(p.Priority))];

        if (missing.Count == 0)
        {
            return 0;
        }

        _db.SlaPolicies.AddRange(missing);
        await _db.SaveChangesAsync(ct);
        return missing.Count;
    }

    /// <summary>
    /// When a ticket of <paramref name="priority"/> raised at
    /// <paramref name="raisedAt"/> must be resolved, by the branch's own policy.
    ///
    /// A branch with no row for the priority — one created before this table and
    /// not yet re-seeded — falls back to the seeded default rather than leaving
    /// the ticket with no due date: an SLA nobody wrote down is still the one
    /// the product has always applied.
    /// </summary>
    public async Task<DateTimeOffset> DueAtAsync(
        TicketPriority priority, DateTimeOffset raisedAt, CancellationToken ct)
    {
        int? hours = await _db.SlaPolicies
            .Where(p => p.Priority == priority)
            .Select(p => (int?)p.ResolutionHours)
            .FirstOrDefaultAsync(ct);

        return raisedAt.AddHours(hours ?? DefaultResolutionHours(priority));
    }

    public static int DefaultResolutionHours(TicketPriority priority) =>
        SlaPolicySeed.Defaults
            .Where(d => d.Priority == priority)
            .Select(d => d.ResolutionHours)
            .DefaultIfEmpty(SlaPolicySeed.Defaults.Single(d => d.Priority == TicketPriority.Medium).ResolutionHours)
            .First();
}
