using Gateway.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;

namespace Gateway.Repository;

/// <summary>
/// The <c>gwy</c> schema in the master database. One table: the proxied-request log.
///
/// The gateway owns this context outright. It never reaches into another service's
/// DbContext, and no other service reads this one.
/// </summary>
public class GatewayDbContext(DbContextOptions<GatewayDbContext> options) : DbContext(options)
{
    public DbSet<RequestLog> RequestLogs => Set<RequestLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("gwy");

        modelBuilder.Entity<RequestLog>(b =>
        {
            b.HasKey(e => e.RequestLogId);
            b.Property(e => e.RequestLogId).UseIdentityByDefaultColumn();
            b.Property(e => e.Version).HasColumnName("xmin").IsRowVersion();

            // Retention purges by age, and almost every query is "recent activity",
            // so this is the index that matters. Descending to match the read order.
            b.HasIndex(e => e.StartedAt).IsDescending();

            // Narrowing a time range to one tenant is the next most common query.
            b.HasIndex(e => new { e.OrgId, e.StartedAt }).IsDescending(false, true);

            // Partial index: error hunting only ever looks at non-2xx/3xx, which is
            // a tiny fraction of rows, so indexing the rest would be wasted space.
            b.HasIndex(e => e.StatusCode)
                .HasFilter("\"StatusCode\" >= 400")
                .HasDatabaseName("IX_RequestLogs_Failures");

            b.HasIndex(e => e.CorrelationId);
        });
    }
}
