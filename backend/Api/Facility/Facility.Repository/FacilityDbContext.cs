using Facility.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;

namespace Facility.Repository;

/// <summary>
/// The <c>fac</c> schema (S5, TK-65): buildings, spaces and facility assets.
/// The base class supplies the CustomerId/OrgId query filter, xmin concurrency
/// and ErrorLogs; <see cref="OnModelCreating"/> calls it last.
/// </summary>
public class FacilityDbContext : TenantDbContext
{
    public FacilityDbContext(DbContextOptions<FacilityDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Space> Spaces => Set<Space>();
    public DbSet<FacilityAsset> FacilityAssets => Set<FacilityAsset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("fac");

        modelBuilder.Entity<Building>(b =>
        {
            b.HasKey(e => e.BuildingId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.Code }).IsUnique();
        });

        modelBuilder.Entity<Space>(b =>
        {
            b.HasKey(e => e.SpaceId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.Code }).IsUnique();
            b.Property(e => e.SpaceKind).HasConversion<string>().HasMaxLength(20);
            b.HasOne<Building>().WithMany().HasForeignKey(e => e.BuildingId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FacilityAsset>(b =>
        {
            b.HasKey(e => e.FacilityAssetId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.AssetTag }).IsUnique();
            b.HasIndex(e => new { e.OrgId, e.AssetStatus });
            b.Property(e => e.AssetCategory).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.AssetStatus).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.PurchaseCost).HasColumnType("decimal(18,4)");
            b.HasOne<Space>().WithMany().HasForeignKey(e => e.SpaceId).OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t => t.HasCheckConstraint(
                "chk_facility_asset_dates", "\"WarrantyUntil\" IS NULL OR \"PurchaseDate\" IS NULL OR \"WarrantyUntil\" >= \"PurchaseDate\""));
        });

        base.OnModelCreating(modelBuilder);
    }
}
