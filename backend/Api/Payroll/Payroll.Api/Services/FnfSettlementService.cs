using Microsoft.EntityFrameworkCore;
using Payroll.Entity.Enums;
using Payroll.Entity.Models;
using Payroll.Entity.TableEntities;
using Payroll.Repository;
using Shared.Kernel.Tenancy;

namespace Payroll.Api.Services;

public sealed class FnfSettlementService
{
    private readonly PayrollDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILedgerClient _ledgerClient;
    private readonly IEmployeeClient _hrmClient;
    private readonly IMasterUserClient _masterUserClient;

    public FnfSettlementService(
        PayrollDbContext db,
        ITenantContext tenant,
        ILedgerClient ledgerClient,
        IEmployeeClient hrmClient,
        IMasterUserClient masterUserClient)
    {
        _db = db;
        _tenant = tenant;
        _ledgerClient = ledgerClient;
        _hrmClient = hrmClient;
        _masterUserClient = masterUserClient;
    }

    public async Task<FullAndFinalSettlement?> GetSettlementAsync(long id, CancellationToken ct) =>
        await _db.FullAndFinalSettlements.AsNoTracking()
            .Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.FullAndFinalSettlementId == id, ct);

    public async Task<FullAndFinalSettlement?> GetSettlementByEmployeeAsync(long employeeId, CancellationToken ct) =>
        await _db.FullAndFinalSettlements.AsNoTracking()
            .Include(s => s.Lines)
            .OrderByDescending(s => s.FullAndFinalSettlementId)
            .FirstOrDefaultAsync(s => s.EmployeeId == employeeId, ct);

    public async Task<long> CalculateAndCreateSettlementAsync(CreateFnfSettlementRequest req, CancellationToken ct)
    {
        var salary = await _db.EmployeeSalaries.AsNoTracking()
            .Include(s => s.Structure.Components)
            .ThenInclude(sc => sc.Component)
            .Where(s => s.EmployeeId == req.EmployeeId)
            .OrderByDescending(s => s.EffectiveFrom)
            .FirstOrDefaultAsync(ct);

        decimal monthlyGross = 0m;
        decimal basic = 0m;

        if (salary?.Structure?.Components != null)
        {
            foreach (var comp in salary.Structure.Components)
            {
                if (comp.Component.Kind == ComponentKind.Earning)
                {
                    decimal compAmt = comp.ValueType == SalaryValueType.FlatAmount ? (comp.FlatAmount ?? 0m) : (salary.AnnualCtc / 12m) * ((comp.Percentage ?? 0m) / 100m);
                    monthlyGross += compAmt;
                    if (comp.Component.Name.Contains("Basic", StringComparison.OrdinalIgnoreCase))
                    {
                        basic += compAmt;
                    }
                }
            }
        }

        if (monthlyGross == 0m)
        {
            monthlyGross = salary is not null && salary.AnnualCtc > 0 ? Math.Round(salary.AnnualCtc / 12m, 2) : 50000m;
            basic = Math.Round(monthlyGross * 0.5m, 2);
        }

        int daysInMonth = DateTime.DaysInMonth(req.LastWorkingDate.Year, req.LastWorkingDate.Month);
        int workedDays = Math.Min(daysInMonth, req.LastWorkingDate.Day);

        decimal salaryToLwd = Math.Round((monthlyGross / daysInMonth) * workedDays, 2);

        decimal gratuity = 0m;
        if (req.CompletedYearsOfService >= 5)
        {
            gratuity = Math.Round((15m * basic * req.CompletedYearsOfService) / 26m, 2);
        }

        decimal noticeRecovery = 0m;
        if (req.NoticeShortfallDays > 0)
        {
            decimal dailyRate = Math.Round(monthlyGross / 30m, 2);
            noticeRecovery = Math.Round(dailyRate * req.NoticeShortfallDays, 2);
        }

        var activeLoans = await _db.EmployeeLoans.AsNoTracking()
            .Include(l => l.Repayments)
            .Where(l => l.EmployeeId == req.EmployeeId && !l.IsSettled)
            .ToListAsync(ct);

        decimal loanRecovery = activeLoans.Sum(l => Math.Max(0m, l.PrincipalAmount - l.Repayments.Sum(r => r.PrincipalComponent)));

        var lines = new List<FnfLine>
        {
            new FnfLine
            {
                Kind = FnfLineKind.SalaryToLwd,
                Amount = salaryToLwd,
                IsDeduction = false,
                Remarks = $"Salary for {workedDays}/{daysInMonth} days in exit month"
            }
        };

        if (gratuity > 0m)
        {
            lines.Add(new FnfLine
            {
                Kind = FnfLineKind.Gratuity,
                Amount = gratuity,
                IsDeduction = false,
                Remarks = $"Gratuity for {req.CompletedYearsOfService} years of service"
            });
        }

        if (noticeRecovery > 0m)
        {
            lines.Add(new FnfLine
            {
                Kind = FnfLineKind.NoticeRecovery,
                Amount = noticeRecovery,
                IsDeduction = true,
                Remarks = $"Notice recovery for {req.NoticeShortfallDays} shortfall days"
            });
        }

        if (loanRecovery > 0m)
        {
            lines.Add(new FnfLine
            {
                Kind = FnfLineKind.LoanRecovery,
                Amount = loanRecovery,
                IsDeduction = true,
                Remarks = "Outstanding loan balance recovery"
            });
        }

        decimal totalEarnings = lines.Where(l => !l.IsDeduction).Sum(l => l.Amount);
        decimal totalDeductions = lines.Where(l => l.IsDeduction).Sum(l => l.Amount);
        decimal netPayable = Math.Max(0m, totalEarnings - totalDeductions);

        var settlement = new FullAndFinalSettlement
        {
            EmployeeId = req.EmployeeId,
            SeparationId = req.SeparationId,
            LastWorkingDate = req.LastWorkingDate,
            Status = FnfStatus.Draft,
            NetPayable = netPayable,
            Remarks = req.Remarks,
            Lines = lines
        };

        _db.FullAndFinalSettlements.Add(settlement);
        await _db.SaveChangesAsync(ct);
        return settlement.FullAndFinalSettlementId;
    }

    public async Task ApproveSettlementAsync(long settlementId, CancellationToken ct)
    {
        var settlement = await _db.FullAndFinalSettlements.FirstOrDefaultAsync(s => s.FullAndFinalSettlementId == settlementId, ct)
            ?? throw new KeyNotFoundException($"Settlement {settlementId} not found.");

        settlement.Status = FnfStatus.Approved;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<long> PostSettlementAsync(long settlementId, Guid? linkedUserId, CancellationToken ct)
    {
        var settlement = await _db.FullAndFinalSettlements
            .Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.FullAndFinalSettlementId == settlementId, ct)
            ?? throw new KeyNotFoundException($"Settlement {settlementId} not found.");

        if (settlement.Status == FnfStatus.Posted || settlement.Status == FnfStatus.Paid)
        {
            throw new InvalidOperationException("Settlement is already posted.");
        }

        (Guid customerId, Guid orgId) = _tenant.Require();

        decimal totalEarnings = settlement.Lines.Where(l => !l.IsDeduction).Sum(l => l.Amount);
        decimal totalDeductions = settlement.Lines.Where(l => l.IsDeduction).Sum(l => l.Amount);
        decimal netPay = settlement.NetPayable;

        // 1. Create and post FullAndFinal PayrollRun
        var run = new PayrollRun
        {
            Kind = PayrollRunKind.FullAndFinal,
            Month = new DateOnly(settlement.LastWorkingDate.Year, settlement.LastWorkingDate.Month, 1),
            Status = PayrollRunStatus.Posted,
            EmployeeCount = 1,
            TotalNetPay = netPay,
            DaysSource = "FullAndFinal"
        };
        _db.PayrollRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        var payslip = new Payslip
        {
            PayrollRunId = run.PayrollRunId,
            EmployeeId = settlement.EmployeeId,
            GrossEarnings = totalEarnings,
            GrossDeductions = totalDeductions,
            NetPay = netPay,
            PaidDays = settlement.LastWorkingDate.Day
        };
        _db.Payslips.Add(payslip);

        settlement.Status = FnfStatus.Posted;
        settlement.PayrollRunId = run.PayrollRunId;

        // 2. Post balanced journal through Accounting
        var postReq = new PostLedgerRequest
        {
            CustomerId = customerId,
            OrgId = orgId,
            TransactionTypeCode = "PAY",
            TransactionId = run.PayrollRunId,
            LedgerDate = run.Month,
            DocumentNo = $"FNF-{settlement.FullAndFinalSettlementId}",
            Legs =
            [
                new LedgerLegRequest
                {
                    LedgerTypeId = 1,
                    LedgerSourceId = 1,
                    AccountSystemName = "Payroll Expense",
                    DebitAmount = totalEarnings,
                    CreditAmount = 0m,
                    TransactionDesc = $"F&F Settlement Employee #{settlement.EmployeeId} - Gross"
                },
                new LedgerLegRequest
                {
                    LedgerTypeId = 3,
                    LedgerSourceId = 1,
                    AccountSystemName = "Salary Payable",
                    DebitAmount = 0m,
                    CreditAmount = netPay,
                    TransactionDesc = $"F&F Settlement Employee #{settlement.EmployeeId} - Net Payable"
                }
            ]
        };

        if (totalDeductions > 0m)
        {
            postReq.Legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = 3,
                LedgerSourceId = 1,
                AccountSystemName = "Salary Payable",
                DebitAmount = 0m,
                CreditAmount = totalDeductions,
                TransactionDesc = $"F&F Settlement Employee #{settlement.EmployeeId} - Deductions"
            });
        }

        await _ledgerClient.PostAsync(postReq, ct);

        // 3. Clear employee loans if any recovered
        var loans = await _db.EmployeeLoans.Where(l => l.EmployeeId == settlement.EmployeeId && !l.IsSettled).ToListAsync(ct);
        foreach (var l in loans)
        {
            l.IsSettled = true;
        }

        await _db.SaveChangesAsync(ct);

        // 4. Notify HRMS service of exit settlement
        await _hrmClient.SettleEmployeeAsync(settlement.EmployeeId, settlement.LastWorkingDate, ct);

        // 5. Deactivate linked login through Master API
        if (linkedUserId.HasValue)
        {
            await _masterUserClient.DeactivateUserAsync(linkedUserId.Value, ct);
        }

        return run.PayrollRunId;
    }
}
