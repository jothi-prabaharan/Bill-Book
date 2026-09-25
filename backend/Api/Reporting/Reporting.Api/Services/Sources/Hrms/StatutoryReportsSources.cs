using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Apps;

namespace Reporting.Api.Services.Sources.Hrms;

// 24. PF Statement
public class PfStatementRow
{
    public long Id { get; set; }
    public DateOnly Month { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string? Uan { get; set; }
    public decimal GrossEarnings { get; set; }
    public decimal EmployeePf { get; set; }
    public decimal EmployerEpf { get; set; }
    public decimal EmployerEps { get; set; }
}

public sealed class PfStatementSource : ReportSource<PfStatementRow>
{
    public override string ReportKey => "pf-statement";
    public override string Title => "PF Statement";
    public override ReportModule Module => ReportModule.Statutory;
    public override App App => App.Payroll;
    public override string RequiredPermission => "payroll.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<PfStatementRow, DateOnly>("month", ColumnDataType.Date, r => r.Month),
        ReportColumn.Of<PfStatementRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<PfStatementRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<PfStatementRow, string?>("uan", ColumnDataType.Text, r => r.Uan),
        ReportColumn.Of<PfStatementRow, decimal>("grossEarnings", ColumnDataType.Money, r => r.GrossEarnings, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<PfStatementRow, decimal>("employeePf", ColumnDataType.Money, r => r.EmployeePf, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<PfStatementRow, decimal>("employerEpf", ColumnDataType.Money, r => r.EmployerEpf, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<PfStatementRow, decimal>("employerEps", ColumnDataType.Money, r => r.EmployerEps, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<PfStatementRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<PfStatementRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from p in db.Payslips
               join r in db.PayrollRuns on p.PayrollRunId equals r.PayrollRunId
               join e in db.Employees on p.EmployeeId equals e.EmployeeId
               select new PfStatementRow
               {
                   Id = p.PayslipId,
                   Month = r.Month,
                   EmployeeCode = e.EmployeeCode,
                   EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                   Uan = e.Uan,
                   GrossEarnings = p.GrossEarnings,
                   EmployeePf = Math.Round(p.GrossEarnings * 0.12m, 2),
                   EmployerEpf = Math.Round(p.GrossEarnings * 0.0367m, 2),
                   EmployerEps = Math.Round(p.GrossEarnings * 0.0833m, 2),
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<PfStatementRow, DateOnly>>)(r => r.Month);
}

// 25. ESI Statement
public class EsiStatementRow
{
    public long Id { get; set; }
    public DateOnly Month { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public decimal GrossEarnings { get; set; }
    public decimal EmployeeEsi { get; set; }
    public decimal EmployerEsi { get; set; }
    public decimal TotalEsi { get; set; }
}

public sealed class EsiStatementSource : ReportSource<EsiStatementRow>
{
    public override string ReportKey => "esi-statement";
    public override string Title => "ESI Statement";
    public override ReportModule Module => ReportModule.Statutory;
    public override App App => App.Payroll;
    public override string RequiredPermission => "payroll.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<EsiStatementRow, DateOnly>("month", ColumnDataType.Date, r => r.Month),
        ReportColumn.Of<EsiStatementRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<EsiStatementRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<EsiStatementRow, decimal>("grossEarnings", ColumnDataType.Money, r => r.GrossEarnings, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<EsiStatementRow, decimal>("employeeEsi", ColumnDataType.Money, r => r.EmployeeEsi, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<EsiStatementRow, decimal>("employerEsi", ColumnDataType.Money, r => r.EmployerEsi, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<EsiStatementRow, decimal>("totalEsi", ColumnDataType.Money, r => r.TotalEsi, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<EsiStatementRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<EsiStatementRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from p in db.Payslips
               join r in db.PayrollRuns on p.PayrollRunId equals r.PayrollRunId
               join e in db.Employees on p.EmployeeId equals e.EmployeeId
               select new EsiStatementRow
               {
                   Id = p.PayslipId,
                   Month = r.Month,
                   EmployeeCode = e.EmployeeCode,
                   EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                   GrossEarnings = p.GrossEarnings,
                   EmployeeEsi = Math.Round(p.GrossEarnings * 0.0075m, 2),
                   EmployerEsi = Math.Round(p.GrossEarnings * 0.0325m, 2),
                   TotalEsi = Math.Round(p.GrossEarnings * 0.04m, 2),
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<EsiStatementRow, DateOnly>>)(r => r.Month);
}

// 26. PT Statement
public class PtStatementRow
{
    public long Id { get; set; }
    public DateOnly Month { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string WorkLocation { get; set; } = null!;
    public decimal GrossEarnings { get; set; }
    public decimal PtAmount { get; set; }
}

public sealed class PtStatementSource : ReportSource<PtStatementRow>
{
    public override string ReportKey => "pt-statement";
    public override string Title => "Professional Tax Statement";
    public override ReportModule Module => ReportModule.Statutory;
    public override App App => App.Payroll;
    public override string RequiredPermission => "payroll.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<PtStatementRow, DateOnly>("month", ColumnDataType.Date, r => r.Month),
        ReportColumn.Of<PtStatementRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<PtStatementRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<PtStatementRow, string>("workLocation", ColumnDataType.Text, r => r.WorkLocation, groupable: true),
        ReportColumn.Of<PtStatementRow, decimal>("grossEarnings", ColumnDataType.Money, r => r.GrossEarnings, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<PtStatementRow, decimal>("ptAmount", ColumnDataType.Money, r => r.PtAmount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<PtStatementRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<PtStatementRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from p in db.Payslips
               join r in db.PayrollRuns on p.PayrollRunId equals r.PayrollRunId
               join e in db.Employees on p.EmployeeId equals e.EmployeeId
               join w in db.WorkLocations on e.WorkLocationId equals w.WorkLocationId into gw from w in gw.DefaultIfEmpty()
               select new PtStatementRow
               {
                   Id = p.PayslipId,
                   Month = r.Month,
                   EmployeeCode = e.EmployeeCode,
                   EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                   WorkLocation = w != null ? w.Name : "Unassigned",
                   GrossEarnings = p.GrossEarnings,
                   PtAmount = 200.00m,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<PtStatementRow, DateOnly>>)(r => r.Month);
}

// 27. LWF Statement
public class LwfStatementRow
{
    public long Id { get; set; }
    public DateOnly Month { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string WorkLocation { get; set; } = null!;
    public decimal EmployeeContribution { get; set; }
    public decimal EmployerContribution { get; set; }
    public decimal TotalLwf { get; set; }
}

public sealed class LwfStatementSource : ReportSource<LwfStatementRow>
{
    public override string ReportKey => "lwf-statement";
    public override string Title => "LWF Statement";
    public override ReportModule Module => ReportModule.Statutory;
    public override App App => App.Payroll;
    public override string RequiredPermission => "payroll.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<LwfStatementRow, DateOnly>("month", ColumnDataType.Date, r => r.Month),
        ReportColumn.Of<LwfStatementRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<LwfStatementRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<LwfStatementRow, string>("workLocation", ColumnDataType.Text, r => r.WorkLocation, groupable: true),
        ReportColumn.Of<LwfStatementRow, decimal>("employeeContribution", ColumnDataType.Money, r => r.EmployeeContribution, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<LwfStatementRow, decimal>("employerContribution", ColumnDataType.Money, r => r.EmployerContribution, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<LwfStatementRow, decimal>("totalLwf", ColumnDataType.Money, r => r.TotalLwf, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<LwfStatementRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<LwfStatementRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from p in db.Payslips
               join r in db.PayrollRuns on p.PayrollRunId equals r.PayrollRunId
               join e in db.Employees on p.EmployeeId equals e.EmployeeId
               join w in db.WorkLocations on e.WorkLocationId equals w.WorkLocationId into gw from w in gw.DefaultIfEmpty()
               select new LwfStatementRow
               {
                   Id = p.PayslipId,
                   Month = r.Month,
                   EmployeeCode = e.EmployeeCode,
                   EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                   WorkLocation = w != null ? w.Name : "Unassigned",
                   EmployeeContribution = 10.00m,
                   EmployerContribution = 20.00m,
                   TotalLwf = 30.00m,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<LwfStatementRow, DateOnly>>)(r => r.Month);
}

// 28. Gratuity Provision
public class GratuityProvisionRow
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string Department { get; set; } = null!;
    public DateOnly JoiningDate { get; set; }
    public decimal MonthlySalary { get; set; }
    public decimal GratuityProvision { get; set; }
}

public sealed class GratuityProvisionSource : ReportSource<GratuityProvisionRow>
{
    public override string ReportKey => "gratuity-provision";
    public override string Title => "Gratuity Provision";
    public override ReportModule Module => ReportModule.Statutory;
    public override App App => App.Payroll;
    public override string RequiredPermission => "payroll.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<GratuityProvisionRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<GratuityProvisionRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<GratuityProvisionRow, string>("department", ColumnDataType.Text, r => r.Department, groupable: true),
        ReportColumn.Of<GratuityProvisionRow, DateOnly>("joiningDate", ColumnDataType.Date, r => r.JoiningDate),
        ReportColumn.Of<GratuityProvisionRow, decimal>("monthlySalary", ColumnDataType.Money, r => r.MonthlySalary, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<GratuityProvisionRow, decimal>("gratuityProvision", ColumnDataType.Money, r => r.GratuityProvision, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<GratuityProvisionRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<GratuityProvisionRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from s in db.EmployeeSalaries
               join e in db.Employees on s.EmployeeId equals e.EmployeeId
               join d in db.Departments on e.DepartmentId equals d.DepartmentId into gd from d in gd.DefaultIfEmpty()
               select new GratuityProvisionRow
               {
                   Id = s.EmployeeSalaryId,
                   EmployeeCode = e.EmployeeCode,
                   EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                   Department = d != null ? d.Name : "Unassigned",
                   JoiningDate = e.JoiningDate,
                   MonthlySalary = Math.Round(s.AnnualCtc / 12.0m, 2),
                   GratuityProvision = Math.Round((s.AnnualCtc / 12.0m) * 0.0481m, 2),
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<GratuityProvisionRow, string>>)(r => r.EmployeeCode);
}

// 29. Bonus Register
public class BonusRegisterRow
{
    public long Id { get; set; }
    public DateOnly PaymentMonth { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string Department { get; set; } = null!;
    public decimal BonusAmount { get; set; }
    public string? Remarks { get; set; }
}

public sealed class BonusRegisterSource : ReportSource<BonusRegisterRow>
{
    public override string ReportKey => "bonus-register";
    public override string Title => "Bonus Register";
    public override ReportModule Module => ReportModule.Statutory;
    public override App App => App.Payroll;
    public override string RequiredPermission => "payroll.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<BonusRegisterRow, DateOnly>("paymentMonth", ColumnDataType.Date, r => r.PaymentMonth),
        ReportColumn.Of<BonusRegisterRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<BonusRegisterRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<BonusRegisterRow, string>("department", ColumnDataType.Text, r => r.Department, groupable: true),
        ReportColumn.Of<BonusRegisterRow, decimal>("bonusAmount", ColumnDataType.Money, r => r.BonusAmount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<BonusRegisterRow, string?>("remarks", ColumnDataType.Text, r => r.Remarks),
        ReportColumn.Of<BonusRegisterRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<BonusRegisterRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from o in db.OneTimePayments
               join e in db.Employees on o.EmployeeId equals e.EmployeeId
               join d in db.Departments on e.DepartmentId equals d.DepartmentId into gd from d in gd.DefaultIfEmpty()
               join c in db.SalaryComponents on o.SalaryComponentId equals c.SalaryComponentId into gc from c in gc.DefaultIfEmpty()
               where c != null && (c.Name.Contains("Bonus") || c.Code.Contains("BONUS"))
               select new BonusRegisterRow
               {
                   Id = o.OneTimePaymentId,
                   PaymentMonth = o.PaymentMonth,
                   EmployeeCode = e.EmployeeCode,
                   EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                   Department = d != null ? d.Name : "Unassigned",
                   BonusAmount = o.Amount,
                   Remarks = o.Remarks,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<BonusRegisterRow, DateOnly>>)(r => r.PaymentMonth);
}

// 30. TDS Summary
public class TdsSummaryRow
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string? Pan { get; set; }
    public string FinancialYear { get; set; } = null!;
    public string Regime { get; set; } = null!;
    public bool IsLocked { get; set; }
}

public sealed class TdsSummarySource : ReportSource<TdsSummaryRow>
{
    public override string ReportKey => "tds-summary";
    public override string Title => "TDS Summary and Projection";
    public override ReportModule Module => ReportModule.Statutory;
    public override App App => App.Payroll;
    public override string RequiredPermission => "payroll.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<TdsSummaryRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<TdsSummaryRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<TdsSummaryRow, string?>("pan", ColumnDataType.Text, r => r.Pan),
        ReportColumn.Of<TdsSummaryRow, string>("financialYear", ColumnDataType.Text, r => r.FinancialYear, groupable: true),
        ReportColumn.Of<TdsSummaryRow, string>("regime", ColumnDataType.Text, r => r.Regime, groupable: true),
        ReportColumn.Of<TdsSummaryRow, bool>("isLocked", ColumnDataType.Boolean, r => r.IsLocked),
        ReportColumn.Of<TdsSummaryRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<TdsSummaryRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from t in db.TaxDeclarations
               join e in db.Employees on t.EmployeeId equals e.EmployeeId
               select new TdsSummaryRow
               {
                   Id = t.TaxDeclarationId,
                   EmployeeCode = e.EmployeeCode,
                   EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                   Pan = e.Pan,
                   FinancialYear = t.FinancialYear,
                   Regime = t.Regime,
                   IsLocked = t.IsLocked,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<TdsSummaryRow, string>>)(r => r.EmployeeCode);
}
