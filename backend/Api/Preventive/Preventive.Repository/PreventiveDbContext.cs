using Microsoft.EntityFrameworkCore;
using Preventive.Entity.TableEntities;
using Shared.Kernel.Tenancy;

namespace Preventive.Repository;

/// <summary>
/// The <c>ppm</c> schema (S7, TK-67): preventive plans and their occurrences.
/// The base class supplies the CustomerId/OrgId query filter, xmin concurrency
/// and ErrorLogs; <see cref="OnModelCreating"/> calls it last.
/// </summary>
public class PreventiveDbContext : TenantDbContext
{
    public PreventiveDbContext(DbContextOptions<PreventiveDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<PreventivePlan> PreventivePlans => Set<PreventivePlan>();
    public DbSet<PreventiveOccurrence> PreventiveOccurrences => Set<PreventiveOccurrence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("ppm");

        modelBuilder.Entity<PreventivePlan>(b =>
        {
            b.HasKey(e => e.PreventivePlanId);
            b.HasIndex(e => new { e.OrgId, e.IsActive, e.NextDueDate });
            b.Property(e => e.Frequency).HasConversion<string>().HasMaxLength(20);
            b.ToTable(t =>
            {
                t.HasCheckConstraint("chk_plan_where", "\"FacilityAssetId\" IS NOT NULL OR \"SpaceId\" IS NOT NULL");
                t.HasCheckConstraint("chk_plan_dates", "\"EndDate\" IS NULL OR \"EndDate\" >= \"StartDate\"");
                t.HasCheckConstraint("chk_plan_interval", "\"Interval\" >= 1 AND \"LeadDays\" >= 0");
            });
        });

        modelBuilder.Entity<PreventiveOccurrence>(b =>
        {
            b.HasKey(e => e.PreventiveOccurrenceId);

            // The idempotency key: one occurrence per plan per due date.
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.PreventivePlanId, e.DueDate }).IsUnique();
            b.HasIndex(e => new { e.OrgId, e.OccurrenceStatus, e.DueDate });
            b.Property(e => e.OccurrenceStatus).HasConversion<string>().HasMaxLength(20);
            b.HasOne<PreventivePlan>().WithMany().HasForeignKey(e => e.PreventivePlanId).OnDelete(DeleteBehavior.Restrict);
        });

        base.OnModelCreating(modelBuilder);
    }
}
