using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using WorkOrder.Entity.TableEntities;

namespace WorkOrder.Repository;

/// <summary>
/// The <c>wrk</c> schema (S6, TK-66): work orders, their tasks and parts. The
/// base class supplies the CustomerId/OrgId query filter, xmin concurrency and
/// ErrorLogs; <see cref="OnModelCreating"/> calls it last. <c>NumberingSeries</c>
/// is Accounting's, mapped without migrations, for the WRK number.
/// </summary>
public class WorkOrderDbContext : TenantDbContext
{
    public WorkOrderDbContext(DbContextOptions<WorkOrderDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<WorkOrderDocument> WorkOrders => Set<WorkOrderDocument>();
    public DbSet<WorkOrderTask> WorkOrderTasks => Set<WorkOrderTask>();
    public DbSet<WorkOrderPart> WorkOrderParts => Set<WorkOrderPart>();

    public DbSet<NumberingSeries> NumberingSeries => Set<NumberingSeries>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("wrk");

        modelBuilder.Entity<WorkOrderDocument>(b =>
        {
            b.ToTable("WorkOrders", t =>
            {
                t.HasCheckConstraint("chk_work_order_where", "\"FacilityAssetId\" IS NOT NULL OR \"SpaceId\" IS NOT NULL");
                t.HasCheckConstraint("chk_work_order_completed", "\"WorkOrderStatus\" NOT IN ('Completed', 'Closed') OR \"CompletedDate\" IS NOT NULL");
                t.HasCheckConstraint("chk_work_order_labour", "\"LabourCost\" >= 0");
            });
            b.HasKey(e => e.WorkOrderId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.WorkOrderNo }).IsUnique();

            // One work order per plan occurrence or AMC visit: raising again finds it.
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.SourceKey }).IsUnique().HasFilter("\"SourceKey\" IS NOT NULL");
            b.HasIndex(e => new { e.OrgId, e.WorkOrderStatus });
            b.HasIndex(e => new { e.OrgId, e.FacilityAssetId });

            b.Property(e => e.WorkOrderSource).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.Priority).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.WorkOrderStatus).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.LabourCost).HasColumnType("decimal(18,4)");

            b.HasMany(e => e.Tasks).WithOne().HasForeignKey(t => t.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(e => e.Parts).WithOne().HasForeignKey(p => p.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkOrderTask>(b => b.HasKey(e => e.WorkOrderTaskId));

        modelBuilder.Entity<WorkOrderPart>(b =>
        {
            b.HasKey(e => e.WorkOrderPartId);
            b.Property(e => e.Quantity).HasColumnType("decimal(18,4)");
            b.Property(e => e.UnitCost).HasColumnType("decimal(18,4)");
            b.ToTable(t => t.HasCheckConstraint("chk_work_order_part", "\"Quantity\" > 0 AND \"UnitCost\" >= 0"));
        });

        modelBuilder.ConfigureNumberingSeries(ownsMigration: false);

        base.OnModelCreating(modelBuilder);
    }
}
