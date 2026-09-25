using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;
using TimeLeave.Entity.TableEntities;

namespace TimeLeave.Repository;

public class TimeLeaveDbContext : TenantDbContext
{
    public TimeLeaveDbContext(DbContextOptions<TimeLeaveDbContext> options, TenantContext tenantContext)
        : base(options, tenantContext)
    {
    }

    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeavePolicy> LeavePolicies => Set<LeavePolicy>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<LeaveApplication> LeaveApplications => Set<LeaveApplication>();
    public DbSet<LeaveEncashment> LeaveEncashments => Set<LeaveEncashment>();
    public DbSet<HolidayList> HolidayLists => Set<HolidayList>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<WeeklyOffPolicy> WeeklyOffPolicies => Set<WeeklyOffPolicy>();
    public DbSet<ShiftRoster> ShiftRosters => Set<ShiftRoster>();
    public DbSet<Punch> Punches => Set<Punch>();
    public DbSet<BiometricDeviceUser> BiometricDeviceUsers => Set<BiometricDeviceUser>();
    public DbSet<DailyAttendance> DailyAttendances => Set<DailyAttendance>();
    public DbSet<RegularisationRequest> RegularisationRequests => Set<RegularisationRequest>();
    public DbSet<OvertimeRequest> OvertimeRequests => Set<OvertimeRequest>();
    public DbSet<CompOffCredit> CompOffCredits => Set<CompOffCredit>();
    public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("tla");

        modelBuilder.Entity<LeaveType>(b =>
        {
            b.HasKey(e => e.LeaveTypeId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.Code }).IsUnique();
            b.Property(e => e.Gender).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.IsAttachmentRequiredAboveDays).HasColumnType("decimal(18,4)");
        });

        modelBuilder.Entity<LeavePolicy>(b =>
        {
            b.HasKey(e => e.LeavePolicyId);
            b.Property(e => e.AccrualKind).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.CarryForwardKind).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.AnnualQuota).HasColumnType("decimal(18,4)");
            b.Property(e => e.MaxCarryForward).HasColumnType("decimal(18,4)");
            b.Property(e => e.MaxEncashPerYear).HasColumnType("decimal(18,4)");
            b.Property(e => e.MinDaysPerApplication).HasColumnType("decimal(18,4)");
            b.Property(e => e.MaxDaysPerApplication).HasColumnType("decimal(18,4)");
            b.HasOne(e => e.LeaveType).WithMany().HasForeignKey(e => e.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LeaveBalance>(b =>
        {
            b.HasKey(e => e.LeaveBalanceId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.EmployeeId, e.LeaveTypeId, e.LeaveYear }).IsUnique();
            b.Property(e => e.Opening).HasColumnType("decimal(18,4)");
            b.Property(e => e.Accrued).HasColumnType("decimal(18,4)");
            b.Property(e => e.Taken).HasColumnType("decimal(18,4)");
            b.Property(e => e.Encashed).HasColumnType("decimal(18,4)");
            b.Property(e => e.Lapsed).HasColumnType("decimal(18,4)");
            b.Property(e => e.Adjusted).HasColumnType("decimal(18,4)");
            b.HasOne(e => e.LeaveType).WithMany().HasForeignKey(e => e.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LeaveApplication>(b =>
        {
            b.HasKey(e => e.LeaveApplicationId);
            b.Property(e => e.FromHalf).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.ToHalf).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.LeaveStatus).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.ApprovalStatus).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.Days).HasColumnType("decimal(18,4)");
            b.HasOne(e => e.LeaveType).WithMany().HasForeignKey(e => e.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LeaveEncashment>(b =>
        {
            b.HasKey(e => e.LeaveEncashmentId);
            b.Property(e => e.EncashmentStatus).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.ApprovalStatus).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.Days).HasColumnType("decimal(18,4)");
            b.HasOne(e => e.LeaveType).WithMany().HasForeignKey(e => e.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HolidayList>(b =>
        {
            b.HasKey(e => e.HolidayListId);
        });

        modelBuilder.Entity<Holiday>(b =>
        {
            b.HasKey(e => e.HolidayId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.HolidayListId, e.HolidayDate }).IsUnique();
            b.HasOne(e => e.HolidayList).WithMany(h => h.Holidays).HasForeignKey(e => e.HolidayListId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Shift>(b =>
        {
            b.HasKey(e => e.ShiftId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.Code }).IsUnique();
        });

        modelBuilder.Entity<WeeklyOffPolicy>(b =>
        {
            b.HasKey(e => e.WeeklyOffPolicyId);
            b.Property(e => e.MondayRule).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.TuesdayRule).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.WednesdayRule).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.ThursdayRule).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.FridayRule).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.SaturdayRule).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.SundayRule).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<ShiftRoster>(b =>
        {
            b.HasKey(e => e.ShiftRosterId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.EmployeeId, e.FromDate, e.ToDate });
            b.HasOne(e => e.Shift).WithMany().HasForeignKey(e => e.ShiftId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(e => e.WeeklyOffPolicy).WithMany().HasForeignKey(e => e.WeeklyOffPolicyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Punch>(b =>
        {
            b.HasKey(e => e.PunchId);
            b.Property(e => e.PunchSource).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.Latitude).HasColumnType("decimal(9,6)");
            b.Property(e => e.Longitude).HasColumnType("decimal(9,6)");
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.EmployeeId, e.PunchedAt });
        });

        modelBuilder.Entity<BiometricDeviceUser>(b =>
        {
            b.HasKey(e => e.BiometricDeviceUserId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.DeviceCode, e.DeviceUserId }).IsUnique();
        });

        modelBuilder.Entity<DailyAttendance>(b =>
        {
            b.HasKey(e => e.DailyAttendanceId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.EmployeeId, e.AttendanceDate }).IsUnique();
            b.Property(e => e.AttendanceStatus).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.AttendanceSource).HasConversion<string>().HasMaxLength(20);
            b.HasOne(e => e.Shift).WithMany().HasForeignKey(e => e.ShiftId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RegularisationRequest>(b =>
        {
            b.HasKey(e => e.RegularisationRequestId);
            b.Property(e => e.RequestedStatus).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.ApprovalStatus).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<OvertimeRequest>(b =>
        {
            b.HasKey(e => e.OvertimeRequestId);
            b.Property(e => e.OvertimeRate).HasColumnType("decimal(18,4)");
            b.Property(e => e.ApprovalStatus).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<CompOffCredit>(b =>
        {
            b.HasKey(e => e.CompOffCreditId);
            b.Property(e => e.Days).HasColumnType("decimal(18,4)");
            b.Property(e => e.AvailedDays).HasColumnType("decimal(18,4)");
        });

        modelBuilder.Entity<ApprovalStep>(b =>
        {
            b.HasKey(e => e.ApprovalStepId);
            b.Property(e => e.RequestKind).HasConversion<string>().HasMaxLength(30);
            b.Property(e => e.StepStatus).HasConversion<string>().HasMaxLength(20);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.RequestKind, e.RequestId, e.Sequence });
        });

        base.OnModelCreating(modelBuilder);
    }
}
