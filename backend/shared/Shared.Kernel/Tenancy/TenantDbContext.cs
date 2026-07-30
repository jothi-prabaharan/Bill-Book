using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Entities;

namespace Shared.Kernel.Tenancy;

/// <summary>
/// Base DbContext for a per-customer schema. Three things happen here so no
/// individual service can forget them:
///
/// 1. Every <see cref="OrgScopedEntity"/> gets a global query filter on OrgId.
/// 2. OrgId is stamped on insert, so a caller cannot write into another org.
/// 3. xmin is mapped as the concurrency token on every audited entity.
///
/// The connection itself is chosen per request by the tenant resolver, and
/// app.current_org_id is set transaction-locally by the RLS interceptor.
/// </summary>
public abstract class TenantDbContext : DbContext
{
    protected TenantDbContext(DbContextOptions options, ITenantContext tenant)
        : base(options)
    {
        Tenant = tenant;
    }

    protected ITenantContext Tenant { get; }

    /// <summary>Read by the query filter. A null org yields no rows rather than all rows.</summary>
    private Guid CurrentOrgId => Tenant.OrgId ?? Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            Type clr = entityType.ClrType;

            if (typeof(OrgScopedEntity).IsAssignableFrom(clr))
            {
                // OrgId == CurrentOrgId, built as an expression so EF can translate it.
                ParameterExpression parameter = Expression.Parameter(clr, "e");
                MemberExpression orgId = Expression.Property(parameter, nameof(OrgScopedEntity.OrgId));
                MemberExpression current = Expression.Property(
                    Expression.Constant(this), nameof(CurrentOrgId));
                LambdaExpression filter = Expression.Lambda(
                    Expression.Equal(orgId, current), parameter);

                modelBuilder.Entity(clr).HasQueryFilter(filter);
                modelBuilder.Entity(clr).HasIndex(nameof(OrgScopedEntity.OrgId));
            }

            if (typeof(AuditableEntity).IsAssignableFrom(clr))
            {
                modelBuilder.Entity(clr)
                    .Property(nameof(AuditableEntity.Version))
                    .HasColumnName("xmin")
                    .HasColumnType("xid")
                    .ValueGeneratedOnAddOrUpdate()
                    .IsConcurrencyToken();
            }
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampOrgId();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampOrgId();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Sets OrgId on new rows from the request's tenant context, and refuses an
    /// insert that names a different org — the caller does not get to choose.
    /// </summary>
    private void StampOrgId()
    {
        Guid orgId = Tenant.Require().OrgId;

        foreach (var entry in ChangeTracker.Entries<OrgScopedEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.OrgId == Guid.Empty)
                    {
                        entry.Entity.OrgId = orgId;
                    }
                    else if (entry.Entity.OrgId != orgId)
                    {
                        throw new InvalidOperationException(
                            $"Refusing to insert a row for organization {entry.Entity.OrgId} " +
                            $"while the request is scoped to {orgId}.");
                    }

                    break;

                case EntityState.Modified:
                    // OrgId is never reassigned — moving a row between orgs would
                    // silently relocate its history.
                    entry.Property(nameof(OrgScopedEntity.OrgId)).IsModified = false;
                    break;
            }
        }
    }
}
