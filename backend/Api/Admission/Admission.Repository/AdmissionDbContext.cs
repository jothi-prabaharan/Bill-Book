using Admission.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;

namespace Admission.Repository;

/// <summary>
/// The <c>adm</c> schema (S2, TK-62): enquiries, applications and their
/// documents. The base class supplies the CustomerId/OrgId query filter, xmin
/// concurrency and ErrorLogs; <see cref="OnModelCreating"/> calls it last.
/// <c>NumberingSeries</c> is Accounting's, mapped without migrations, so an
/// application number is taken in the application's own transaction.
/// </summary>
public class AdmissionDbContext : TenantDbContext
{
    public AdmissionDbContext(DbContextOptions<AdmissionDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<Enquiry> Enquiries => Set<Enquiry>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<ApplicationDocument> ApplicationDocuments => Set<ApplicationDocument>();

    public DbSet<NumberingSeries> NumberingSeries => Set<NumberingSeries>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("adm");

        modelBuilder.Entity<Enquiry>(b =>
        {
            b.HasKey(e => e.EnquiryId);
            b.HasIndex(e => new { e.OrgId, e.EnquiryStatus, e.FollowUpDate });
            b.Property(e => e.EnquirySource).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.EnquiryStatus).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<Application>(b =>
        {
            b.HasKey(e => e.ApplicationId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.ApplicationNo }).IsUnique();

            // One application per enquiry.
            b.HasIndex(e => e.EnquiryId).IsUnique().HasFilter("\"EnquiryId\" IS NOT NULL");
            b.HasIndex(e => new { e.OrgId, e.ApplicationStage });

            b.Property(e => e.ChildGender).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.GuardianRelationship).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.ApplicationStage).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.AssessmentScore).HasColumnType("decimal(18,4)");
            b.Property(e => e.ApplicationFee).HasColumnType("decimal(18,4)");

            b.HasOne<Enquiry>().WithMany().HasForeignKey(e => e.EnquiryId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(e => e.Documents).WithOne().HasForeignKey(d => d.ApplicationId).OnDelete(DeleteBehavior.Cascade);

            // Admitted means a student exists: the one column cannot say it without the other.
            b.ToTable(t => t.HasCheckConstraint(
                "chk_application_admitted", "\"ApplicationStage\" <> 'Admitted' OR \"AdmittedStudentId\" IS NOT NULL"));
        });

        modelBuilder.Entity<ApplicationDocument>(b =>
        {
            b.HasKey(e => e.ApplicationDocumentId);
            b.Property(e => e.DocumentKind).HasConversion<string>().HasMaxLength(30);
        });

        modelBuilder.ConfigureNumberingSeries(ownsMigration: false);

        base.OnModelCreating(modelBuilder);
    }
}
