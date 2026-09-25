using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Apps;

namespace Reporting.Api.Services.Sources.Hrms;

// 16. Salary Register
public class SalaryRegisterRow
{
    public long Id { get; set; }
    public DateOnly Month { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string Department { get; set; } = null!;
    public decimal PaidDays { get; set; }
    public decimal GrossEarnings { get; set; }
    public decimal GrossDeductions { get; set; }
    public decimal NetPay { get; set; }
}

public sealed class SalaryRegisterSource : ReportSource<SalaryRegisterRow>
{
    public override string ReportKey => "salary-register";
    public override string Title => "Salary Register";
    public override ReportModule Module => ReportModule.Pay;
    public override App App => App.Payroll;
    public override string RequiredPermission => "payroll.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<SalaryRegisterRow, DateOnly>("month", ColumnDataType.Date, r => r.Month),
        ReportColumn.Of<SalaryRegisterRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<SalaryRegisterRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<SalaryRegisterRow, string>("department", ColumnDataType.Text, r => r.Department, groupable: true),
        ReportColumn.Of<SalaryRegisterRow, decimal>("paidDays", ColumnDataType.Quantity, r => r.PaidDays),
        ReportColumn.Of<SalaryRegisterRow, decimal>("grossEarnings", ColumnDataType.Money, r => r.GrossEarnings, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SalaryRegisterRow, decimal>("grossDeductions", ColumnDataType.Money, r => r.GrossDeductions, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SalaryRegisterRow, decimal>("netPay", ColumnDataType.Money, r => r.NetPay, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SalaryRegisterRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<SalaryRegisterRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from p in db.Payslips
               join r in db.PayrollRuns on p.PayrollRunId equals r.PayrollRunId
               join e in db.Employees on p.EmployeeId equals e.EmployeeId
               join d in db.Departments on e.DepartmentId equals d.DepartmentId into gd from d in gd.DefaultIfEmpty()
               select new SalaryRegisterRow
               {
                   Id = p.PayslipId,
                   Month = r.Month,
                   EmployeeCode = e.EmployeeCode,
                   EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                   Department = d != null ? d.Name : "Unassigned",
                   PaidDays = p.PaidDays,
                   GrossEarnings = p.GrossEarnings,
                   GrossDeductions = p.GrossDeductions,
                   NetPay = p.NetPay,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<SalaryRegisterRow, DateOnly>>)(r => r.Month);
}

// 17. Payslip Summary
public class PayslipSummaryRow
{
    public DateOnly Month { get; set; }
    public string Department { get; set; } = null!;
    public int EmployeeCount { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNetPay { get; set; }
}

public sealed class PayslipSummarySource : ReportSource<PayslipSummaryRow>
{
    public override string ReportKey => "payslip-summary";
    public override string Title => "Payslip Summary";
    public override ReportModule Module => ReportModule.Pay;
    public override App App => App.Payroll;
    public override string RequiredPermission => "payroll.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<PayslipSummaryRow, DateOnly>("month", ColumnDataType.Date, r => r.Month),
        ReportColumn.Of<PayslipSummaryRow, string>("department", ColumnDataType.Text, r => r.Department, groupable: true),
        ReportColumn.Of<PayslipSummaryRow, int>("employeeCount", ColumnDataType.Quantity, r => r.EmployeeCount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<PayslipSummaryRow, decimal>("totalEarnings", ColumnDataType.Money, r => r.TotalEarnings, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<PayslipSummaryRow, decimal>("totalDeductions", ColumnDataType.Money, r => r.TotalDeductions, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<PayslipSummaryRow, decimal>("totalNetPay", ColumnDataType.Money, r => r.TotalNetPay, aggregate: AggregateFunction.Sum),
    ];

    protected override IQueryable<PayslipSummaryRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from p in db.Payslips
               join r in db.PayrollRuns on p.PayrollRunId equals r.PayrollRunId
               join e in db.Employees on p.EmployeeId equals e.EmployeeId
               join d in db.Departments on e.DepartmentId equals d.DepartmentId into gd from d in gd.DefaultIfEmpty()
               group new { p, r } by new
               {
                   r.Month,
                   Department = d != null ? d.Name : "Unassigned",
               } into grp
               select new PayslipSummaryRow
               {
                   Month = grp.Key.Month,
                   Department = grp.Key.Department,
                   EmployeeCount = grp.Count(),
                   TotalEarnings = grp.Sum(x => x.p.GrossEarnings),
                   TotalDeductions = grp.Sum(x => x.p.GrossDeductions),
                   TotalNetPay = grp.Sum(x => x.p.NetPay),
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<PayslipSummaryRow, DateOnly>>)(r => r.Month);
}

// 18. CTC Report
public class CtcReportRow
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string Department { get; set; } = null!;
    public string Designation { get; set; } = null!;
    public string SalaryStructure { get; set; } = null!;
    public decimal AnnualCtc { get; set; }
    public DateOnly EffectiveFrom { get; set; }
}

public sealed class CtcReportSource : ReportSource<CtcReportRow>
{
    public override string ReportKey => "ctc-report";
    public override string Title => "CTC Report";
    public override ReportModule Module => ReportModule.Pay;
    public override App App => App.Payroll;
    public override string RequiredPermission => "payroll.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<CtcReportRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<CtcReportRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<CtcReportRow, string>("department", ColumnDataType.Text, r => r.Department, groupable: true),
        ReportColumn.Of<CtcReportRow, string>("designation", ColumnDataType.Text, r => r.Designation, groupable: true),
        ReportColumn.Of<CtcReportRow, string>("salaryStructure", ColumnDataType.Text, r => r.SalaryStructure, groupable: true),
        ReportColumn.Of<CtcReportRow, decimal>("annualCtc", ColumnDataType.Money, r => r.AnnualCtc, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<CtcReportRow, DateOnly>("effectiveFrom", ColumnDataType.Date, r => r.EffectiveFrom),
        ReportColumn.Of<CtcReportRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<CtcReportRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from s in db.EmployeeSalaries
               join e in db.Employees on s.EmployeeId equals e.EmployeeId
               join d in db.Departments on e.DepartmentId equals d.DepartmentId into gd from d in gd.DefaultIfEmpty()
               join des in db.Designations on e.DesignationId equals des.DesignationId into gdes from des in gdes.DefaultIfEmpty()
               join str in db.SalaryStructures on s.SalaryStructureId equals str.SalaryStructureId into gstr from str in gstr.DefaultIfEmpty()
               select new CtcReportRow
               {
                   Id = s.EmployeeSalaryId,
                   EmployeeCode = e.EmployeeCode,
                   EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                   Department = d != null ? d.Name : "Unassigned",
                   Designation = des != null ? des.Name : "Unassigned",
                   SalaryStructure = str != null ? str.Name : "Unassigned",
                   AnnualCtc = s.AnnualCtc,
                   EffectiveFrom = s.EffectiveFrom,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<CtcReportRow, string>>)(r => r.EmployeeCode);
}

// 19. Salary Variance
public class SalaryVarianceRow
{
    public long Id { get; set; }
    public DateOnly Month { get; set; }
    public string Department { get; set; } = null!;
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public decimal GrossEarnings { get; set; }
    public decimal GrossDeductions { get; set; }
    public decimal NetPay { get; set; }
}

public sealed class SalaryVarianceSource : ReportSource<SalaryVarianceRow>
{
    public override string ReportKey => "salary-variance";
    public override string Title => "Salary Variance";
    public override ReportModule Module => ReportModule.Pay;
    public override App App => App.Payroll;
    public override string RequiredPermission => "payroll.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<SalaryVarianceRow, DateOnly>("month", ColumnDataType.Date, r => r.Month),
        ReportColumn.Of<SalaryVarianceRow, string>("department", ColumnDataType.Text, r => r.Department, groupable: true),
        ReportColumn.Of<SalaryVarianceRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<SalaryVarianceRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<SalaryVarianceRow, decimal>("grossEarnings", ColumnDataType.Money, r => r.GrossEarnings, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SalaryVarianceRow, decimal>("grossDeductions", ColumnDataType.Money, r => r.GrossDeductions, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SalaryVarianceRow, decimal>("netPay", ColumnDataType.Money, r => r.NetPay, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SalaryVarianceRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<SalaryVarianceRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from p in db.Payslips
               join r in db.PayrollRuns on p.PayrollRunId equals r.PayrollRunId
               join e in db.Employees on p.EmployeeId equals e.EmployeeId
               join d in db.Departments on e.DepartmentId equals d.DepartmentId into gd from d in gd.DefaultIfEmpty()
               select new SalaryVarianceRow
               {
                   Id = p.PayslipId,
                   Month = r.Month,
                   Department = d != null ? d.Name : "Unassigned",
                   EmployeeCode = e.EmployeeCode,
                   EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                   GrossEarnings = p.GrossEarnings,
                   GrossDeductions = p.GrossDeductions,
                   NetPay = p.NetPay,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<SalaryVarianceRow, DateOnly>>)(r => r.Month);
}

// 20. Bank Advice
public class BankAdviceRow
{
    public long Id { get; set; }
    public DateOnly Month { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string BankName { get; set; } = null!;
    public string AccountNo { get; set; } = null!;
    public string Ifsc { get; set; } = null!;
    public decimal NetPay { get; set; }
}

public sealed class BankAdviceSource : ReportSource<BankAdviceRow>
{
    public override string ReportKey => "bank-advice";
    public override string Title => "Bank Advice";
    public override ReportModule Module => ReportModule.Pay;
    public override App App => App.Payroll;
    public override string RequiredPermission => "payroll.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<BankAdviceRow, DateOnly>("month", ColumnDataType.Date, r => r.Month),
        ReportColumn.Of<BankAdviceRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<BankAdviceRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<BankAdviceRow, string>("bankName", ColumnDataType.Text, r => r.BankName, groupable: true),
        ReportColumn.Of<BankAdviceRow, string>("accountNo", ColumnDataType.Text, r => r.AccountNo),
        ReportColumn.Of<BankAdviceRow, string>("ifsc", ColumnDataType.Text, r => r.Ifsc),
        ReportColumn.Of<BankAdviceRow, decimal>("netPay", ColumnDataType.Money, r => r.NetPay, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<BankAdviceRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<BankAdviceRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from p in db.Payslips
               join r in db.PayrollRuns on p.PayrollRunId equals r.PayrollRunId
               join e in db.Employees on p.EmployeeId equals e.EmployeeId
               join b in db.EmployeeBankDetails on e.EmployeeId equals b.EmployeeId into gb from b in gb.DefaultIfEmpty()
               where b == null || b.IsPrimary
               select new BankAdviceRow
               {
                   Id = p.PayslipId,
                   Month = r.Month,
                   EmployeeCode = e.EmployeeCode,
                   EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                   BankName = b != null ? b.BankName : "Unspecified",
                   AccountNo = b != null ? b.AccountNo : "Unspecified",
                   Ifsc = b != null ? b.Ifsc : "Unspecified",
                   NetPay = p.NetPay,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<BankAdviceRow, string>>)(r => r.EmployeeCode);
}

// 21. Held Salaries
public class HeldSalariesRow
{
    public long Id { get; set; }
    public DateOnly Month { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string Department { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateOnly? ReleasedDate { get; set; }
}

public sealed class HeldSalariesSource : ReportSource<HeldSalariesRow>
{
    public override string ReportKey => "held-salaries";
    public override string Title => "Held Salaries";
    public override ReportModule Module => ReportModule.Pay;
    public override App App => App.Payroll;
    public override string RequiredPermission => "payroll.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<HeldSalariesRow, DateOnly>("month", ColumnDataType.Date, r => r.Month),
        ReportColumn.Of<HeldSalariesRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<HeldSalariesRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<HeldSalariesRow, string>("department", ColumnDataType.Text, r => r.Department, groupable: true),
        ReportColumn.Of<HeldSalariesRow, string>("reason", ColumnDataType.Text, r => r.Reason),
        ReportColumn.Of<HeldSalariesRow, string>("status", ColumnDataType.Text, r => r.Status, groupable: true),
        ReportColumn.Of<HeldSalariesRow, DateOnly?>("releasedDate", ColumnDataType.Date, r => r.ReleasedDate),
        ReportColumn.Of<HeldSalariesRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<HeldSalariesRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from h in db.SalaryHolds
               join r in db.PayrollRuns on h.PayrollRunId equals r.PayrollRunId
               join e in db.Employees on h.EmployeeId equals e.EmployeeId
               join d in db.Departments on e.DepartmentId equals d.DepartmentId into gd from d in gd.DefaultIfEmpty()
               select new HeldSalariesRow
               {
                   Id = h.SalaryHoldId,
                   Month = r.Month,
                   EmployeeCode = e.EmployeeCode,
                   EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                   Department = d != null ? d.Name : "Unassigned",
                   Reason = h.Reason,
                   Status = h.Status,
                   ReleasedDate = h.ReleasedDate,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<HeldSalariesRow, DateOnly>>)(r => r.Month);
}

// 22. Salary Arrears
public class SalaryArrearsRow
{
    public long Id { get; set; }
    public DateOnly PaymentMonth { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string Department { get; set; } = null!;
    public string ComponentName { get; set; } = null!;
    public decimal Amount { get; set; }
    public string? Remarks { get; set; }
}

public sealed class SalaryArrearsSource : ReportSource<SalaryArrearsRow>
{
    public override string ReportKey => "salary-arrears";
    public override string Title => "Salary Arrears";
    public override ReportModule Module => ReportModule.Pay;
    public override App App => App.Payroll;
    public override string RequiredPermission => "payroll.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<SalaryArrearsRow, DateOnly>("paymentMonth", ColumnDataType.Date, r => r.PaymentMonth),
        ReportColumn.Of<SalaryArrearsRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<SalaryArrearsRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<SalaryArrearsRow, string>("department", ColumnDataType.Text, r => r.Department, groupable: true),
        ReportColumn.Of<SalaryArrearsRow, string>("componentName", ColumnDataType.Text, r => r.ComponentName, groupable: true),
        ReportColumn.Of<SalaryArrearsRow, decimal>("amount", ColumnDataType.Money, r => r.Amount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SalaryArrearsRow, string?>("remarks", ColumnDataType.Text, r => r.Remarks),
        ReportColumn.Of<SalaryArrearsRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<SalaryArrearsRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from o in db.OneTimePayments
               join e in db.Employees on o.EmployeeId equals e.EmployeeId
               join d in db.Departments on e.DepartmentId equals d.DepartmentId into gd from d in gd.DefaultIfEmpty()
               join c in db.SalaryComponents on o.SalaryComponentId equals c.SalaryComponentId into gc from c in gc.DefaultIfEmpty()
               select new SalaryArrearsRow
               {
                   Id = o.OneTimePaymentId,
                   PaymentMonth = o.PaymentMonth,
                   EmployeeCode = e.EmployeeCode,
                   EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                   Department = d != null ? d.Name : "Unassigned",
                   ComponentName = c != null ? c.Name : "Arrears",
                   Amount = o.Amount,
                   Remarks = o.Remarks,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<SalaryArrearsRow, DateOnly>>)(r => r.PaymentMonth);
}

// 23. Loans Outstanding
public class LoansOutstandingRow
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string Department { get; set; } = null!;
    public decimal PrincipalAmount { get; set; }
    public DateOnly DisbursedDate { get; set; }
    public decimal MonthlyInstallment { get; set; }
    public decimal TotalRecovered { get; set; }
    public decimal OutstandingBalance { get; set; }
    public string Status { get; set; } = null!;
}

public sealed class LoansOutstandingSource : ReportSource<LoansOutstandingRow>
{
    public override string ReportKey => "loans-outstanding";
    public override string Title => "Loans Outstanding";
    public override ReportModule Module => ReportModule.Pay;
    public override App App => App.Payroll;
    public override string RequiredPermission => "payroll.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<LoansOutstandingRow, string>("employeeCode", ColumnDataType.Text, r => r.EmployeeCode),
        ReportColumn.Of<LoansOutstandingRow, string>("employeeName", ColumnDataType.Text, r => r.EmployeeName, groupable: true),
        ReportColumn.Of<LoansOutstandingRow, string>("department", ColumnDataType.Text, r => r.Department, groupable: true),
        ReportColumn.Of<LoansOutstandingRow, decimal>("principalAmount", ColumnDataType.Money, r => r.PrincipalAmount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<LoansOutstandingRow, DateOnly>("disbursedDate", ColumnDataType.Date, r => r.DisbursedDate),
        ReportColumn.Of<LoansOutstandingRow, decimal>("monthlyInstallment", ColumnDataType.Money, r => r.MonthlyInstallment, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<LoansOutstandingRow, decimal>("totalRecovered", ColumnDataType.Money, r => r.TotalRecovered, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<LoansOutstandingRow, decimal>("outstandingBalance", ColumnDataType.Money, r => r.OutstandingBalance, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<LoansOutstandingRow, string>("status", ColumnDataType.Text, r => r.Status, groupable: true),
        ReportColumn.Of<LoansOutstandingRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<LoansOutstandingRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from l in db.EmployeeLoans
               join e in db.Employees on l.EmployeeId equals e.EmployeeId
               join d in db.Departments on e.DepartmentId equals d.DepartmentId into gd from d in gd.DefaultIfEmpty()
               select new LoansOutstandingRow
               {
                   Id = l.EmployeeLoanId,
                   EmployeeCode = e.EmployeeCode,
                   EmployeeName = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                   Department = d != null ? d.Name : "Unassigned",
                   PrincipalAmount = l.PrincipalAmount,
                   DisbursedDate = l.DisbursedDate,
                   MonthlyInstallment = l.MonthlyInstallment,
                   TotalRecovered = l.TotalRecovered,
                   OutstandingBalance = l.PrincipalAmount - l.TotalRecovered,
                   Status = l.Status,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<LoansOutstandingRow, string>>)(r => r.EmployeeCode);
}
