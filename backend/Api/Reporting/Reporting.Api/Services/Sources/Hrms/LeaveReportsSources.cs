using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Apps;

namespace Reporting.Api.Services.Sources.Hrms;

// 12. Leave Register
public class LeaveRegisterRow
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string Department { get; set; } = null!;
    public string LeaveType { get; set; } = null!;
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public decimal Days { get; set; }
    public string Reason { get; set; } = null!;
    public string LeaveStatus { get; set; } = null!;
    public string ApprovalStatus { get; set; } = null!;
}

public sealed class LeaveRegisterSource : ReportSource<LeaveRegisterRow>
{
    public override string ReportKey => "leave-register";
    public override string Title => "Leave Register";
    public override ReportModule Module => ReportModule.Leave;
    public override App App => App.Hrms;
    public override string RequiredPermission => "leave.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<LeaveRegisterRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<LeaveRegisterRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<LeaveRegisterRow, string>("department", ColumnDataType.Text, r => r.Department, groupable: true),
        ReportColumn.Of<LeaveRegisterRow, string>("leaveType", ColumnDataType.Text, r => r.LeaveType, groupable: true),
        ReportColumn.Of<LeaveRegisterRow, DateOnly>("fromDate", ColumnDataType.Date, r => r.FromDate),
        ReportColumn.Of<LeaveRegisterRow, DateOnly>("toDate", ColumnDataType.Date, r => r.ToDate),
        ReportColumn.Of<LeaveRegisterRow, decimal>("days", ColumnDataType.Quantity, r => r.Days, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<LeaveRegisterRow, string>("reason", ColumnDataType.Text, r => r.Reason),
        ReportColumn.Of<LeaveRegisterRow, string>("leaveStatus", ColumnDataType.Text, r => r.LeaveStatus, groupable: true),
        ReportColumn.Of<LeaveRegisterRow, string>("approvalStatus", ColumnDataType.Text, r => r.ApprovalStatus, groupable: true),
        ReportColumn.Of<LeaveRegisterRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<LeaveRegisterRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from l in db.LeaveApplications
               join e in db.Employees on l.EmployeeId equals e.EmployeeId
               join d in db.Departments on e.DepartmentId equals d.DepartmentId into gd from d in gd.DefaultIfEmpty()
               join lt in db.LeaveTypes on l.LeaveTypeId equals lt.LeaveTypeId into glt from lt in glt.DefaultIfEmpty()
               select new LeaveRegisterRow
               {
                   Id = l.LeaveApplicationId,
                   EmployeeCode = e.EmployeeCode,
                   EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                   Department = d != null ? d.Name : "Unassigned",
                   LeaveType = lt != null ? lt.Name : "Unassigned",
                   FromDate = l.FromDate,
                   ToDate = l.ToDate,
                   Days = l.Days,
                   Reason = l.Reason,
                   LeaveStatus = l.LeaveStatus,
                   ApprovalStatus = l.ApprovalStatus,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<LeaveRegisterRow, DateOnly>>)(r => r.FromDate);
}

// 13. Leave Balances
public class LeaveBalancesRow
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string Department { get; set; } = null!;
    public string LeaveType { get; set; } = null!;
    public int LeaveYear { get; set; }
    public decimal Opening { get; set; }
    public decimal Accrued { get; set; }
    public decimal Taken { get; set; }
    public decimal Encashed { get; set; }
    public decimal Closing { get; set; }
}

public sealed class LeaveBalancesSource : ReportSource<LeaveBalancesRow>
{
    public override string ReportKey => "leave-balances";
    public override string Title => "Leave Balances";
    public override ReportModule Module => ReportModule.Leave;
    public override App App => App.Hrms;
    public override string RequiredPermission => "leave.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<LeaveBalancesRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<LeaveBalancesRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<LeaveBalancesRow, string>("department", ColumnDataType.Text, r => r.Department, groupable: true),
        ReportColumn.Of<LeaveBalancesRow, string>("leaveType", ColumnDataType.Text, r => r.LeaveType, groupable: true),
        ReportColumn.Of<LeaveBalancesRow, int>("leaveYear", ColumnDataType.Number, r => r.LeaveYear),
        ReportColumn.Of<LeaveBalancesRow, decimal>("opening", ColumnDataType.Quantity, r => r.Opening, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<LeaveBalancesRow, decimal>("accrued", ColumnDataType.Quantity, r => r.Accrued, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<LeaveBalancesRow, decimal>("taken", ColumnDataType.Quantity, r => r.Taken, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<LeaveBalancesRow, decimal>("encashed", ColumnDataType.Quantity, r => r.Encashed, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<LeaveBalancesRow, decimal>("closing", ColumnDataType.Quantity, r => r.Closing, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<LeaveBalancesRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<LeaveBalancesRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from b in db.LeaveBalances
               join e in db.Employees on b.EmployeeId equals e.EmployeeId
               join d in db.Departments on e.DepartmentId equals d.DepartmentId into gd from d in gd.DefaultIfEmpty()
               join lt in db.LeaveTypes on b.LeaveTypeId equals lt.LeaveTypeId into glt from lt in glt.DefaultIfEmpty()
               select new LeaveBalancesRow
               {
                   Id = b.LeaveBalanceId,
                   EmployeeCode = e.EmployeeCode,
                   EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                   Department = d != null ? d.Name : "Unassigned",
                   LeaveType = lt != null ? lt.Name : "Unassigned",
                   LeaveYear = b.LeaveYear,
                   Opening = b.Opening,
                   Accrued = b.Accrued,
                   Taken = b.Taken,
                   Encashed = b.Encashed,
                   Closing = b.Opening + b.Accrued - b.Taken - b.Encashed - b.Lapsed + b.Adjusted,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<LeaveBalancesRow, string>>)(r => r.EmployeeCode);
}

// 14. Leave Encashment
public class LeaveEncashmentRow
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string Department { get; set; } = null!;
    public string LeaveType { get; set; } = null!;
    public int LeaveYear { get; set; }
    public decimal Days { get; set; }
    public string EncashmentStatus { get; set; } = null!;
    public string ApprovalStatus { get; set; } = null!;
}

public sealed class LeaveEncashmentSource : ReportSource<LeaveEncashmentRow>
{
    public override string ReportKey => "leave-encashment";
    public override string Title => "Leave Encashment";
    public override ReportModule Module => ReportModule.Leave;
    public override App App => App.Hrms;
    public override string RequiredPermission => "leave.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<LeaveEncashmentRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<LeaveEncashmentRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<LeaveEncashmentRow, string>("department", ColumnDataType.Text, r => r.Department, groupable: true),
        ReportColumn.Of<LeaveEncashmentRow, string>("leaveType", ColumnDataType.Text, r => r.LeaveType, groupable: true),
        ReportColumn.Of<LeaveEncashmentRow, int>("leaveYear", ColumnDataType.Number, r => r.LeaveYear),
        ReportColumn.Of<LeaveEncashmentRow, decimal>("days", ColumnDataType.Quantity, r => r.Days, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<LeaveEncashmentRow, string>("encashmentStatus", ColumnDataType.Text, r => r.EncashmentStatus, groupable: true),
        ReportColumn.Of<LeaveEncashmentRow, string>("approvalStatus", ColumnDataType.Text, r => r.ApprovalStatus, groupable: true),
        ReportColumn.Of<LeaveEncashmentRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<LeaveEncashmentRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from e in db.LeaveEncashments
               join emp in db.Employees on e.EmployeeId equals emp.EmployeeId
               join d in db.Departments on emp.DepartmentId equals d.DepartmentId into gd from d in gd.DefaultIfEmpty()
               join lt in db.LeaveTypes on e.LeaveTypeId equals lt.LeaveTypeId into glt from lt in glt.DefaultIfEmpty()
               select new LeaveEncashmentRow
               {
                   Id = e.LeaveEncashmentId,
                   EmployeeCode = emp.EmployeeCode,
                   EmployeeName = emp.FirstName + (emp.LastName != null ? " " + emp.LastName : ""),
                   Department = d != null ? d.Name : "Unassigned",
                   LeaveType = lt != null ? lt.Name : "Unassigned",
                   LeaveYear = e.LeaveYear,
                   Days = e.Days,
                   EncashmentStatus = e.EncashmentStatus,
                   ApprovalStatus = e.ApprovalStatus,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<LeaveEncashmentRow, string>>)(r => r.EmployeeCode);
}

// 15. Team Availability
public class TeamAvailabilityRow
{
    public long Id { get; set; }
    public string Department { get; set; } = null!;
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string LeaveType { get; set; } = null!;
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public decimal Days { get; set; }
    public string ApprovalStatus { get; set; } = null!;
}

public sealed class TeamAvailabilitySource : ReportSource<TeamAvailabilityRow>
{
    public override string ReportKey => "team-availability";
    public override string Title => "Team Availability";
    public override ReportModule Module => ReportModule.Leave;
    public override App App => App.Hrms;
    public override string RequiredPermission => "leave.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<TeamAvailabilityRow, string>("department", ColumnDataType.Text, r => r.Department, groupable: true),
        ReportColumn.Of<TeamAvailabilityRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<TeamAvailabilityRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<TeamAvailabilityRow, string>("leaveType", ColumnDataType.Text, r => r.LeaveType, groupable: true),
        ReportColumn.Of<TeamAvailabilityRow, DateOnly>("fromDate", ColumnDataType.Date, r => r.FromDate),
        ReportColumn.Of<TeamAvailabilityRow, DateOnly>("toDate", ColumnDataType.Date, r => r.ToDate),
        ReportColumn.Of<TeamAvailabilityRow, decimal>("days", ColumnDataType.Quantity, r => r.Days, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<TeamAvailabilityRow, string>("approvalStatus", ColumnDataType.Text, r => r.ApprovalStatus, groupable: true),
        ReportColumn.Of<TeamAvailabilityRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<TeamAvailabilityRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from l in db.LeaveApplications
               join e in db.Employees on l.EmployeeId equals e.EmployeeId
               join d in db.Departments on e.DepartmentId equals d.DepartmentId into gd from d in gd.DefaultIfEmpty()
               join lt in db.LeaveTypes on l.LeaveTypeId equals lt.LeaveTypeId into glt from lt in glt.DefaultIfEmpty()
               select new TeamAvailabilityRow
               {
                   Id = l.LeaveApplicationId,
                   Department = d != null ? d.Name : "Unassigned",
                   EmployeeCode = e.EmployeeCode,
                   EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                   LeaveType = lt != null ? lt.Name : "Unassigned",
                   FromDate = l.FromDate,
                   ToDate = l.ToDate,
                   Days = l.Days,
                   ApprovalStatus = l.ApprovalStatus,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<TeamAvailabilityRow, DateOnly>>)(r => r.FromDate);
}
