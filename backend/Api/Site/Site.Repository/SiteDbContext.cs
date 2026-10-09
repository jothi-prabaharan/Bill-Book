using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Site.Entity.TableEntities;

namespace Site.Repository;

/// <summary>
/// The <c>sit</c> schema: Site services for service vertical branches (TK-168, TK-169).
/// Trades, laborers, site jobs, assignments, geofenced clock entries, dispatch/returns,
/// material usage, and billing invoices.
/// </summary>
public class SiteDbContext : TenantDbContext
{
    public SiteDbContext(DbContextOptions<SiteDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<Trade> Trades => Set<Trade>();
    public DbSet<Laborer> Laborers => Set<Laborer>();
    public DbSet<SiteJob> Sites => Set<SiteJob>();
    public DbSet<SiteAssignment> SiteAssignments => Set<SiteAssignment>();
    public DbSet<MarginRule> MarginRules => Set<MarginRule>();
    public DbSet<SiteAttendance> SiteAttendances => Set<SiteAttendance>();
    public DbSet<SiteAttendancePhoto> SiteAttendancePhotos => Set<SiteAttendancePhoto>();
    public DbSet<SiteStockDocument> SiteStockDocuments => Set<SiteStockDocument>();
    public DbSet<SiteStockLine> SiteStockLines => Set<SiteStockLine>();
    public DbSet<SiteStockBalance> SiteStockBalances => Set<SiteStockBalance>();
    public DbSet<SiteDispatch> SiteDispatches => Set<SiteDispatch>();
    public DbSet<SiteDispatchLine> SiteDispatchLines => Set<SiteDispatchLine>();
    public DbSet<SiteReturn> SiteReturns => Set<SiteReturn>();
    public DbSet<SiteReturnLine> SiteReturnLine => Set<SiteReturnLine>();
    public DbSet<SiteMaterialUsage> SiteMaterialUsages => Set<SiteMaterialUsage>();
    public DbSet<SiteMaterialUsageLine> SiteMaterialUsageLines => Set<SiteMaterialUsageLine>();
    public DbSet<SiteBillingInvoice> SiteBillingInvoices => Set<SiteBillingInvoice>();
    public DbSet<SiteBillingInvoiceLine> SiteBillingInvoiceLines => Set<SiteBillingInvoiceLine>();

    public DbSet<NumberingSeries> NumberingSeries => Set<NumberingSeries>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("sit");

        modelBuilder.Entity<Trade>(b =>
        {
            b.HasKey(e => e.TradeId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.TradeCode }).IsUnique();
            b.Property(e => e.DefaultCostRatePerDay).HasColumnType("decimal(18,4)");
            b.Property(e => e.DefaultSellRatePerDay).HasColumnType("decimal(18,4)");
            b.Property(e => e.DefaultOtCostRatePerHour).HasColumnType("decimal(18,4)");
            b.Property(e => e.DefaultOtSellRatePerHour).HasColumnType("decimal(18,4)");
        });

        modelBuilder.Entity<Laborer>(b =>
        {
            b.HasKey(e => e.LaborerId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.Phone });
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.UserId }).HasFilter("\"UserId\" IS NOT NULL");
            b.Property(e => e.CostRatePerDay).HasColumnType("decimal(18,4)");
            b.Property(e => e.SellRatePerDay).HasColumnType("decimal(18,4)");
            b.Property(e => e.OtCostRatePerHour).HasColumnType("decimal(18,4)");
            b.Property(e => e.OtSellRatePerHour).HasColumnType("decimal(18,4)");
            b.HasOne<Trade>()
                .WithMany()
                .HasForeignKey(e => e.TradeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SiteJob>(b =>
        {
            b.HasKey(e => e.SiteId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.SiteCode }).IsUnique();
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.Status });
            b.Property(e => e.Latitude).HasColumnType("decimal(10,7)");
            b.Property(e => e.Longitude).HasColumnType("decimal(10,7)");
            b.Property(e => e.Status).HasConversion<int>();
        });

        modelBuilder.Entity<SiteAssignment>(b =>
        {
            b.HasKey(e => e.SiteAssignmentId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.SiteId, e.LaborerId });
            b.Property(e => e.CostRatePerDay).HasColumnType("decimal(18,4)");
            b.Property(e => e.SellRatePerDay).HasColumnType("decimal(18,4)");
            b.Property(e => e.ApprovedDays).HasColumnType("decimal(8,2)");
            b.Property(e => e.ApprovedOtHours).HasColumnType("decimal(8,2)");
            b.Property(e => e.BilledDays).HasColumnType("decimal(8,2)");
            b.Property(e => e.BilledOtHours).HasColumnType("decimal(8,2)");
            b.Property(e => e.Status).HasConversion<int>();
            b.HasOne<SiteJob>()
                .WithMany()
                .HasForeignKey(e => e.SiteId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Laborer>()
                .WithMany()
                .HasForeignKey(e => e.LaborerId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Trade>()
                .WithMany()
                .HasForeignKey(e => e.TradeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MarginRule>(b =>
        {
            b.HasKey(e => e.MarginRuleId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.Scope, e.ScopeId });
            b.Property(e => e.Scope).HasConversion<int>();
            b.Property(e => e.AppliesTo).HasConversion<int>();
            b.Property(e => e.MarginType).HasConversion<int>();
            b.Property(e => e.MarginValue).HasColumnType("decimal(18,4)");
        });

        modelBuilder.Entity<SiteAttendance>(b =>
        {
            b.HasKey(e => e.AttendanceId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.AssignmentId, e.WorkDate }).IsUnique();
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.LaborerId, e.WorkDate });
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.SiteId, e.WorkDate });
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.Status });
            b.Property(e => e.CheckInLat).HasColumnType("decimal(10,7)");
            b.Property(e => e.CheckInLng).HasColumnType("decimal(10,7)");
            b.Property(e => e.DayFraction).HasColumnType("decimal(3,2)");
            b.Property(e => e.OtHoursClaimed).HasColumnType("decimal(4,2)");
            b.Property(e => e.OtHoursApproved).HasColumnType("decimal(4,2)");
            b.Property(e => e.Status).HasConversion<int>();
            b.Property(e => e.DayCostSnapshot).HasColumnType("decimal(18,2)");
            b.Property(e => e.DayMarginSnapshot).HasColumnType("decimal(18,2)");
            b.Property(e => e.DaySellSnapshot).HasColumnType("decimal(18,2)");
            b.Property(e => e.OtCostSnapshot).HasColumnType("decimal(18,2)");
            b.Property(e => e.OtMarginSnapshot).HasColumnType("decimal(18,2)");
            b.Property(e => e.OtSellSnapshot).HasColumnType("decimal(18,2)");
            b.HasOne<SiteAssignment>()
                .WithMany()
                .HasForeignKey(e => e.AssignmentId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SiteJob>()
                .WithMany()
                .HasForeignKey(e => e.SiteId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Laborer>()
                .WithMany()
                .HasForeignKey(e => e.LaborerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SiteAttendancePhoto>(b =>
        {
            b.HasKey(e => e.PhotoId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.AttendanceId });
            b.HasOne<SiteAttendance>()
                .WithMany()
                .HasForeignKey(e => e.AttendanceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SiteStockDocument>(b =>
        {
            b.HasKey(e => e.DocumentId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.DocumentNo }).IsUnique();
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.SiteId, e.DocumentType });
            b.Property(e => e.DocumentType).HasConversion<int>();
            b.Property(e => e.Status).HasConversion<int>();
            b.HasOne<SiteJob>()
                .WithMany()
                .HasForeignKey(e => e.SiteId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SiteStockLine>(b =>
        {
            b.HasKey(e => e.LineId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.DocumentId });
            b.Property(e => e.Quantity).HasColumnType("decimal(18,4)");
            b.Property(e => e.UnitCost).HasColumnType("decimal(18,2)");
            b.Property(e => e.MarginValue).HasColumnType("decimal(18,4)");
            b.Property(e => e.SellingPrice).HasColumnType("decimal(18,2)");
            b.Property(e => e.BilledQuantity).HasColumnType("decimal(18,4)");
            b.HasOne<SiteStockDocument>()
                .WithMany()
                .HasForeignKey(e => e.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SiteStockBalance>(b =>
        {
            b.HasKey(e => e.BalanceId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.SiteId, e.ItemId }).IsUnique();
            b.Property(e => e.DispatchedQty).HasColumnType("decimal(18,4)");
            b.Property(e => e.ReturnedQty).HasColumnType("decimal(18,4)");
            b.Property(e => e.UsedQty).HasColumnType("decimal(18,4)");
            b.Property(e => e.OnSiteQty).HasColumnType("decimal(18,4)");
            b.Property(e => e.UnbilledUsedQty).HasColumnType("decimal(18,4)");
            b.Property(e => e.BilledQty).HasColumnType("decimal(18,4)");
            b.HasOne<SiteJob>()
                .WithMany()
                .HasForeignKey(e => e.SiteId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SiteDispatch>(b =>
        {
            b.HasKey(e => e.SiteDispatchId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.DispatchNumber }).IsUnique();
            b.Property(e => e.Status).HasConversion<int>();
        });

        modelBuilder.Entity<SiteDispatchLine>(b =>
        {
            b.HasKey(e => e.SiteDispatchLineId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.SiteDispatchId });
            b.Property(e => e.DispatchedQuantity).HasColumnType("decimal(18,4)");
            b.Property(e => e.ReceivedQuantity).HasColumnType("decimal(18,4)");
            b.Property(e => e.UnitCost).HasColumnType("decimal(18,4)");
        });

        modelBuilder.Entity<SiteReturn>(b =>
        {
            b.HasKey(e => e.SiteReturnId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.ReturnNumber }).IsUnique();
            b.Property(e => e.Status).HasConversion<int>();
        });

        modelBuilder.Entity<SiteReturnLine>(b =>
        {
            b.HasKey(e => e.SiteReturnLineId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.SiteReturnId });
            b.Property(e => e.ReturnedQuantity).HasColumnType("decimal(18,4)");
        });

        modelBuilder.Entity<SiteMaterialUsage>(b =>
        {
            b.HasKey(e => e.SiteMaterialUsageId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.UsageNumber }).IsUnique();
            b.Property(e => e.ApprovalStatus).HasConversion<int>();
        });

        modelBuilder.Entity<SiteMaterialUsageLine>(b =>
        {
            b.HasKey(e => e.SiteMaterialUsageLineId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.SiteMaterialUsageId });
            b.Property(e => e.Quantity).HasColumnType("decimal(18,4)");
            b.Property(e => e.UnitCost).HasColumnType("decimal(18,4)");
            b.Property(e => e.SellingPrice).HasColumnType("decimal(18,4)");
        });

        modelBuilder.Entity<SiteBillingInvoice>(b =>
        {
            b.HasKey(e => e.SiteBillingInvoiceId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.BillingNumber }).IsUnique();
            b.Property(e => e.TotalLaborAmount).HasColumnType("decimal(18,4)");
            b.Property(e => e.TotalMaterialAmount).HasColumnType("decimal(18,4)");
            b.Property(e => e.TotalBillableAmount).HasColumnType("decimal(18,4)");
        });

        modelBuilder.Entity<SiteBillingInvoiceLine>(b =>
        {
            b.HasKey(e => e.SiteBillingInvoiceLineId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.SiteBillingInvoiceId });
            b.Property(e => e.Quantity).HasColumnType("decimal(18,4)");
            b.Property(e => e.UnitPrice).HasColumnType("decimal(18,4)");
            b.Property(e => e.TotalAmount).HasColumnType("decimal(18,4)");
            b.Property(e => e.LineType).HasConversion<int>();
        });

        modelBuilder.ConfigureNumberingSeries(ownsMigration: false);

        base.OnModelCreating(modelBuilder);
    }
}
