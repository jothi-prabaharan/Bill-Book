using Microsoft.EntityFrameworkCore;
using Site.Entity.TableEntities;

namespace Site.Repository.Repositories;

public sealed class SiteRepository
{
    private readonly SiteDbContext _db;

    public SiteRepository(SiteDbContext db) => _db = db;

    public async Task<IReadOnlyList<SiteJob>> ListAsync(CancellationToken ct) =>
        await _db.Sites.AsNoTracking().OrderBy(s => s.SiteName).ToListAsync(ct);

    public async Task<SiteJob?> GetAsync(long siteId, CancellationToken ct) =>
        await _db.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.SiteId == siteId, ct);
}
