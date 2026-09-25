using Attendance.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;

namespace Attendance.Repository;

/// <summary>
/// The <c>att</c> schema (S3, TK-63): student attendance and day locks. The
/// base class supplies the CustomerId/OrgId query filter, xmin concurrency and
/// ErrorLogs; <see cref="OnModelCreating"/> calls it last.
/// </summary>
public class AttendanceDbContext : TenantDbContext
{
    public AttendanceDbContext(DbContextOptions<AttendanceDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<StudentAttendance> StudentAttendance => Set<StudentAttendance>();
    public DbSet<AttendanceLock> AttendanceLocks => Set<AttendanceLock>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("att");

        modelBuilder.Entity<StudentAttendance>(b =>
        {
            b.HasKey(e => e.StudentAttendanceId);
            b.ToTable("StudentAttendance");

            // One mark per student per day (per period, once timetables exist).
            b.HasIndex(e => new { e.EnrolmentId, e.AttendanceDate }).IsUnique();
            b.HasIndex(e => new { e.OrgId, e.SectionId, e.AttendanceDate });
            b.Property(e => e.AttendanceStatus).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<AttendanceLock>(b =>
        {
            b.HasKey(e => e.AttendanceLockId);
            b.HasIndex(e => new { e.SectionId, e.AttendanceDate }).IsUnique();
        });

        base.OnModelCreating(modelBuilder);
    }
}
