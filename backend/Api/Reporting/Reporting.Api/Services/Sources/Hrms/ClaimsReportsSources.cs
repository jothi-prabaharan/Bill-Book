using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Apps;

namespace Reporting.Api.Services.Sources.Hrms;

// 35. Claims by Category
public class ClaimsByCategoryRow
{
    public string CategoryCode { get; set; } = null!;
    public string CategoryName { get; set; } = null!;
    public int ClaimCount { get; set; }
    public decimal TotalAmount { get; set; }
}

public sealed class ClaimsByCategorySource : ReportSource<ClaimsByCategoryRow>
{
    public override string ReportKey => "claims-by-category";
    public override string Title => "Claims by Category";
    public override ReportModule Module => ReportModule.Claims;
    public override App App => App.Hrms;
    public override string RequiredPermission => "claims.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<ClaimsByCategoryRow, string>("categoryCode", ColumnDataType.Text, r => r.CategoryCode, groupable: true),
        ReportColumn.Of<ClaimsByCategoryRow, string>("categoryName", ColumnDataType.Text, r => r.CategoryName, groupable: true),
        ReportColumn.Of<ClaimsByCategoryRow, int>("claimCount", ColumnDataType.Quantity, r => r.ClaimCount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ClaimsByCategoryRow, decimal>("totalAmount", ColumnDataType.Money, r => r.TotalAmount, aggregate: AggregateFunction.Sum),
    ];

    protected override IQueryable<ClaimsByCategoryRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from l in db.ExpenseClaimLines
               join c in db.ClaimCategories on l.ClaimCategoryId equals c.ClaimCategoryId
               group l by new
               {
                   c.Code,
                   c.Name,
               } into grp
               select new ClaimsByCategoryRow
               {
                   CategoryCode = grp.Key.Code,
                   CategoryName = grp.Key.Name,
                   ClaimCount = grp.Count(),
                   TotalAmount = grp.Sum(x => x.Amount),
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<ClaimsByCategoryRow, string>>)(r => r.CategoryCode);
}

// 36. Claims by Employee
public class ClaimsByEmployeeRow
{
    public long Id { get; set; }
    public string ClaimNo { get; set; } = null!;
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string Department { get; set; } = null!;
    public DateOnly ClaimDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal ApprovedAmount { get; set; }
    public string ClaimStatus { get; set; } = null!;
    public string PayoutMode { get; set; } = null!;
}

public sealed class ClaimsByEmployeeSource : ReportSource<ClaimsByEmployeeRow>
{
    public override string ReportKey => "claims-by-employee";
    public override string Title => "Claims by Employee";
    public override ReportModule Module => ReportModule.Claims;
    public override App App => App.Hrms;
    public override string RequiredPermission => "claims.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<ClaimsByEmployeeRow, string>("claimNo", ColumnDataType.Text, r => r.ClaimNo),
        ReportColumn.Of<ClaimsByEmployeeRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<ClaimsByEmployeeRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<ClaimsByEmployeeRow, string>("department", ColumnDataType.Text, r => r.Department, groupable: true),
        ReportColumn.Of<ClaimsByEmployeeRow, DateOnly>("claimDate", ColumnDataType.Date, r => r.ClaimDate),
        ReportColumn.Of<ClaimsByEmployeeRow, decimal>("totalAmount", ColumnDataType.Money, r => r.TotalAmount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ClaimsByEmployeeRow, decimal>("approvedAmount", ColumnDataType.Money, r => r.ApprovedAmount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ClaimsByEmployeeRow, string>("claimStatus", ColumnDataType.Text, r => r.ClaimStatus, groupable: true),
        ReportColumn.Of<ClaimsByEmployeeRow, string>("payoutMode", ColumnDataType.Text, r => r.PayoutMode, groupable: true),
        ReportColumn.Of<ClaimsByEmployeeRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<ClaimsByEmployeeRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from c in db.ExpenseClaims
               join e in db.Employees on c.EmployeeId equals e.EmployeeId
               join d in db.Departments on e.DepartmentId equals d.DepartmentId into gd from d in gd.DefaultIfEmpty()
               select new ClaimsByEmployeeRow
               {
                   Id = c.ExpenseClaimId,
                   ClaimNo = c.ClaimNo,
                   EmployeeCode = e.EmployeeCode,
                   EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                   Department = d != null ? d.Name : "Unassigned",
                   ClaimDate = c.ClaimDate,
                   TotalAmount = c.TotalAmount,
                   ApprovedAmount = c.ApprovedAmount,
                   ClaimStatus = c.ClaimStatus,
                   PayoutMode = c.PayoutMode,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<ClaimsByEmployeeRow, DateOnly>>)(r => r.ClaimDate);
}

// 37. Claims Pending Approval
public class ClaimsPendingApprovalRow
{
    public long Id { get; set; }
    public string ClaimNo { get; set; } = null!;
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string Department { get; set; } = null!;
    public DateOnly ClaimDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string CurrentStepLabel { get; set; } = null!;
    public string ApprovalStatus { get; set; } = null!;
}

public sealed class ClaimsPendingApprovalSource : ReportSource<ClaimsPendingApprovalRow>
{
    public override string ReportKey => "claims-pending-approval";
    public override string Title => "Claims Pending Approval";
    public override ReportModule Module => ReportModule.Claims;
    public override App App => App.Hrms;
    public override string RequiredPermission => "claims.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<ClaimsPendingApprovalRow, string>("claimNo", ColumnDataType.Text, r => r.ClaimNo),
        ReportColumn.Of<ClaimsPendingApprovalRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<ClaimsPendingApprovalRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<ClaimsPendingApprovalRow, string>("department", ColumnDataType.Text, r => r.Department, groupable: true),
        ReportColumn.Of<ClaimsPendingApprovalRow, DateOnly>("claimDate", ColumnDataType.Date, r => r.ClaimDate),
        ReportColumn.Of<ClaimsPendingApprovalRow, decimal>("totalAmount", ColumnDataType.Money, r => r.TotalAmount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ClaimsPendingApprovalRow, string>("currentStepLabel", ColumnDataType.Text, r => r.CurrentStepLabel, groupable: true),
        ReportColumn.Of<ClaimsPendingApprovalRow, string>("approvalStatus", ColumnDataType.Text, r => r.ApprovalStatus, groupable: true),
        ReportColumn.Of<ClaimsPendingApprovalRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<ClaimsPendingApprovalRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from c in db.ExpenseClaims
               where c.ApprovalStatus == "Pending" || c.ApprovalStatus == "Submitted"
               join e in db.Employees on c.EmployeeId equals e.EmployeeId
               join d in db.Departments on e.DepartmentId equals d.DepartmentId into gd from d in gd.DefaultIfEmpty()
               select new ClaimsPendingApprovalRow
               {
                   Id = c.ExpenseClaimId,
                   ClaimNo = c.ClaimNo,
                   EmployeeCode = e.EmployeeCode,
                   EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                   Department = d != null ? d.Name : "Unassigned",
                   ClaimDate = c.ClaimDate,
                   TotalAmount = c.TotalAmount,
                   CurrentStepLabel = c.CurrentStepLabel ?? "Pending Approval",
                   ApprovalStatus = c.ApprovalStatus,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<ClaimsPendingApprovalRow, DateOnly>>)(r => r.ClaimDate);
}
