using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Apps;

namespace Reporting.Api.Services.Sources.Hrms;

// 1. Headcount Summary
public class HeadcountSummaryRow
{
    public string Department { get; set; } = null!;
    public string Designation { get; set; } = null!;
    public string Grade { get; set; } = null!;
    public string WorkLocation { get; set; } = null!;
    public string EmploymentType { get; set; } = null!;
    public string Gender { get; set; } = null!;
    public string Status { get; set; } = null!;
    public int Headcount { get; set; }
}

public sealed class HeadcountSummarySource : ReportSource<HeadcountSummaryRow>
{
    public override string ReportKey => "headcount-summary";
    public override string Title => "Headcount Summary";
    public override ReportModule Module => ReportModule.People;
    public override App App => App.Hrms;
    public override string RequiredPermission => "employee.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<HeadcountSummaryRow, string>("department", ColumnDataType.Text, r => r.Department, groupable: true),
        ReportColumn.Of<HeadcountSummaryRow, string>("designation", ColumnDataType.Text, r => r.Designation, groupable: true),
        ReportColumn.Of<HeadcountSummaryRow, string>("grade", ColumnDataType.Text, r => r.Grade, groupable: true),
        ReportColumn.Of<HeadcountSummaryRow, string>("workLocation", ColumnDataType.Text, r => r.WorkLocation, groupable: true),
        ReportColumn.Of<HeadcountSummaryRow, string>("employmentType", ColumnDataType.Text, r => r.EmploymentType, groupable: true),
        ReportColumn.Of<HeadcountSummaryRow, string>("gender", ColumnDataType.Text, r => r.Gender, groupable: true),
        ReportColumn.Of<HeadcountSummaryRow, string>("status", ColumnDataType.Text, r => r.Status, groupable: true),
        ReportColumn.Of<HeadcountSummaryRow, int>("headcount", ColumnDataType.Quantity, r => r.Headcount, aggregate: AggregateFunction.Sum),
    ];

    protected override IQueryable<HeadcountSummaryRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from e in db.Employees
               join d in db.Departments on e.DepartmentId equals d.DepartmentId into gd from d in gd.DefaultIfEmpty()
               join des in db.Designations on e.DesignationId equals des.DesignationId into gdes from des in gdes.DefaultIfEmpty()
               join g in db.Grades on e.GradeId equals g.GradeId into gg from g in gg.DefaultIfEmpty()
               join w in db.WorkLocations on e.WorkLocationId equals w.WorkLocationId into gw from w in gw.DefaultIfEmpty()
               group e by new
               {
                   Department = d != null ? d.Name : "Unassigned",
                   Designation = des != null ? des.Name : "Unassigned",
                   Grade = g != null ? g.Name : "Unassigned",
                   WorkLocation = w != null ? w.Name : "Unassigned",
                   e.EmploymentType,
                   e.Gender,
                   e.EmployeeStatus,
               } into grp
               select new HeadcountSummaryRow
               {
                   Department = grp.Key.Department,
                   Designation = grp.Key.Designation,
                   Grade = grp.Key.Grade,
                   WorkLocation = grp.Key.WorkLocation,
                   EmploymentType = grp.Key.EmploymentType,
                   Gender = grp.Key.Gender,
                   Status = grp.Key.EmployeeStatus,
                   Headcount = grp.Count(),
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<HeadcountSummaryRow, string>>)(r => r.Department);
}

// 2. Joiners and Leavers
public class JoinersLeaversRow
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string Department { get; set; } = null!;
    public string Designation { get; set; } = null!;
    public string EventType { get; set; } = null!;
    public DateOnly EventDate { get; set; }
    public string? Reason { get; set; }
}

public sealed class JoinersLeaversSource : ReportSource<JoinersLeaversRow>
{
    public override string ReportKey => "joiners-leavers";
    public override string Title => "Joiners and Leavers";
    public override ReportModule Module => ReportModule.People;
    public override App App => App.Hrms;
    public override string RequiredPermission => "employee.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<JoinersLeaversRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<JoinersLeaversRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<JoinersLeaversRow, string>("department", ColumnDataType.Text, r => r.Department, groupable: true),
        ReportColumn.Of<JoinersLeaversRow, string>("designation", ColumnDataType.Text, r => r.Designation, groupable: true),
        ReportColumn.Of<JoinersLeaversRow, string>("eventType", ColumnDataType.Text, r => r.EventType, groupable: true),
        ReportColumn.Of<JoinersLeaversRow, DateOnly>("eventDate", ColumnDataType.Date, r => r.EventDate),
        ReportColumn.Of<JoinersLeaversRow, string?>("reason", ColumnDataType.Text, r => r.Reason),
        ReportColumn.Of<JoinersLeaversRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<JoinersLeaversRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        var joiners = from e in db.Employees
                      join d in db.Departments on e.DepartmentId equals d.DepartmentId into gd from d in gd.DefaultIfEmpty()
                      join des in db.Designations on e.DesignationId equals des.DesignationId into gdes from des in gdes.DefaultIfEmpty()
                      select new JoinersLeaversRow
                      {
                          Id = e.EmployeeId,
                          EmployeeCode = e.EmployeeCode,
                          EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                          Department = d != null ? d.Name : "Unassigned",
                          Designation = des != null ? des.Name : "Unassigned",
                          EventType = e.ExitDate.HasValue ? "Leaver" : "Joiner",
                          EventDate = e.ExitDate ?? e.JoiningDate,
                          Reason = e.ExitDate.HasValue ? "Exit" : "New Hire",
                      };

        return joiners;
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<JoinersLeaversRow, DateOnly>>)(r => r.EventDate);
}

// 3. Attrition Rate
public class AttritionRateRow
{
    public string Department { get; set; } = null!;
    public string WorkLocation { get; set; } = null!;
    public int TotalHeadcount { get; set; }
    public int ActiveCount { get; set; }
    public int ExitedCount { get; set; }
}

public sealed class AttritionRateSource : ReportSource<AttritionRateRow>
{
    public override string ReportKey => "attrition-rate";
    public override string Title => "Attrition Rate";
    public override ReportModule Module => ReportModule.People;
    public override App App => App.Hrms;
    public override string RequiredPermission => "employee.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<AttritionRateRow, string>("department", ColumnDataType.Text, r => r.Department, groupable: true),
        ReportColumn.Of<AttritionRateRow, string>("workLocation", ColumnDataType.Text, r => r.WorkLocation, groupable: true),
        ReportColumn.Of<AttritionRateRow, int>("totalHeadcount", ColumnDataType.Quantity, r => r.TotalHeadcount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<AttritionRateRow, int>("activeCount", ColumnDataType.Quantity, r => r.ActiveCount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<AttritionRateRow, int>("exitedCount", ColumnDataType.Quantity, r => r.ExitedCount, aggregate: AggregateFunction.Sum),
    ];

    protected override IQueryable<AttritionRateRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from e in db.Employees
               join d in db.Departments on e.DepartmentId equals d.DepartmentId into gd from d in gd.DefaultIfEmpty()
               join w in db.WorkLocations on e.WorkLocationId equals w.WorkLocationId into gw from w in gw.DefaultIfEmpty()
               group e by new
               {
                   Department = d != null ? d.Name : "Unassigned",
                   WorkLocation = w != null ? w.Name : "Unassigned",
               } into grp
               select new AttritionRateRow
               {
                   Department = grp.Key.Department,
                   WorkLocation = grp.Key.WorkLocation,
                   TotalHeadcount = grp.Count(),
                   ActiveCount = grp.Count(x => x.ExitDate == null),
                   ExitedCount = grp.Count(x => x.ExitDate != null),
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<AttritionRateRow, string>>)(r => r.Department);
}

// 4. Probation Due
public class ProbationDueRow
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string Department { get; set; } = null!;
    public string Designation { get; set; } = null!;
    public DateOnly JoiningDate { get; set; }
    public DateOnly? ProbationEndDate { get; set; }
    public DateOnly? ConfirmationDate { get; set; }
    public string Status { get; set; } = null!;
}

public sealed class ProbationDueSource : ReportSource<ProbationDueRow>
{
    public override string ReportKey => "probation-due";
    public override string Title => "Probation Due";
    public override ReportModule Module => ReportModule.People;
    public override App App => App.Hrms;
    public override string RequiredPermission => "employee.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<ProbationDueRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<ProbationDueRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<ProbationDueRow, string>("department", ColumnDataType.Text, r => r.Department, groupable: true),
        ReportColumn.Of<ProbationDueRow, string>("designation", ColumnDataType.Text, r => r.Designation, groupable: true),
        ReportColumn.Of<ProbationDueRow, DateOnly>("joiningDate", ColumnDataType.Date, r => r.JoiningDate),
        ReportColumn.Of<ProbationDueRow, DateOnly?>("probationEndDate", ColumnDataType.Date, r => r.ProbationEndDate),
        ReportColumn.Of<ProbationDueRow, DateOnly?>("confirmationDate", ColumnDataType.Date, r => r.ConfirmationDate),
        ReportColumn.Of<ProbationDueRow, string>("status", ColumnDataType.Text, r => r.Status, groupable: true),
        ReportColumn.Of<ProbationDueRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<ProbationDueRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from e in db.Employees
               where e.EmployeeStatus == "Probation" || (e.ProbationEndDate != null && e.ConfirmationDate == null)
               join d in db.Departments on e.DepartmentId equals d.DepartmentId into gd from d in gd.DefaultIfEmpty()
               join des in db.Designations on e.DesignationId equals des.DesignationId into gdes from des in gdes.DefaultIfEmpty()
               select new ProbationDueRow
               {
                   Id = e.EmployeeId,
                   EmployeeCode = e.EmployeeCode,
                   EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                   Department = d != null ? d.Name : "Unassigned",
                   Designation = des != null ? des.Name : "Unassigned",
                   JoiningDate = e.JoiningDate,
                   ProbationEndDate = e.ProbationEndDate,
                   ConfirmationDate = e.ConfirmationDate,
                   Status = e.EmployeeStatus,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<ProbationDueRow, DateOnly>>)(r => r.JoiningDate);
}

// 5. Birthdays and Anniversaries
public class BirthdaysAnniversariesRow
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string Department { get; set; } = null!;
    public string Designation { get; set; } = null!;
    public DateOnly DateOfBirth { get; set; }
    public DateOnly JoiningDate { get; set; }
    public string Status { get; set; } = null!;
}

public sealed class BirthdaysAnniversariesSource : ReportSource<BirthdaysAnniversariesRow>
{
    public override string ReportKey => "birthdays-anniversaries";
    public override string Title => "Birthdays and Work Anniversaries";
    public override ReportModule Module => ReportModule.People;
    public override App App => App.Hrms;
    public override string RequiredPermission => "employee.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<BirthdaysAnniversariesRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<BirthdaysAnniversariesRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<BirthdaysAnniversariesRow, string>("department", ColumnDataType.Text, r => r.Department, groupable: true),
        ReportColumn.Of<BirthdaysAnniversariesRow, string>("designation", ColumnDataType.Text, r => r.Designation, groupable: true),
        ReportColumn.Of<BirthdaysAnniversariesRow, DateOnly>("dateOfBirth", ColumnDataType.Date, r => r.DateOfBirth),
        ReportColumn.Of<BirthdaysAnniversariesRow, DateOnly>("joiningDate", ColumnDataType.Date, r => r.JoiningDate),
        ReportColumn.Of<BirthdaysAnniversariesRow, string>("status", ColumnDataType.Text, r => r.Status, groupable: true),
        ReportColumn.Of<BirthdaysAnniversariesRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<BirthdaysAnniversariesRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from e in db.Employees
               join d in db.Departments on e.DepartmentId equals d.DepartmentId into gd from d in gd.DefaultIfEmpty()
               join des in db.Designations on e.DesignationId equals des.DesignationId into gdes from des in gdes.DefaultIfEmpty()
               select new BirthdaysAnniversariesRow
               {
                   Id = e.EmployeeId,
                   EmployeeCode = e.EmployeeCode,
                   EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                   Department = d != null ? d.Name : "Unassigned",
                   Designation = des != null ? des.Name : "Unassigned",
                   DateOfBirth = e.DateOfBirth,
                   JoiningDate = e.JoiningDate,
                   Status = e.EmployeeStatus,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<BirthdaysAnniversariesRow, DateOnly>>)(r => r.JoiningDate);
}

// 6. Document Expiry
public class DocumentExpiryRow
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string Department { get; set; } = null!;
    public string DocumentKind { get; set; } = null!;
    public DateOnly? ExpiryDate { get; set; }
}

public sealed class DocumentExpirySource : ReportSource<DocumentExpiryRow>
{
    public override string ReportKey => "document-expiry";
    public override string Title => "Document Expiry";
    public override ReportModule Module => ReportModule.People;
    public override App App => App.Hrms;
    public override string RequiredPermission => "employee.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<DocumentExpiryRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<DocumentExpiryRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<DocumentExpiryRow, string>("department", ColumnDataType.Text, r => r.Department, groupable: true),
        ReportColumn.Of<DocumentExpiryRow, string>("documentKind", ColumnDataType.Text, r => r.DocumentKind, groupable: true),
        ReportColumn.Of<DocumentExpiryRow, DateOnly?>("expiryDate", ColumnDataType.Date, r => r.ExpiryDate),
        ReportColumn.Of<DocumentExpiryRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<DocumentExpiryRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from doc in db.EmployeeDocuments
               join e in db.Employees on doc.EmployeeId equals e.EmployeeId
               join d in db.Departments on e.DepartmentId equals d.DepartmentId into gd from d in gd.DefaultIfEmpty()
               select new DocumentExpiryRow
               {
                   Id = doc.EmployeeDocumentId,
                   EmployeeCode = e.EmployeeCode,
                   EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                   Department = d != null ? d.Name : "Unassigned",
                   DocumentKind = doc.DocumentKind,
                   ExpiryDate = doc.ValidUntil,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<DocumentExpiryRow, long>>)(r => r.Id);
}
