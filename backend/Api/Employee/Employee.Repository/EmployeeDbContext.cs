using Employee.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;

namespace Employee.Repository;

/// <summary>
/// The <c>hrm</c> schema (H1, TK-48): organisation setup and the shared
/// employee master, in the tenant database beside every other schema. The base
/// class supplies the CustomerId/OrgId query filter, xmin concurrency and the
/// ErrorLogs table, and <see cref="OnModelCreating"/> calls it last, as
/// <c>SalesDbContext</c> once forgot to.
///
/// <c>NumberingSeries</c> is Accounting's table, mapped here without migrations
/// (the settled exception in CLAUDE.md), so an employee's <c>EMP</c> code is
/// taken in the same transaction as the employee and a failed insert gives it back.
/// </summary>
public class EmployeeDbContext : TenantDbContext
{
    public EmployeeDbContext(DbContextOptions<EmployeeDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Designation> Designations => Set<Designation>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<CostCentre> CostCentres => Set<CostCentre>();
    public DbSet<WorkLocation> WorkLocations => Set<WorkLocation>();

    public DbSet<EmployeeRecord> Employees => Set<EmployeeRecord>();
    public DbSet<EmployeeAddress> EmployeeAddresses => Set<EmployeeAddress>();
    public DbSet<EmployeeContact> EmployeeContacts => Set<EmployeeContact>();
    public DbSet<EmployeeFamilyMember> EmployeeFamilyMembers => Set<EmployeeFamilyMember>();
    public DbSet<EmployeeNominee> EmployeeNominees => Set<EmployeeNominee>();
    public DbSet<EmployeeEducation> EmployeeEducation => Set<EmployeeEducation>();
    public DbSet<PreviousEmployment> PreviousEmployments => Set<PreviousEmployment>();
    public DbSet<EmployeeBankDetail> EmployeeBankDetails => Set<EmployeeBankDetail>();
    public DbSet<EmploymentHistory> EmploymentHistories => Set<EmploymentHistory>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();
    public DbSet<AssetIssue> AssetIssues => Set<AssetIssue>();

    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<PolicyDocument> PolicyDocuments => Set<PolicyDocument>();
    public DbSet<PolicyAcknowledgement> PolicyAcknowledgements => Set<PolicyAcknowledgement>();

    public DbSet<ChecklistTemplate> ChecklistTemplates => Set<ChecklistTemplate>();
    public DbSet<ChecklistTemplateItem> ChecklistTemplateItems => Set<ChecklistTemplateItem>();
    public DbSet<EmployeeChecklist> EmployeeChecklists => Set<EmployeeChecklist>();
    public DbSet<EmployeeChecklistItem> EmployeeChecklistItems => Set<EmployeeChecklistItem>();
    public DbSet<Separation> Separations => Set<Separation>();
    public DbSet<RelationshipType> RelationshipTypes => Set<RelationshipType>();
    public DbSet<EmployeeRelationship> EmployeeRelationships => Set<EmployeeRelationship>();

    public DbSet<NumberingSeries> NumberingSeries => Set<NumberingSeries>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("hrm");

        // ---- Organisation: codes unique per branch --------------------------
        modelBuilder.Entity<Department>(b =>
        {
            b.HasKey(e => e.DepartmentId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.Code }).IsUnique();
        });
        modelBuilder.Entity<Designation>(b =>
        {
            b.HasKey(e => e.DesignationId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.Code }).IsUnique();
        });
        modelBuilder.Entity<Grade>(b =>
        {
            b.HasKey(e => e.GradeId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.Code }).IsUnique();
        });
        modelBuilder.Entity<CostCentre>(b =>
        {
            b.HasKey(e => e.CostCentreId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.Code }).IsUnique();
        });
        modelBuilder.Entity<WorkLocation>(b =>
        {
            b.HasKey(e => e.WorkLocationId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.Code }).IsUnique();
            b.Property(e => e.Latitude).HasColumnType("decimal(9,6)");
            b.Property(e => e.Longitude).HasColumnType("decimal(9,6)");
        });

        // ---- Employee --------------------------------------------------------
        modelBuilder.Entity<EmployeeRecord>(b =>
        {
            b.HasKey(e => e.EmployeeId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.EmployeeCode }).IsUnique();

            // One employee per login per branch, so self-service knows who "me" is.
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.UserId })
                .IsUnique()
                .HasFilter("\"UserId\" IS NOT NULL");

            b.HasIndex(e => new { e.OrgId, e.EmployeeStatus });

            b.Property(e => e.Gender).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.MaritalStatus).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.EmploymentType).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.EmployeeStatus).HasConversion<string>().HasMaxLength(20);

            // The organisation masters are deactivated, never deleted, so a
            // reference to one is restricted rather than cascaded.
            b.HasOne<Department>().WithMany().HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Designation>().WithMany().HasForeignKey(e => e.DesignationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Grade>().WithMany().HasForeignKey(e => e.GradeId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<WorkLocation>().WithMany().HasForeignKey(e => e.WorkLocationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<CostCentre>().WithMany().HasForeignKey(e => e.CostCentreId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<EmployeeRecord>().WithMany().HasForeignKey(e => e.ReportsToEmployeeId).OnDelete(DeleteBehavior.Restrict);

            // Every collection names its own column, so EF never maps a second,
            // shadow key beside it (the sal bug of 21 August).
            b.HasMany(e => e.Addresses).WithOne().HasForeignKey(c => c.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(e => e.Contacts).WithOne().HasForeignKey(c => c.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(e => e.FamilyMembers).WithOne().HasForeignKey(c => c.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(e => e.Nominees).WithOne().HasForeignKey(c => c.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(e => e.Education).WithOne().HasForeignKey(c => c.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(e => e.PreviousEmployments).WithOne().HasForeignKey(c => c.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(e => e.BankDetails).WithOne().HasForeignKey(c => c.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(e => e.Documents).WithOne().HasForeignKey(c => c.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(e => e.AssetIssues).WithOne().HasForeignKey(c => c.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmployeeAddress>(b =>
        {
            b.HasKey(e => e.EmployeeAddressId);
            b.Property(e => e.AddressKind).HasConversion<string>().HasMaxLength(20);
            b.HasIndex(e => new { e.EmployeeId, e.AddressKind }).IsUnique();
        });
        modelBuilder.Entity<EmployeeContact>(b =>
        {
            b.HasKey(e => e.EmployeeContactId);
            b.Property(e => e.Relationship).HasConversion<string>().HasMaxLength(20);
        });
        modelBuilder.Entity<EmployeeFamilyMember>(b =>
        {
            b.HasKey(e => e.EmployeeFamilyMemberId);
            b.Property(e => e.Relationship).HasConversion<string>().HasMaxLength(20);
        });
        modelBuilder.Entity<EmployeeNominee>(b =>
        {
            b.HasKey(e => e.EmployeeNomineeId);
            b.Property(e => e.NominationKind).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.SharePercent).HasColumnType("decimal(18,4)");
            b.HasOne(e => e.FamilyMember).WithMany().HasForeignKey(e => e.FamilyMemberId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(e => new { e.EmployeeId, e.NominationKind, e.FamilyMemberId }).IsUnique();
        });
        modelBuilder.Entity<EmployeeEducation>(b => b.HasKey(e => e.EmployeeEducationId));
        modelBuilder.Entity<PreviousEmployment>(b => b.HasKey(e => e.PreviousEmploymentId));
        modelBuilder.Entity<EmployeeBankDetail>(b =>
        {
            b.HasKey(e => e.EmployeeBankDetailId);
            // Exactly one primary account, held by the database as well as by C#.
            b.HasIndex(e => e.EmployeeId).IsUnique().HasFilter("\"IsPrimary\"").HasDatabaseName("IX_EmployeeBankDetails_Primary");
        });
        modelBuilder.Entity<EmploymentHistory>(b =>
        {
            b.HasKey(e => e.EmploymentHistoryId);
            b.Property(e => e.ChangeKind).HasConversion<string>().HasMaxLength(20);
            b.HasOne<EmployeeRecord>().WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(e => new { e.EmployeeId, e.EffectiveDate });
        });
        modelBuilder.Entity<EmployeeDocument>(b =>
        {
            b.HasKey(e => e.EmployeeDocumentId);
            b.Property(e => e.DocumentKind).HasConversion<string>().HasMaxLength(20);
        });
        modelBuilder.Entity<AssetIssue>(b =>
        {
            b.HasKey(e => e.AssetIssueId);
            b.Property(e => e.RecoveryAmount).HasColumnType("decimal(18,4)");
        });

        // ---- Notices ---------------------------------------------------------
        modelBuilder.Entity<Announcement>(b =>
        {
            b.HasKey(e => e.AnnouncementId);
            b.Property(e => e.Audience).HasConversion<string>().HasMaxLength(20);
            b.HasIndex(e => new { e.OrgId, e.PublishDate });
        });
        modelBuilder.Entity<PolicyDocument>(b => b.HasKey(e => e.PolicyDocumentId));
        modelBuilder.Entity<PolicyAcknowledgement>(b =>
        {
            b.HasKey(e => e.PolicyAcknowledgementId);
            b.HasOne<PolicyDocument>().WithMany().HasForeignKey(e => e.PolicyDocumentId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<EmployeeRecord>().WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(e => new { e.PolicyDocumentId, e.EmployeeId }).IsUnique();
        });

        // ---- Lifecycle & Exit ------------------------------------------------
        modelBuilder.Entity<ChecklistTemplate>(b =>
        {
            b.HasKey(e => e.ChecklistTemplateId);
            b.Property(e => e.Kind).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<ChecklistTemplateItem>(b =>
        {
            b.HasKey(e => e.ChecklistTemplateItemId);
            b.Property(e => e.OwnerRole).HasConversion<string>().HasMaxLength(20);
            b.HasOne(e => e.Template)
                .WithMany(t => t.Items)
                .HasForeignKey(e => e.ChecklistTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmployeeChecklist>(b =>
        {
            b.HasKey(e => e.EmployeeChecklistId);
            b.Property(e => e.Kind).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<EmployeeChecklistItem>(b =>
        {
            b.HasKey(e => e.EmployeeChecklistItemId);
            b.Property(e => e.OwnerRole).HasConversion<string>().HasMaxLength(20);
            b.HasOne(e => e.Checklist)
                .WithMany(c => c.Items)
                .HasForeignKey(e => e.EmployeeChecklistId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Separation>(b =>
        {
            b.HasKey(e => e.SeparationId);
            b.Property(e => e.Kind).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.NoticeShortfallDays).HasColumnType("decimal(18,4)");
        });

        // ---- Relationships (TK-99): "Lead", "Project Lead" ----------------
        modelBuilder.Entity<RelationshipType>(b =>
        {
            b.HasKey(e => e.RelationshipTypeId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.Code }).IsUnique();
        });
        modelBuilder.Entity<EmployeeRelationship>(b =>
        {
            b.HasKey(e => e.EmployeeRelationshipId);
            b.HasOne<EmployeeRecord>().WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<EmployeeRecord>().WithMany().HasForeignKey(e => e.RelatedEmployeeId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<RelationshipType>().WithMany().HasForeignKey(e => e.RelationshipTypeId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(e => new { e.EmployeeId, e.RelationshipTypeId, e.FromDate });
        });

        modelBuilder.ConfigureNumberingSeries(ownsMigration: false);

        // Last, so the base class sees every entity configured above.
        base.OnModelCreating(modelBuilder);
    }
}
