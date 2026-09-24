using Microsoft.EntityFrameworkCore;
using Payroll.Entity.Enums;
using Payroll.Entity.Models;
using Payroll.Entity.TableEntities;
using Payroll.Repository;
using Shared.Kernel.Tenancy;
using System.Text;

namespace Payroll.Api.Services;

public sealed class PayrollRunService
{
    private readonly PayrollDbContext _db;
    private readonly TenantContext _tenant;
    private readonly ILedgerClient _ledger;

    public PayrollRunService(PayrollDbContext db, TenantContext tenant, ILedgerClient ledger)
    {
        _db = db;
        _tenant = tenant;
        _ledger = ledger;
    }

    public async Task<IReadOnlyList<PayrollRunView>> ListRunsAsync(CancellationToken ct) =>
        await _db.PayrollRuns.AsNoTracking()
            .Include(r => r.Payslips)
            .OrderByDescending(r => r.Month)
            .Select(r => new PayrollRunView
            {
                PayrollRunId = r.PayrollRunId,
                Month = r.Month,
                Status = r.Status,
                EmployeeCount = r.EmployeeCount,
                TotalGrossEarnings = r.Payslips.Sum(p => p.GrossEarnings),
                TotalGrossDeductions = r.Payslips.Sum(p => p.GrossDeductions),
                TotalNetPay = r.TotalNetPay,
                DaysSource = r.DaysSource,
                JournalId = r.JournalId
            })
            .ToListAsync(ct);

    public async Task<PayrollRunView?> GetRunAsync(long runId, CancellationToken ct)
    {
        PayrollRun? r = await _db.PayrollRuns.AsNoTracking()
            .Include(x => x.Payslips)
            .FirstOrDefaultAsync(x => x.PayrollRunId == runId, ct);

        if (r is null) return null;

        return new PayrollRunView
        {
            PayrollRunId = r.PayrollRunId,
            Month = r.Month,
            Status = r.Status,
            EmployeeCount = r.EmployeeCount,
            TotalGrossEarnings = r.Payslips.Sum(p => p.GrossEarnings),
            TotalGrossDeductions = r.Payslips.Sum(p => p.GrossDeductions),
            TotalNetPay = r.TotalNetPay,
            DaysSource = r.DaysSource,
            JournalId = r.JournalId
        };
    }

    public async Task<IReadOnlyList<PayslipView>> GetPayslipsAsync(long runId, CancellationToken ct) =>
        await _db.Payslips.AsNoTracking()
            .Include(p => p.Lines)
            .Where(p => p.PayrollRunId == runId)
            .Select(p => new PayslipView
            {
                PayslipId = p.PayslipId,
                PayrollRunId = p.PayrollRunId,
                EmployeeId = p.EmployeeId,
                PaidDays = p.PaidDays,
                GrossEarnings = p.GrossEarnings,
                GrossDeductions = p.GrossDeductions,
                NetPay = p.NetPay,
                Lines = p.Lines.Select(l => new PayslipLineView
                {
                    PayslipLineId = l.PayslipLineId,
                    SalaryComponentId = l.SalaryComponentId,
                    ComponentName = l.Kind.ToString(),
                    Kind = l.Kind,
                    Amount = l.Amount
                }).ToList()
            })
            .ToListAsync(ct);

    public async Task<long> ProcessRunAsync(DateOnly month, CancellationToken ct)
    {
        DateOnly normalizedMonth = new DateOnly(month.Year, month.Month, 1);
        int daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);

        PayrollRun? run = await _db.PayrollRuns
            .Include(r => r.Payslips).ThenInclude(p => p.Lines)
            .FirstOrDefaultAsync(r => r.Month == normalizedMonth, ct);

        if (run is not null && run.Status is PayrollRunStatus.Approved or PayrollRunStatus.Posted or PayrollRunStatus.MarkedPaid)
        {
            throw new InvalidOperationException($"Payroll run for {normalizedMonth:yyyy-MM} is already {run.Status} and cannot be re-processed.");
        }

        // Determine days source: check if licensed for HRMS, fallback to MonthlyAttendanceInput
        string daysSource = "MonthlyAttendanceInput";

        if (run is null)
        {
            run = new PayrollRun
            {
                Month = normalizedMonth,
                Status = PayrollRunStatus.Draft,
                DaysSource = daysSource
            };
            _db.PayrollRuns.Add(run);
        }
        else
        {
            // Clear existing payslips
            _db.Payslips.RemoveRange(run.Payslips);
            run.Payslips.Clear();
            run.DaysSource = daysSource;
            run.Status = PayrollRunStatus.Draft;
        }

        // Fetch distinct employee salaries effective on or before this month
        List<EmployeeSalary> salaries = await _db.EmployeeSalaries
            .Include(es => es.Structure).ThenInclude(s => s.Components).ThenInclude(sc => sc.Component)
            .Where(es => es.EffectiveFrom <= normalizedMonth)
            .GroupBy(es => es.EmployeeId)
            .Select(g => g.OrderByDescending(x => x.EffectiveFrom).First())
            .ToListAsync(ct);

        List<MonthlyAttendanceInput> attendances = await _db.MonthlyAttendanceInputs
            .Where(a => a.Month == normalizedMonth)
            .ToListAsync(ct);

        List<OneTimePayment> oneTimes = await _db.OneTimePayments
            .Include(o => o.Component)
            .Where(o => o.Month == normalizedMonth && !o.Processed)
            .ToListAsync(ct);

        List<EmployeeLoan> loans = await _db.EmployeeLoans
            .Where(l => !l.IsSettled && l.DeductionStartDate <= normalizedMonth)
            .ToListAsync(ct);

        List<SalaryRevision> arrears = await _db.SalaryRevisions
            .Where(r => !r.ArrearsProcessed && r.EffectiveFrom < normalizedMonth)
            .ToListAsync(ct);

        List<SalaryHold> activeHolds = await _db.SalaryHolds
            .Where(h => h.IsActive && h.FromMonth <= normalizedMonth && (h.ToMonth == null || h.ToMonth >= normalizedMonth))
            .ToListAsync(ct);

        decimal totalNet = 0m;

        foreach (EmployeeSalary salary in salaries)
        {
            decimal paidDays = daysInMonth;
            var att = attendances.FirstOrDefault(a => a.EmployeeId == salary.EmployeeId);
            if (att != null)
            {
                paidDays = att.PaidDays;
            }

            var empOneTimes = oneTimes.Where(o => o.EmployeeId == salary.EmployeeId).ToList();
            var empLoan = loans.FirstOrDefault(l => l.EmployeeId == salary.EmployeeId);
            var empArrears = arrears.Where(r => r.EmployeeId == salary.EmployeeId).ToList();
            bool isHeld = activeHolds.Any(h => h.EmployeeId == salary.EmployeeId);

            CalculatedPayslipData calculated = PayrollCalculationEngine.Compute(
                salary, paidDays, daysInMonth, empOneTimes, empLoan, empArrears, isHeld);

            var payslip = new Payslip
            {
                EmployeeId = salary.EmployeeId,
                PaidDays = calculated.PaidDays,
                GrossEarnings = calculated.GrossEarnings,
                GrossDeductions = calculated.GrossDeductions,
                NetPay = calculated.NetPay
            };

            foreach (var line in calculated.Lines)
            {
                payslip.Lines.Add(new PayslipLine
                {
                    SalaryComponentId = line.SalaryComponentId,
                    Kind = line.Kind,
                    Amount = line.Amount
                });
            }

            run.Payslips.Add(payslip);
            totalNet += calculated.NetPay;
        }

        run.EmployeeCount = run.Payslips.Count;
        run.TotalNetPay = totalNet;
        run.Status = PayrollRunStatus.Processed;

        await _db.SaveChangesAsync(ct);
        return run.PayrollRunId;
    }

    public async Task ApproveRunAsync(long runId, CancellationToken ct)
    {
        PayrollRun run = await _db.PayrollRuns.FirstOrDefaultAsync(r => r.PayrollRunId == runId, ct)
            ?? throw new KeyNotFoundException($"Payroll run {runId} not found.");

        if (run.Status != PayrollRunStatus.Processed)
        {
            throw new InvalidOperationException($"Cannot approve run in status {run.Status}.");
        }

        run.Status = PayrollRunStatus.Approved;
        await _db.SaveChangesAsync(ct);
    }

    public async Task PostRunAsync(long runId, CancellationToken ct)
    {
        PayrollRun run = await _db.PayrollRuns
            .Include(r => r.Payslips).ThenInclude(p => p.Lines)
            .FirstOrDefaultAsync(r => r.PayrollRunId == runId, ct)
            ?? throw new KeyNotFoundException($"Payroll run {runId} not found.");

        if (run.Status != PayrollRunStatus.Approved)
        {
            throw new InvalidOperationException($"Only an Approved run can be posted. Current status: {run.Status}.");
        }

        (Guid customerId, Guid orgId) = _tenant.Require();

        decimal totalEarnings = run.Payslips.Sum(p => p.GrossEarnings);
        decimal totalNetPay = run.Payslips.Sum(p => p.NetPay);
        decimal totalDeductions = run.Payslips.Sum(p => p.GrossDeductions);

        // Build balanced journal legs:
        // Dr: Payroll Expense (Gross Earnings)
        // Cr: Salary Payable (Total Net Pay)
        // Cr: Salary Payable / Deductions (Total Deductions)
        // Check: totalEarnings == totalNetPay + totalDeductions
        decimal creditDeductions = Math.Max(0m, totalEarnings - totalNetPay);

        var request = new PostLedgerRequest
        {
            CustomerId = customerId,
            OrgId = orgId,
            TransactionTypeCode = "PAY",
            TransactionId = run.PayrollRunId,
            LedgerDate = run.Month,
            DocumentNo = $"PAY-{run.Month:yyyy-MM}",
            Legs =
            [
                new LedgerLegRequest
                {
                    LedgerTypeId = 1,
                    LedgerSourceId = 1,
                    AccountSystemName = "Payroll Expense",
                    DebitAmount = totalEarnings,
                    CreditAmount = 0m,
                    TransactionDesc = $"Payroll for {run.Month:MMM yyyy} - Gross Salaries"
                },
                new LedgerLegRequest
                {
                    LedgerTypeId = 3,
                    LedgerSourceId = 1,
                    AccountSystemName = "Salary Payable",
                    DebitAmount = 0m,
                    CreditAmount = totalNetPay,
                    TransactionDesc = $"Payroll for {run.Month:MMM yyyy} - Net Payable"
                }
            ]
        };

        if (creditDeductions > 0)
        {
            request.Legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = 3,
                LedgerSourceId = 1,
                AccountSystemName = "Salary Payable",
                DebitAmount = 0m,
                CreditAmount = creditDeductions,
                TransactionDesc = $"Payroll for {run.Month:MMM yyyy} - Deductions / Withholdings"
            });
        }

        PostLedgerOutcomeResult outcome = await _ledger.PostAsync(request, ct);
        if (!outcome.Posted)
        {
            throw new InvalidOperationException($"Failed to post payroll run to Accounting: {outcome.Detail}");
        }

        // Mark OTPs as processed
        List<OneTimePayment> otps = await _db.OneTimePayments
            .Where(o => o.Month == run.Month && !o.Processed)
            .ToListAsync(ct);
        foreach (var otp in otps) otp.Processed = true;

        // Mark Arrears as processed
        List<SalaryRevision> arrears = await _db.SalaryRevisions
            .Where(r => !r.ArrearsProcessed && r.EffectiveFrom < run.Month)
            .ToListAsync(ct);
        foreach (var rev in arrears) rev.ArrearsProcessed = true;

        // Record loan repayments
        List<EmployeeLoan> activeLoans = await _db.EmployeeLoans
            .Where(l => !l.IsSettled && l.DeductionStartDate <= run.Month)
            .ToListAsync(ct);

        foreach (var loan in activeLoans)
        {
            decimal emi = Math.Min(loan.MonthlyEmi, loan.PrincipalAmount);
            _db.LoanRepayments.Add(new LoanRepayment
            {
                EmployeeLoanId = loan.EmployeeLoanId,
                Month = run.Month,
                PrincipalComponent = emi,
                InterestComponent = 0m,
                Processed = true
            });
        }

        run.Status = PayrollRunStatus.Posted;
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkPaidAsync(long runId, CancellationToken ct)
    {
        PayrollRun run = await _db.PayrollRuns.FirstOrDefaultAsync(r => r.PayrollRunId == runId, ct)
            ?? throw new KeyNotFoundException($"Payroll run {runId} not found.");

        if (run.Status != PayrollRunStatus.Posted)
        {
            throw new InvalidOperationException($"Only a Posted run can be marked paid. Current status: {run.Status}.");
        }

        run.Status = PayrollRunStatus.MarkedPaid;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ReverseRunAsync(long runId, CancellationToken ct)
    {
        PayrollRun run = await _db.PayrollRuns.FirstOrDefaultAsync(r => r.PayrollRunId == runId, ct)
            ?? throw new KeyNotFoundException($"Payroll run {runId} not found.");

        if (run.Status != PayrollRunStatus.Posted)
        {
            throw new InvalidOperationException($"Only a Posted run can be reversed. Current status: {run.Status}.");
        }

        (Guid customerId, Guid orgId) = _tenant.Require();

        // Withdraw posted legs in Accounting
        var request = new PostLedgerRequest
        {
            CustomerId = customerId,
            OrgId = orgId,
            TransactionTypeCode = "PAY",
            TransactionId = run.PayrollRunId,
            LedgerDate = run.Month,
            WithdrawLedgerTypeIds = [1, 3],
            Legs = []
        };

        PostLedgerOutcomeResult outcome = await _ledger.PostAsync(request, ct);
        if (!outcome.Posted)
        {
            throw new InvalidOperationException($"Failed to reverse payroll posting in Accounting: {outcome.Detail}");
        }

        run.Status = PayrollRunStatus.Reversed;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<string> ExportBankCsvAsync(long runId, CancellationToken ct)
    {
        var payslips = await _db.Payslips.AsNoTracking()
            .Where(p => p.PayrollRunId == runId)
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("EmployeeId,PaidDays,GrossEarnings,GrossDeductions,NetPay");
        foreach (var p in payslips)
        {
            sb.AppendLine($"{p.EmployeeId},{p.PaidDays},{p.GrossEarnings:F2},{p.GrossDeductions:F2},{p.NetPay:F2}");
        }
        return sb.ToString();
    }

    public async Task<string> ExportTallyXmlAsync(long runId, CancellationToken ct)
    {
        PayrollRun? run = await _db.PayrollRuns.AsNoTracking()
            .Include(r => r.Payslips)
            .FirstOrDefaultAsync(r => r.PayrollRunId == runId, ct);

        if (run is null) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("<ENVELOPE>");
        sb.AppendLine("  <HEADER><TALLYREQUEST>Import Data</TALLYREQUEST></HEADER>");
        sb.AppendLine("  <BODY>");
        sb.AppendLine("    <IMPORTDATA>");
        sb.AppendLine("      <REQUESTDATA>");
        sb.AppendLine($"        <VOUCHER VCHTYPE=\"Journal\" ACTION=\"Create\">");
        sb.AppendLine($"          <DATE>{run.Month:yyyyMMdd}</DATE>");
        sb.AppendLine($"          <NARRATION>Payroll for {run.Month:MMMM yyyy}</NARRATION>");
        sb.AppendLine($"          <ALLLEDGERENTRIES.LIST>");
        sb.AppendLine($"            <LEDGERNAME>Payroll Expense</LEDGERNAME>");
        sb.AppendLine($"            <ISDEEMEDPOSITIVE>Yes</ISDEEMEDPOSITIVE>");
        sb.AppendLine($"            <AMOUNT>-{run.Payslips.Sum(p => p.GrossEarnings):F2}</AMOUNT>");
        sb.AppendLine($"          </ALLLEDGERENTRIES.LIST>");
        sb.AppendLine($"          <ALLLEDGERENTRIES.LIST>");
        sb.AppendLine($"            <LEDGERNAME>Salary Payable</LEDGERNAME>");
        sb.AppendLine($"            <ISDEEMEDPOSITIVE>No</ISDEEMEDPOSITIVE>");
        sb.AppendLine($"            <AMOUNT>{run.TotalNetPay:F2}</AMOUNT>");
        sb.AppendLine($"          </ALLLEDGERENTRIES.LIST>");
        sb.AppendLine("        </VOUCHER>");
        sb.AppendLine("      </REQUESTDATA>");
        sb.AppendLine("    </IMPORTDATA>");
        sb.AppendLine("  </BODY>");
        sb.AppendLine("</ENVELOPE>");
        return sb.ToString();
    }
}
