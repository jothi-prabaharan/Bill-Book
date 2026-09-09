using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Entities;
using Shared.Kernel.Errors;

namespace Shared.Kernel.Tenancy;

/// <summary>
/// Base DbContext for a per-customer schema. Four things happen here so no
/// individual service can forget them:
///
/// 1. Every <see cref="OrgScopedEntity"/> gets a global query filter on
///    CustomerId and OrgId together.
/// 2. CustomerId and OrgId are stamped on insert, so a caller cannot write
///    into another customer's or another org's rows.
/// 3. xmin is mapped as the concurrency token on every audited entity.
/// 4. Both ids are reasserted on every pooled connection by the RLS
///    interceptor (app.current_customer_id, app.current_org_id), so Postgres
///    row-level security enforces the same boundary independently of the
///    query filter above.
///
/// The connection itself is chosen per request by the tenant resolver.
/// </summary>
public abstract class TenantDbContext : DbContext
{
    protected TenantDbContext(DbContextOptions options, ITenantContext tenant)
        : base(options)
    {
        Tenant = tenant;
    }

    protected ITenantContext Tenant { get; }

    /// <summary>
    /// Read by the query filter. A null customer/org yields no rows rather than
    /// all rows.
    ///
    /// Public deliberately. The filter is built with
    /// <c>Expression.Property(Expression.Constant(this), nameof(CurrentOrgId))</c>,
    /// and that overload resolves public properties only. While this was private,
    /// model building threw "Instance property 'CurrentOrgId' is not defined for
    /// type ..." for every per-customer context, at run time as well as design time.
    /// The same applies to <see cref="CurrentCustomerId"/>.
    /// </summary>
    public Guid CurrentCustomerId => Tenant.CustomerId ?? Guid.Empty;

    public Guid CurrentOrgId => Tenant.OrgId ?? Guid.Empty;

    /// <summary>The service's own error log. See <see cref="ErrorLog"/> for why every tenant schema has one.</summary>
    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Mapped here rather than in each of the seven contexts, so a service
        // cannot ship without somewhere to record its failures. It lands in
        // whichever schema the derived context declared as its default, which
        // gives acc.ErrorLogs, sal.ErrorLogs and so on — one table per service,
        // owned and migrated by that service, nothing shared across a boundary.
        //
        // Declared before the loop below on purpose: it is an OrgScopedEntity,
        // so the loop gives it the same query filter, the same tenant index and
        // the same xmin token as every other table, and the RLS audit sees it
        // as a table needing a policy.
        modelBuilder.Entity<ErrorLog>(b =>
        {
            b.ToTable("ErrorLogs");
            b.HasKey(e => e.ErrorLogId);

            // The only column a caller ever sees. Unique because it is quoted
            // to a user as "the" reference for their failure.
            b.HasIndex(e => e.ErrorReference).IsUnique();

            b.Property(e => e.Source).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.Code).HasConversion<string>().HasMaxLength(40);
            b.Property(e => e.FollowUpStatus).HasConversion<string>().HasMaxLength(20);

            // The task list: everything still open in this branch, newest
            // first. It is the only query anybody runs against this table
            // often enough to index for.
            b.HasIndex(e => new { e.OrgId, e.FollowUpStatus, e.OccurredAt });
        });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            Type clr = entityType.ClrType;

            if (typeof(OrgScopedEntity).IsAssignableFrom(clr))
            {
                // CustomerId == CurrentCustomerId && OrgId == CurrentOrgId, built
                // as an expression so EF can translate it. OrgId alone is already
                // globally unique, so CustomerId here is a second, independent
                // check rather than the thing doing the isolating — see
                // OrgScopedEntity's doc comment.
                ParameterExpression parameter = Expression.Parameter(clr, "e");

                MemberExpression customerId = Expression.Property(
                    parameter, nameof(OrgScopedEntity.CustomerId));
                MemberExpression currentCustomer = Expression.Property(
                    Expression.Constant(this), nameof(CurrentCustomerId));

                MemberExpression orgId = Expression.Property(parameter, nameof(OrgScopedEntity.OrgId));
                MemberExpression current = Expression.Property(
                    Expression.Constant(this), nameof(CurrentOrgId));

                LambdaExpression filter = Expression.Lambda(
                    Expression.AndAlso(
                        Expression.Equal(customerId, currentCustomer),
                        Expression.Equal(orgId, current)),
                    parameter);

                modelBuilder.Entity(clr).HasQueryFilter(filter);
                modelBuilder.Entity(clr)
                    .HasIndex(nameof(OrgScopedEntity.CustomerId), nameof(OrgScopedEntity.OrgId));
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
        StampTenant();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampTenant();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Sets CustomerId and OrgId on new rows from the request's tenant context,
    /// and refuses an insert that names a different customer or org — the caller
    /// does not get to choose.
    /// </summary>
    private void StampTenant()
    {
        (Guid customerId, Guid orgId) = Tenant.Require();

        foreach (var entry in ChangeTracker.Entries<OrgScopedEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.CustomerId == Guid.Empty)
                    {
                        entry.Entity.CustomerId = customerId;
                    }
                    else if (entry.Entity.CustomerId != customerId)
                    {
                        throw new InvalidOperationException(
                            $"Refusing to insert a row for customer {entry.Entity.CustomerId} " +
                            $"while the request is scoped to {customerId}.");
                    }

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
                    // Neither id is ever reassigned — moving a row between
                    // customers or orgs would silently relocate its history.
                    entry.Property(nameof(OrgScopedEntity.CustomerId)).IsModified = false;
                    entry.Property(nameof(OrgScopedEntity.OrgId)).IsModified = false;
                    break;
            }
        }
    }
}
