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
    public DbSet<LaborerClockEntry> LaborerClockEntries => Set<LaborerClockEntry>();
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
            b.Property(e => e.StandardHourlyRate).HasColumnType("decimal(18,4)");
            b.Property(e => e.StandardOvertimeHourlyRate).HasColumnType("decimal(18,4)");
        });

        modelBuilder.Entity<Laborer>(b =>
        {
            b.HasKey(e => e.LaborerId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.Phone });
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.UserId }).HasFilter("\"UserId\" IS NOT NULL");
            b.Property(e => e.CostHourlyRate).HasColumnType("decimal(18,4)");
            b.Property(e => e.BillingHourlyRate).HasColumnType("decimal(18,4)");
            b.Property(e => e.LaborerType).HasConversion<int>();
        });

        modelBuilder.Entity<SiteJob>(b =>
        {
            b.HasKey(e => e.SiteId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.SiteCode }).IsUnique();
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.Status });
            b.Property(e => e.Latitude).HasColumnType("decimal(9,6)");
            b.Property(e => e.Longitude).HasColumnType("decimal(9,6)");
            b.Property(e => e.Status).HasConversion<int>();
        });

        modelBuilder.Entity<SiteAssignment>(b =>
        {
            b.HasKey(e => e.SiteAssignmentId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.SiteId, e.LaborerId });
        });

        modelBuilder.Entity<MarginRule>(b =>
        {
            b.HasKey(e => e.MarginRuleId);
            b.Property(e => e.MinLaborMarginPercent).HasColumnType("decimal(5,2)");
            b.Property(e => e.MinMaterialMarginPercent).HasColumnType("decimal(5,2)");
            b.Property(e => e.SubcontractorMarkupPercent).HasColumnType("decimal(5,2)");
        });

        modelBuilder.Entity<LaborerClockEntry>(b =>
        {
            b.HasKey(e => e.LaborerClockEntryId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.SiteId, e.LaborerId, e.ClockInUtc });
            b.Property(e => e.ClockInLatitude).HasColumnType("decimal(9,6)");
            b.Property(e => e.ClockInLongitude).HasColumnType("decimal(9,6)");
            b.Property(e => e.ClockOutLatitude).HasColumnType("decimal(9,6)");
            b.Property(e => e.ClockOutLongitude).HasColumnType("decimal(9,6)");
            b.Property(e => e.RegularHours).HasColumnType("decimal(6,2)");
            b.Property(e => e.OvertimeHours).HasColumnType("decimal(6,2)");
            b.Property(e => e.ApprovalStatus).HasConversion<int>();
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
