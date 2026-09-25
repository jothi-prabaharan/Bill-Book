using MaintenanceContract.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;

namespace MaintenanceContract.Repository;

/// <summary>
/// The <c>amc</c> schema (S8, TK-68): contracts, their covered assets and
/// visits. The base class supplies the CustomerId/OrgId query filter, xmin
/// concurrency and ErrorLogs; <see cref="OnModelCreating"/> calls it last.
/// </summary>
public class MaintenanceContractDbContext : TenantDbContext
{
    public MaintenanceContractDbContext(DbContextOptions<MaintenanceContractDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<AmcContract> AmcContracts => Set<AmcContract>();
    public DbSet<AmcCoveredAsset> AmcCoveredAssets => Set<AmcCoveredAsset>();
    public DbSet<AmcVisit> AmcVisits => Set<AmcVisit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("amc");

        modelBuilder.Entity<AmcContract>(b =>
        {
            b.HasKey(e => e.AmcContractId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.VendorContactId, e.ContractNo }).IsUnique();
            b.HasIndex(e => new { e.OrgId, e.ContractStatus, e.EndDate });
            b.Property(e => e.ContractValue).HasColumnType("decimal(18,4)");
            b.Property(e => e.BillingFrequency).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.AmcCoverage).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.ContractStatus).HasConversion<string>().HasMaxLength(20);
            b.ToTable(t =>
            {
                t.HasCheckConstraint("chk_amc_dates", "\"EndDate\" > \"StartDate\"");
                t.HasCheckConstraint("chk_amc_value", "\"ContractValue\" >= 0");
                t.HasCheckConstraint("chk_amc_terminated", "\"ContractStatus\" <> 'Terminated' OR \"TerminationReason\" IS NOT NULL");
            });
        });

        modelBuilder.Entity<AmcCoveredAsset>(b =>
        {
            b.HasKey(e => e.AmcCoveredAssetId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.AmcContractId, e.FacilityAssetId }).IsUnique();
            b.HasIndex(e => new { e.OrgId, e.FacilityAssetId });
            b.HasOne<AmcContract>().WithMany().HasForeignKey(e => e.AmcContractId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AmcVisit>(b =>
        {
            b.HasKey(e => e.AmcVisitId);
            b.HasIndex(e => new { e.OrgId, e.AmcContractId, e.VisitDate });
            b.Property(e => e.VisitKind).HasConversion<string>().HasMaxLength(20);
            b.HasOne<AmcContract>().WithMany().HasForeignKey(e => e.AmcContractId).OnDelete(DeleteBehavior.Restrict);
        });

        base.OnModelCreating(modelBuilder);
    }
}
