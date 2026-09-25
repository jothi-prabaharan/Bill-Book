using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Apps;

namespace Reporting.Api.Services.Sources.Hrms;

// 7. Daily Attendance
public class DailyAttendanceRow
{
    public long Id { get; set; }
    public DateOnly AttendanceDate { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string Department { get; set; } = null!;
    public string ShiftName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public int WorkedMinutes { get; set; }
    public int LateMinutes { get; set; }
    public int EarlyOutMinutes { get; set; }
    public int OvertimeMinutes { get; set; }
}

public sealed class DailyAttendanceSource : ReportSource<DailyAttendanceRow>
{
    public override string ReportKey => "daily-attendance";
    public override string Title => "Daily Attendance";
    public override ReportModule Module => ReportModule.Time;
    public override App App => App.Hrms;
    public override string RequiredPermission => "attendance.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<DailyAttendanceRow, DateOnly>("attendanceDate", ColumnDataType.Date, r => r.AttendanceDate),
        ReportColumn.Of<DailyAttendanceRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<DailyAttendanceRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<DailyAttendanceRow, string>("department", ColumnDataType.Text, r => r.Department, groupable: true),
        ReportColumn.Of<DailyAttendanceRow, string>("shiftName", ColumnDataType.Text, r => r.ShiftName, groupable: true),
        ReportColumn.Of<DailyAttendanceRow, string>("status", ColumnDataType.Text, r => r.Status, groupable: true),
        ReportColumn.Of<DailyAttendanceRow, int>("workedMinutes", ColumnDataType.Quantity, r => r.WorkedMinutes, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<DailyAttendanceRow, int>("lateMinutes", ColumnDataType.Quantity, r => r.LateMinutes, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<DailyAttendanceRow, int>("earlyOutMinutes", ColumnDataType.Quantity, r => r.EarlyOutMinutes, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<DailyAttendanceRow, int>("overtimeMinutes", ColumnDataType.Quantity, r => r.OvertimeMinutes, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<DailyAttendanceRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<DailyAttendanceRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from a in db.DailyAttendances
               join e in db.Employees on a.EmployeeId equals e.EmployeeId
               join d in db.Departments on e.DepartmentId equals d.DepartmentId into gd from d in gd.DefaultIfEmpty()
               join s in db.Shifts on a.ShiftId equals s.ShiftId into gs from s in gs.DefaultIfEmpty()
               select new DailyAttendanceRow
               {
                   Id = a.DailyAttendanceId,
                   AttendanceDate = a.AttendanceDate,
                   EmployeeCode = e.EmployeeCode,
                   EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                   Department = d != null ? d.Name : "Unassigned",
                   ShiftName = s != null ? s.Name : "Default",
                   Status = a.AttendanceStatus,
                   WorkedMinutes = a.WorkedMinutes,
                   LateMinutes = a.LateMinutes,
                   EarlyOutMinutes = a.EarlyOutMinutes,
                   OvertimeMinutes = a.OvertimeMinutes,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<DailyAttendanceRow, DateOnly>>)(r => r.AttendanceDate);
}

// 8. Monthly Muster Roll
public class MonthlyMusterRollRow
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string Department { get; set; } = null!;
    public int PresentDays { get; set; }
    public int AbsentDays { get; set; }
    public int HalfDays { get; set; }
    public int TotalRecorded { get; set; }
}

public sealed class MonthlyMusterRollSource : ReportSource<MonthlyMusterRollRow>
{
    public override string ReportKey => "monthly-muster-roll";
    public override string Title => "Monthly Muster Roll";
    public override ReportModule Module => ReportModule.Time;
    public override App App => App.Hrms;
    public override string RequiredPermission => "attendance.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<MonthlyMusterRollRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<MonthlyMusterRollRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<MonthlyMusterRollRow, string>("department", ColumnDataType.Text, r => r.Department, groupable: true),
        ReportColumn.Of<MonthlyMusterRollRow, int>("presentDays", ColumnDataType.Quantity, r => r.PresentDays, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<MonthlyMusterRollRow, int>("absentDays", ColumnDataType.Quantity, r => r.AbsentDays, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<MonthlyMusterRollRow, int>("halfDays", ColumnDataType.Quantity, r => r.HalfDays, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<MonthlyMusterRollRow, int>("totalRecorded", ColumnDataType.Quantity, r => r.TotalRecorded, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<MonthlyMusterRollRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<MonthlyMusterRollRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from a in db.DailyAttendances
               join e in db.Employees on a.EmployeeId equals e.EmployeeId
               join d in db.Departments on e.DepartmentId equals d.DepartmentId into gd from d in gd.DefaultIfEmpty()
               group a by new
               {
                   e.EmployeeId,
                   e.EmployeeCode,
                   EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                   Department = d != null ? d.Name : "Unassigned",
               } into grp
               select new MonthlyMusterRollRow
               {
                   Id = grp.Key.EmployeeId,
                   EmployeeCode = grp.Key.EmployeeCode,
                   EmployeeName = grp.Key.EmployeeName,
                   Department = grp.Key.Department,
                   PresentDays = grp.Count(x => x.AttendanceStatus == "Present"),
                   AbsentDays = grp.Count(x => x.AttendanceStatus == "Absent"),
                   HalfDays = grp.Count(x => x.AttendanceStatus == "HalfDay"),
                   TotalRecorded = grp.Count(),
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<MonthlyMusterRollRow, string>>)(r => r.EmployeeCode);
}

// 9. Late and Early-Out
public class LateEarlyOutRow
{
    public long Id { get; set; }
    public DateOnly AttendanceDate { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string Department { get; set; } = null!;
    public int LateMinutes { get; set; }
    public int EarlyOutMinutes { get; set; }
}

public sealed class LateEarlyOutSource : ReportSource<LateEarlyOutRow>
{
    public override string ReportKey => "late-early-out";
    public override string Title => "Late and Early-Out";
    public override ReportModule Module => ReportModule.Time;
    public override App App => App.Hrms;
    public override string RequiredPermission => "attendance.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<LateEarlyOutRow, DateOnly>("attendanceDate", ColumnDataType.Date, r => r.AttendanceDate),
        ReportColumn.Of<LateEarlyOutRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<LateEarlyOutRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<LateEarlyOutRow, string>("department", ColumnDataType.Text, r => r.Department, groupable: true),
        ReportColumn.Of<LateEarlyOutRow, int>("lateMinutes", ColumnDataType.Quantity, r => r.LateMinutes, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<LateEarlyOutRow, int>("earlyOutMinutes", ColumnDataType.Quantity, r => r.EarlyOutMinutes, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<LateEarlyOutRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<LateEarlyOutRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from a in db.DailyAttendances
               where a.LateMinutes > 0 || a.EarlyOutMinutes > 0
               join e in db.Employees on a.EmployeeId equals e.EmployeeId
               join d in db.Departments on e.DepartmentId equals d.DepartmentId into gd from d in gd.DefaultIfEmpty()
               select new LateEarlyOutRow
               {
                   Id = a.DailyAttendanceId,
                   AttendanceDate = a.AttendanceDate,
                   EmployeeCode = e.EmployeeCode,
                   EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                   Department = d != null ? d.Name : "Unassigned",
                   LateMinutes = a.LateMinutes,
                   EarlyOutMinutes = a.EarlyOutMinutes,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<LateEarlyOutRow, DateOnly>>)(r => r.AttendanceDate);
}

// 10. Overtime Summary
public class OvertimeSummaryRow
{
    public long Id { get; set; }
    public DateOnly AttendanceDate { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string Department { get; set; } = null!;
    public int Minutes { get; set; }
    public decimal OvertimeRate { get; set; }
    public string ApprovalStatus { get; set; } = null!;
}

public sealed class OvertimeSummarySource : ReportSource<OvertimeSummaryRow>
{
    public override string ReportKey => "overtime-summary";
    public override string Title => "Overtime Summary";
    public override ReportModule Module => ReportModule.Time;
    public override App App => App.Hrms;
    public override string RequiredPermission => "attendance.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<OvertimeSummaryRow, DateOnly>("attendanceDate", ColumnDataType.Date, r => r.AttendanceDate),
        ReportColumn.Of<OvertimeSummaryRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<OvertimeSummaryRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<OvertimeSummaryRow, string>("department", ColumnDataType.Text, r => r.Department, groupable: true),
        ReportColumn.Of<OvertimeSummaryRow, int>("minutes", ColumnDataType.Quantity, r => r.Minutes, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<OvertimeSummaryRow, decimal>("overtimeRate", ColumnDataType.Quantity, r => r.OvertimeRate),
        ReportColumn.Of<OvertimeSummaryRow, string>("approvalStatus", ColumnDataType.Text, r => r.ApprovalStatus, groupable: true),
        ReportColumn.Of<OvertimeSummaryRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<OvertimeSummaryRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from o in db.OvertimeRequests
               join e in db.Employees on o.EmployeeId equals e.EmployeeId
               join d in db.Departments on e.DepartmentId equals d.DepartmentId into gd from d in gd.DefaultIfEmpty()
               select new OvertimeSummaryRow
               {
                   Id = o.OvertimeRequestId,
                   AttendanceDate = o.AttendanceDate,
                   EmployeeCode = e.EmployeeCode,
                   EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                   Department = d != null ? d.Name : "Unassigned",
                   Minutes = o.Minutes,
                   OvertimeRate = o.OvertimeRate,
                   ApprovalStatus = o.ApprovalStatus,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<OvertimeSummaryRow, DateOnly>>)(r => r.AttendanceDate);
}

// 11. Attendance Regularisations
public class AttendanceRegularisationsRow
{
    public long Id { get; set; }
    public DateOnly AttendanceDate { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string Department { get; set; } = null!;
    public string RequestedStatus { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public string ApprovalStatus { get; set; } = null!;
}

public sealed class AttendanceRegularisationsSource : ReportSource<AttendanceRegularisationsRow>
{
    public override string ReportKey => "attendance-regularisations";
    public override string Title => "Attendance Regularisations";
    public override ReportModule Module => ReportModule.Time;
    public override App App => App.Hrms;
    public override string RequiredPermission => "attendance.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<AttendanceRegularisationsRow, DateOnly>("attendanceDate", ColumnDataType.Date, r => r.AttendanceDate),
        ReportColumn.Of<AttendanceRegularisationsRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<AttendanceRegularisationsRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<AttendanceRegularisationsRow, string>("department", ColumnDataType.Text, r => r.Department, groupable: true),
        ReportColumn.Of<AttendanceRegularisationsRow, string>("requestedStatus", ColumnDataType.Text, r => r.RequestedStatus, groupable: true),
        ReportColumn.Of<AttendanceRegularisationsRow, string>("reason", ColumnDataType.Text, r => r.Reason),
        ReportColumn.Of<AttendanceRegularisationsRow, string>("approvalStatus", ColumnDataType.Text, r => r.ApprovalStatus, groupable: true),
        ReportColumn.Of<AttendanceRegularisationsRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<AttendanceRegularisationsRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from r in db.RegularisationRequests
               join e in db.Employees on r.EmployeeId equals e.EmployeeId
               join d in db.Departments on e.DepartmentId equals d.DepartmentId into gd from d in gd.DefaultIfEmpty()
               select new AttendanceRegularisationsRow
               {
                   Id = r.RegularisationRequestId,
                   AttendanceDate = r.AttendanceDate,
                   EmployeeCode = e.EmployeeCode,
                   EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                   Department = d != null ? d.Name : "Unassigned",
                   RequestedStatus = r.RequestedStatus,
                   Reason = r.Reason,
                   ApprovalStatus = r.ApprovalStatus,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<AttendanceRegularisationsRow, DateOnly>>)(r => r.AttendanceDate);
}
