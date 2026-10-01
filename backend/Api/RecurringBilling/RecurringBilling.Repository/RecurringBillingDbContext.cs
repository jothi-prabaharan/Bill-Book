using Microsoft.EntityFrameworkCore;
using RecurringBilling.Entity.TableEntities;
using Shared.Kernel.Tenancy;

namespace RecurringBilling.Repository;

public class RecurringBillingDbContext : TenantDbContext
{
    public RecurringBillingDbContext(DbContextOptions<RecurringBillingDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<RecurringSchedule> RecurringSchedules => Set<RecurringSchedule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.HasDefaultSchema("rec");

        modelBuilder.Entity<RecurringSchedule>(entity =>
        {
            entity.HasKey(e => e.RecurringScheduleId);
        });
    }
}
