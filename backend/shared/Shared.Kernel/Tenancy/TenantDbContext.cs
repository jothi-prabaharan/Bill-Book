using System.Data.Common;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Shared.Kernel.Audit;
using Shared.Kernel.Entities;
using Shared.Kernel.Errors;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Persistence;

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
    static TenantDbContext()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

    protected TenantDbContext(DbContextOptions options, ITenantContext tenant)
        : base(options)
    {
        Tenant = tenant;
        try
        {
            Database.SetCommandTimeout(180);
        }
        catch (InvalidOperationException)
        {
            // Non-relational provider in tests
        }
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

    /// <summary>The service's append-only audit trail (TK-118). See <see cref="EditLog"/>.</summary>
    public DbSet<EditLog> EditLogs => Set<EditLog>();

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

        // The audit trail (TK-118), mapped here for the same reason as the error
        // log: every tenant schema gets one, and no service can ship without it.
        // The append-only trigger and the RLS policy are added by each schema's
        // migration through EditLogMigration.
        modelBuilder.Entity<EditLog>(b =>
        {
            b.ToTable("EditLog");
            b.HasKey(e => e.EditLogId);
            b.Property(e => e.Operation).HasConversion<string>().HasMaxLength(10);
            b.Property(e => e.Changes).HasColumnType("jsonb");

            // A document's History tab, the audit-log report by date, by user
            // and by table: the four ways the log is read.
            b.HasIndex(e => new { e.OrgId, e.RootEntityName, e.RootEntityKey, e.ChangedAt });
            b.HasIndex(e => new { e.OrgId, e.ChangedAt });
            b.HasIndex(e => new { e.OrgId, e.UserId, e.ChangedAt });
            b.HasIndex(e => new { e.OrgId, e.EntityName, e.ChangedAt });
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

    /// <summary>
    /// Saves, and writes the edit log for what was saved (TK-118) in the same
    /// transaction: joined when the caller holds one, opened here when not, so
    /// a save that fails or is rolled back leaves no log row.
    /// </summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTenant();
        List<EditLogWriter.PendingEdit> pending = EditLogWriter.Capture(this);
        if (pending.Count == 0)
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        bool owns = Database.CurrentTransaction is null;
        using var transaction = owns ? Database.BeginTransaction() : null;

        int saved = base.SaveChanges(acceptAllChangesOnSuccess);
        WriteEditLog(pending);
        base.SaveChanges(acceptAllChangesOnSuccess: true);

        transaction?.Commit();
        return saved;
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampTenant();
        List<EditLogWriter.PendingEdit> pending = EditLogWriter.Capture(this);
        if (pending.Count == 0)
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        await using ITransactionScope scope = await Database.BeginScopeAsync(cancellationToken);

        int saved = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        WriteEditLog(pending);
        await base.SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

        try
        {
            await scope.CommitAsync(cancellationToken);
        }
        catch (DbException ex)
        {
            throw new DbUpdateException("An error occurred while saving the entity changes.", ex);
        }

        return saved;
    }

    /// <summary>The request's customer and branch, for log rows written beside a bulk statement.</summary>
    internal (Guid CustomerId, Guid OrgId) RequireTenant() => Tenant.Require();

    private void WriteEditLog(IReadOnlyList<EditLogWriter.PendingEdit> pending)
    {
        (Guid customerId, Guid orgId) = Tenant.Require();

        (Guid? userId, string? userName) = EditLogActor();
        foreach (EditLog row in EditLogWriter.Materialize(this, pending, userId, userName, DateTimeOffset.UtcNow))
        {
            row.CustomerId = customerId;
            row.OrgId = orgId;
            EditLogs.Add(row);
        }
    }

    /// <summary>
    /// Who is saving, from the request's <see cref="ICurrentUser"/> when the
    /// context was built by dependency injection. Null in a test that built the
    /// context by hand; the writer then falls back to the audit columns.
    /// </summary>
    protected internal (Guid? UserId, string? UserName) EditLogActor()
    {
        Guid? userId = null;
        string? userName = null;

        try
        {
            userId = this.GetService<ICurrentUser>()?.UserId;
        }
        catch (InvalidOperationException)
        {
            // Built by hand, with no application services behind it.
        }

        try
        {
            userName = this.GetService<IHttpContextAccessor>()?.HttpContext?.User.FindFirst("display_name")?.Value;
        }
        catch (InvalidOperationException)
        {
            // No request: a worker, a seed or a test.
        }

        return (userId, userName);
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
