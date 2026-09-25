using Fee.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;

namespace Fee.Repository;

/// <summary>
/// The <c>fee</c> schema (S4, TK-64). The base class supplies the
/// CustomerId/OrgId query filter, xmin concurrency and ErrorLogs;
/// <see cref="OnModelCreating"/> calls it last. <c>NumberingSeries</c> is
/// Accounting's, mapped without migrations, so a demand or receipt number is
/// taken in the document's own transaction and a refused save gives it back.
/// </summary>
public class FeeDbContext : TenantDbContext
{
    public FeeDbContext(DbContextOptions<FeeDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<FeeHead> FeeHeads => Set<FeeHead>();
    public DbSet<FeeStructure> FeeStructures => Set<FeeStructure>();
    public DbSet<FeeStructureLine> FeeStructureLines => Set<FeeStructureLine>();
    public DbSet<FeeConcession> FeeConcessions => Set<FeeConcession>();
    public DbSet<FeeDemand> FeeDemands => Set<FeeDemand>();
    public DbSet<FeeDemandLine> FeeDemandLines => Set<FeeDemandLine>();
    public DbSet<FeeReceipt> FeeReceipts => Set<FeeReceipt>();
    public DbSet<FeeReceiptAllocation> FeeReceiptAllocations => Set<FeeReceiptAllocation>();

    public DbSet<NumberingSeries> NumberingSeries => Set<NumberingSeries>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("fee");

        modelBuilder.Entity<FeeHead>(b =>
        {
            b.HasKey(e => e.FeeHeadId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.Code }).IsUnique();
        });

        modelBuilder.Entity<FeeStructure>(b =>
        {
            b.HasKey(e => e.FeeStructureId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.AcademicYearId, e.SchoolClassId, e.Name }).IsUnique();
            b.HasMany(e => e.Lines).WithOne().HasForeignKey(l => l.FeeStructureId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FeeStructureLine>(b =>
        {
            b.HasKey(e => e.FeeStructureLineId);
            b.Property(e => e.Amount).HasColumnType("decimal(18,4)");
            b.Property(e => e.Frequency).HasConversion<string>().HasMaxLength(20);
            b.HasOne<FeeHead>().WithMany().HasForeignKey(e => e.FeeHeadId).OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t => t.HasCheckConstraint("chk_fee_structure_line", "\"Amount\" > 0 AND \"DueDay\" BETWEEN 1 AND 28"));
        });

        modelBuilder.Entity<FeeConcession>(b =>
        {
            b.HasKey(e => e.FeeConcessionId);
            b.HasIndex(e => new { e.OrgId, e.StudentId });
            b.Property(e => e.ConcessionKind).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.Value).HasColumnType("decimal(18,4)");
            b.HasOne<FeeHead>().WithMany().HasForeignKey(e => e.FeeHeadId).OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t => t.HasCheckConstraint(
                "chk_fee_concession",
                "\"Value\" > 0 AND (\"ConcessionKind\" <> 'Percent' OR \"Value\" <= 100) AND \"ValidTo\" >= \"ValidFrom\""));
        });

        modelBuilder.Entity<FeeDemand>(b =>
        {
            b.HasKey(e => e.FeeDemandId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.DemandNo }).IsUnique().HasFilter("\"DemandNo\" IS NOT NULL");

            // One demand per enrolment, structure and period: running a period
            // again finds these and raises nothing new.
            b.HasIndex(e => new { e.EnrolmentId, e.FeeStructureId, e.PeriodKey }).IsUnique();
            b.HasIndex(e => new { e.OrgId, e.ContactId, e.DocumentStatus });

            b.Property(e => e.DocumentStatus).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.ExchangeRate).HasColumnType("decimal(18,6)");
            b.Property(e => e.TotalAmount).HasColumnType("decimal(18,4)");
            b.Property(e => e.ConcessionAmount).HasColumnType("decimal(18,4)");
            b.Property(e => e.NetAmount).HasColumnType("decimal(18,4)");
            b.Property(e => e.PaidAmount).HasColumnType("decimal(18,4)");
            b.HasOne<FeeStructure>().WithMany().HasForeignKey(e => e.FeeStructureId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(e => e.Lines).WithOne().HasForeignKey(l => l.FeeDemandId).OnDelete(DeleteBehavior.Cascade);

            b.ToTable(t =>
            {
                t.HasCheckConstraint("chk_fee_demand_amounts",
                    "\"NetAmount\" = \"TotalAmount\" - \"ConcessionAmount\" AND \"NetAmount\" >= 0 "
                    + "AND \"PaidAmount\" >= 0 AND \"PaidAmount\" <= \"NetAmount\"");
                t.HasCheckConstraint("chk_fee_demand_posted_numbered", "\"DocumentStatus\" = 'Draft' OR \"DemandNo\" IS NOT NULL");
            });
        });

        modelBuilder.Entity<FeeDemandLine>(b =>
        {
            b.HasKey(e => e.FeeDemandLineId);
            b.Property(e => e.Amount).HasColumnType("decimal(18,4)");
            b.Property(e => e.ConcessionAmount).HasColumnType("decimal(18,4)");
            b.HasOne<FeeHead>().WithMany().HasForeignKey(e => e.FeeHeadId).OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t => t.HasCheckConstraint("chk_fee_demand_line", "\"Amount\" > 0 AND \"ConcessionAmount\" >= 0 AND \"ConcessionAmount\" <= \"Amount\""));
        });

        modelBuilder.Entity<FeeReceipt>(b =>
        {
            b.HasKey(e => e.FeeReceiptId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.ReceiptNo }).IsUnique();
            b.HasIndex(e => new { e.OrgId, e.ContactId });
            b.Property(e => e.PaymentMode).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.DocumentStatus).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.Amount).HasColumnType("decimal(18,4)");
            b.Property(e => e.UnallocatedAmount).HasColumnType("decimal(18,4)");
            b.HasMany(e => e.Allocations).WithOne().HasForeignKey(a => a.FeeReceiptId).OnDelete(DeleteBehavior.Cascade);
            b.ToTable(t => t.HasCheckConstraint(
                "chk_fee_receipt_amounts", "\"Amount\" > 0 AND \"UnallocatedAmount\" >= 0 AND \"UnallocatedAmount\" <= \"Amount\""));
        });

        modelBuilder.Entity<FeeReceiptAllocation>(b =>
        {
            b.HasKey(e => e.FeeReceiptAllocationId);
            b.HasIndex(e => new { e.FeeReceiptId, e.FeeDemandId }).IsUnique();
            b.HasIndex(e => e.FeeDemandId);
            b.Property(e => e.Amount).HasColumnType("decimal(18,4)");
            b.HasOne<FeeDemand>().WithMany().HasForeignKey(e => e.FeeDemandId).OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t => t.HasCheckConstraint("chk_fee_allocation", "\"Amount\" > 0"));
        });

        modelBuilder.ConfigureNumberingSeries(ownsMigration: false);

        base.OnModelCreating(modelBuilder);
    }
}
