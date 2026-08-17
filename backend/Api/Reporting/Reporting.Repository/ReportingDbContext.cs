using Microsoft.EntityFrameworkCore;
using Reporting.Entity.TableEntities;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository;

/// <summary>
/// The rpt schema, in a per-customer database. The base class supplies the OrgId
/// query filter, the insert-time OrgId stamp and xmin concurrency, so nothing
/// here needs to remember them.
///
/// <b>This context owns three tables and reads many more.</b> The three below are
/// its own — the report catalog, its column metadata and saved layouts. The
/// reports themselves read acc, inv and con, and those come in as read-only
/// models mapped with <c>ExcludeFromMigrations</c> in R0.2: a recorded exception
/// to the rule against crossing a service boundary, argued in REPORTS.md §2,
/// and specific to reporting. <b>Nothing in this context ever writes to another
/// schema.</b>
/// </summary>
public class ReportingDbContext : TenantDbContext
{
    public ReportingDbContext(DbContextOptions<ReportingDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<Report> Reports => Set<Report>();

    public DbSet<ReportDetail> ReportDetails => Set<ReportDetail>();

    public DbSet<ReportView> ReportViews => Set<ReportView>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("rpt");

        modelBuilder.Entity<Report>(b =>
        {
            b.HasKey(e => e.ReportId);

            // The key is how every route names a report, so it has to be unique
            // within the branch. Across branches it repeats — each has its own
            // copy of the catalog.
            b.HasIndex(e => new { e.OrgId, e.ReportKey }).IsUnique();

            b.HasIndex(e => new { e.OrgId, e.Module, e.SortOrder })
                .HasDatabaseName("IX_Reports_Order");
        });

        modelBuilder.Entity<ReportDetail>(b =>
        {
            b.HasKey(e => e.ReportDetailId);

            b.HasIndex(e => new { e.OrgId, e.ReportId, e.ColumnKey }).IsUnique();

            b.HasIndex(e => new { e.OrgId, e.ReportId, e.SortOrder })
                .HasDatabaseName("IX_ReportDetails_Order");

            // Cascade: a column's presentation has no meaning without its report,
            // and leaving orphans behind would fail the startup check that
            // compares seeded columns against source columns.
            b.HasOne<Report>()
                .WithMany()
                .HasForeignKey(e => e.ReportId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReportView>(b =>
        {
            b.HasKey(e => e.ReportViewId);

            b.Property(e => e.LayoutJson).HasColumnType("jsonb");

            b.HasIndex(e => new { e.OrgId, e.ReportId, e.OwnerUserId, e.ViewName })
                .IsUnique()
                .HasDatabaseName("IX_ReportViews_Name");

            // One default per user per report. A filtered unique index rather
            // than a C# check, because two requests setting a default at once
            // both pass a C# check and only one can pass this.
            b.HasIndex(e => new { e.OrgId, e.ReportId, e.OwnerUserId })
                .IsUnique()
                .HasFilter("\"IsDefault\" = true")
                .HasDatabaseName("IX_ReportViews_Default");

            b.HasOne<Report>()
                .WithMany()
                .HasForeignKey(e => e.ReportId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        base.OnModelCreating(modelBuilder);
    }
}
