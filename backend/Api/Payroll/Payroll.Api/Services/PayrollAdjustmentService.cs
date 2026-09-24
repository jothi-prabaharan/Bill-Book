using Microsoft.EntityFrameworkCore;
using Payroll.Entity.Models;
using Payroll.Entity.TableEntities;
using Payroll.Repository;

namespace Payroll.Api.Services;

public sealed class PayrollAdjustmentService
{
    private readonly PayrollDbContext _db;

    public PayrollAdjustmentService(PayrollDbContext db) => _db = db;

    // ---- One Time Payments --------------------------------------------------

    public async Task<IReadOnlyList<OneTimePaymentView>> ListOneTimePaymentsAsync(DateOnly month, CancellationToken ct) =>
        await _db.OneTimePayments.AsNoTracking()
            .Include(o => o.Component)
            .Where(o => o.Month == month)
            .Select(o => new OneTimePaymentView
            {
                OneTimePaymentId = o.OneTimePaymentId,
                EmployeeId = o.EmployeeId,
                SalaryComponentId = o.SalaryComponentId,
                ComponentName = o.Component.Name,
                Amount = o.Amount,
                Month = o.Month,
                Processed = o.Processed
            })
            .ToListAsync(ct);

    public async Task<long> CreateOneTimePaymentAsync(SaveOneTimePaymentRequest request, CancellationToken ct)
    {
        var payment = new OneTimePayment
        {
            EmployeeId = request.EmployeeId,
            SalaryComponentId = request.SalaryComponentId,
            Amount = request.Amount,
            Month = request.Month,
            Processed = false
        };
        _db.OneTimePayments.Add(payment);
        await _db.SaveChangesAsync(ct);
        return payment.OneTimePaymentId;
    }

    // ---- Salary Holds -------------------------------------------------------

    public async Task<IReadOnlyList<SalaryHoldView>> ListSalaryHoldsAsync(CancellationToken ct) =>
        await _db.SalaryHolds.AsNoTracking()
            .OrderByDescending(h => h.FromMonth)
            .Select(h => new SalaryHoldView
            {
                SalaryHoldId = h.SalaryHoldId,
                EmployeeId = h.EmployeeId,
                FromMonth = h.FromMonth,
                ToMonth = h.ToMonth,
                IsActive = h.IsActive,
                Reason = h.Reason
            })
            .ToListAsync(ct);

    public async Task<long> CreateSalaryHoldAsync(SaveSalaryHoldRequest request, CancellationToken ct)
    {
        var hold = new SalaryHold
        {
            EmployeeId = request.EmployeeId,
            FromMonth = request.FromMonth,
            ToMonth = request.ToMonth,
            IsActive = true,
            Reason = request.Reason
        };
        _db.SalaryHolds.Add(hold);
        await _db.SaveChangesAsync(ct);
        return hold.SalaryHoldId;
    }

    public async Task ReleaseSalaryHoldAsync(long holdId, CancellationToken ct)
    {
        SalaryHold? hold = await _db.SalaryHolds.FirstOrDefaultAsync(h => h.SalaryHoldId == holdId, ct)
            ?? throw new KeyNotFoundException($"Salary hold {holdId} not found.");
        hold.IsActive = false;
        await _db.SaveChangesAsync(ct);
    }

    // ---- Employee Loans -----------------------------------------------------

    public async Task<IReadOnlyList<EmployeeLoanView>> ListEmployeeLoansAsync(long? employeeId, CancellationToken ct)
    {
        var query = _db.EmployeeLoans.AsNoTracking().Include(l => l.Repayments).AsQueryable();
        if (employeeId.HasValue)
        {
            query = query.Where(l => l.EmployeeId == employeeId.Value);
        }

        return await query.Select(l => new EmployeeLoanView
        {
            EmployeeLoanId = l.EmployeeLoanId,
            EmployeeId = l.EmployeeId,
            PrincipalAmount = l.PrincipalAmount,
            InterestRate = l.InterestRate,
            TermMonths = l.TermMonths,
            MonthlyEmi = l.MonthlyEmi,
            DisbursedDate = l.DisbursedDate,
            DeductionStartDate = l.DeductionStartDate,
            IsSettled = l.IsSettled,
            TotalRepaid = l.Repayments.Where(r => r.Processed).Sum(r => r.PrincipalComponent + r.InterestComponent),
            BalanceRemaining = Math.Max(0m, l.PrincipalAmount - l.Repayments.Where(r => r.Processed).Sum(r => r.PrincipalComponent))
        }).ToListAsync(ct);
    }

    public async Task<long> DisburseLoanAsync(DisburseLoanRequest request, CancellationToken ct)
    {
        var loan = new EmployeeLoan
        {
            EmployeeId = request.EmployeeId,
            PrincipalAmount = request.PrincipalAmount,
            InterestRate = request.InterestRate,
            TermMonths = request.TermMonths,
            MonthlyEmi = request.MonthlyEmi,
            DisbursedDate = request.DisbursedDate,
            DeductionStartDate = request.DeductionStartDate,
            IsSettled = false
        };
        _db.EmployeeLoans.Add(loan);
        await _db.SaveChangesAsync(ct);
        return loan.EmployeeLoanId;
    }

    // ---- Monthly Attendance Input -------------------------------------------

    public async Task<IReadOnlyList<MonthlyAttendanceView>> ListMonthlyAttendanceAsync(DateOnly month, CancellationToken ct) =>
        await _db.MonthlyAttendanceInputs.AsNoTracking()
            .Where(a => a.Month == month)
            .Select(a => new MonthlyAttendanceView
            {
                MonthlyAttendanceInputId = a.MonthlyAttendanceInputId,
                EmployeeId = a.EmployeeId,
                Month = a.Month,
                PaidDays = a.PaidDays,
                LossOfPayDays = a.LossOfPayDays
            })
            .ToListAsync(ct);

    public async Task SaveMonthlyAttendanceAsync(SaveMonthlyAttendanceRequest request, CancellationToken ct)
    {
        MonthlyAttendanceInput? input = await _db.MonthlyAttendanceInputs
            .FirstOrDefaultAsync(a => a.EmployeeId == request.EmployeeId && a.Month == request.Month, ct);

        if (input is null)
        {
            input = new MonthlyAttendanceInput
            {
                EmployeeId = request.EmployeeId,
                Month = request.Month,
                PaidDays = request.PaidDays,
                LossOfPayDays = request.LossOfPayDays
            };
            _db.MonthlyAttendanceInputs.Add(input);
        }
        else
        {
            input.PaidDays = request.PaidDays;
            input.LossOfPayDays = request.LossOfPayDays;
        }

        await _db.SaveChangesAsync(ct);
    }
}
