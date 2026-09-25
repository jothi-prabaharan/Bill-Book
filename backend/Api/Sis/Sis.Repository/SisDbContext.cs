using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Sis.Entity.TableEntities;

namespace Sis.Repository;

/// <summary>
/// The <c>sis</c> schema (S1, TK-61): academic years, classes, sections,
/// subjects, students and their guardians, enrolments, exams and marks. The
/// base class supplies the CustomerId/OrgId query filter, xmin concurrency and
/// the ErrorLogs table; <see cref="OnModelCreating"/> calls it last.
///
/// <c>NumberingSeries</c> is Accounting's table, mapped without migrations (the
/// settled exception in CLAUDE.md), so an admission number is taken in the same
/// transaction as the student and a failed insert gives it back.
/// </summary>
public class SisDbContext : TenantDbContext
{
    public SisDbContext(DbContextOptions<SisDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<SchoolClass> SchoolClasses => Set<SchoolClass>();
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<StudentGuardian> StudentGuardians => Set<StudentGuardian>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamSubject> ExamSubjects => Set<ExamSubject>();
    public DbSet<ExamMark> ExamMarks => Set<ExamMark>();

    public DbSet<NumberingSeries> NumberingSeries => Set<NumberingSeries>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("sis");

        modelBuilder.Entity<AcademicYear>(b =>
        {
            b.HasKey(e => e.AcademicYearId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.Code }).IsUnique();

            // Exactly one current year per branch.
            b.HasIndex(e => new { e.CustomerId, e.OrgId }).IsUnique().HasFilter("\"IsCurrent\" = true")
                .HasDatabaseName("IX_AcademicYears_OneCurrent");
            b.ToTable(t => t.HasCheckConstraint("chk_academic_year_dates", "\"EndDate\" > \"StartDate\""));
        });

        modelBuilder.Entity<SchoolClass>(b =>
        {
            b.HasKey(e => e.SchoolClassId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.Code }).IsUnique();
        });

        modelBuilder.Entity<Section>(b =>
        {
            b.HasKey(e => e.SectionId);
            b.HasIndex(e => new { e.AcademicYearId, e.SchoolClassId, e.Name }).IsUnique();
            b.HasOne<AcademicYear>().WithMany().HasForeignKey(e => e.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SchoolClass>().WithMany().HasForeignKey(e => e.SchoolClassId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Subject>(b =>
        {
            b.HasKey(e => e.SubjectId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.Code }).IsUnique();
            b.Property(e => e.SubjectKind).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<Student>(b =>
        {
            b.HasKey(e => e.StudentId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.AdmissionNo }).IsUnique();

            // One student per admitted application, so admitting twice is one student (S2).
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.SourceApplicationId }).IsUnique()
                .HasFilter("\"SourceApplicationId\" IS NOT NULL");
            b.HasIndex(e => new { e.OrgId, e.StudentStatus });

            b.Property(e => e.Gender).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.StudentStatus).HasConversion<string>().HasMaxLength(20);
            b.ToTable(t => t.HasCheckConstraint(
                "chk_student_leaving", "\"StudentStatus\" = 'Active' OR \"LeavingDate\" IS NOT NULL"));

            b.HasMany(e => e.Guardians).WithOne().HasForeignKey(g => g.StudentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StudentGuardian>(b =>
        {
            b.HasKey(e => e.StudentGuardianId);
            b.HasIndex(e => new { e.StudentId, e.ContactId }).IsUnique();

            // Exactly one primary guardian: the one invoiced.
            b.HasIndex(e => e.StudentId).IsUnique().HasFilter("\"IsPrimary\" = true")
                .HasDatabaseName("IX_StudentGuardians_OnePrimary");
            b.HasIndex(e => new { e.OrgId, e.ContactId });
            b.Property(e => e.Relationship).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<Enrolment>(b =>
        {
            b.HasKey(e => e.EnrolmentId);
            b.HasIndex(e => new { e.StudentId, e.AcademicYearId }).IsUnique();
            b.HasIndex(e => new { e.SectionId, e.RollNo }).IsUnique().HasFilter("\"RollNo\" IS NOT NULL");
            b.Property(e => e.EnrolmentStatus).HasConversion<string>().HasMaxLength(20);
            b.HasOne<Student>().WithMany().HasForeignKey(e => e.StudentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<AcademicYear>().WithMany().HasForeignKey(e => e.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Section>().WithMany().HasForeignKey(e => e.SectionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Exam>(b =>
        {
            b.HasKey(e => e.ExamId);
            b.Property(e => e.ExamStatus).HasConversion<string>().HasMaxLength(20);
            b.HasOne<AcademicYear>().WithMany().HasForeignKey(e => e.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(e => e.Subjects).WithOne().HasForeignKey(s => s.ExamId).OnDelete(DeleteBehavior.Cascade);
            b.ToTable(t => t.HasCheckConstraint("chk_exam_dates", "\"EndDate\" >= \"StartDate\""));
        });

        modelBuilder.Entity<ExamSubject>(b =>
        {
            b.HasKey(e => e.ExamSubjectId);
            b.HasIndex(e => new { e.ExamId, e.SubjectId, e.SchoolClassId }).IsUnique();
            b.Property(e => e.MaxMarks).HasColumnType("decimal(18,4)");
            b.Property(e => e.PassMarks).HasColumnType("decimal(18,4)");
            b.HasOne<Subject>().WithMany().HasForeignKey(e => e.SubjectId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SchoolClass>().WithMany().HasForeignKey(e => e.SchoolClassId).OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t => t.HasCheckConstraint("chk_exam_subject_marks", "\"MaxMarks\" > 0 AND \"PassMarks\" >= 0 AND \"PassMarks\" <= \"MaxMarks\""));
        });

        modelBuilder.Entity<ExamMark>(b =>
        {
            b.HasKey(e => e.ExamMarkId);
            b.HasIndex(e => new { e.ExamSubjectId, e.EnrolmentId }).IsUnique();
            b.Property(e => e.Marks).HasColumnType("decimal(18,4)");
            b.HasOne<ExamSubject>().WithMany().HasForeignKey(e => e.ExamSubjectId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<Enrolment>().WithMany().HasForeignKey(e => e.EnrolmentId).OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t => t.HasCheckConstraint(
                "chk_exam_mark", "(\"IsAbsent\" = true AND \"Marks\" IS NULL) OR (\"IsAbsent\" = false AND \"Marks\" >= 0)"));
        });

        modelBuilder.ConfigureNumberingSeries(ownsMigration: false);

        // Last, so the base class sees every entity configured above.
        base.OnModelCreating(modelBuilder);
    }
}
