using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Documents;

namespace Master.Repository.Repositories;

/// <summary>
/// The people who work at a branch (TK-146): users with an active role in it.
/// Restaurant orders name a waiter from this list, and Sales resolves waiter
/// names through it in a batch.
/// </summary>
public sealed class BranchStaffRepository
{
    private const int MaxIds = 500;

    private readonly AdminDbContext _db;

    public BranchStaffRepository(AdminDbContext db) => _db = db;

    public async Task<List<UserRef>> StaffAsync(Guid orgId, CancellationToken ct) =>
        await (
                from u in _db.Users
                where u.IsActive && _db.UserOrganizationRoles.Any(r => r.UserId == u.UserId && r.OrgId == orgId && r.IsActive)
                orderby u.DisplayName
                select new UserRef(u.UserId, u.DisplayName))
            .ToListAsync(ct);

    public async Task<List<UserRef>> NamesAsync(Guid orgId, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        List<Guid> wanted = [.. ids.Distinct().Take(MaxIds)];
        if (wanted.Count == 0)
        {
            return [];
        }

        // Inactive users still resolve: an order a former waiter took keeps their name.
        return await (
                from u in _db.Users
                where wanted.Contains(u.UserId) && _db.UserOrganizationRoles.Any(r => r.UserId == u.UserId && r.OrgId == orgId)
                select new UserRef(u.UserId, u.DisplayName))
            .ToListAsync(ct);
    }
}
