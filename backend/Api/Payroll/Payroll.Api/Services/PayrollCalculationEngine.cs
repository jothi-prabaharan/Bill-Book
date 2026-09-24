using Payroll.Entity.Enums;
using Payroll.Entity.TableEntities;

namespace Payroll.Api.Services;

public sealed class CalculatedPayslipData
{
    public long EmployeeId { get; set; }
    public decimal PaidDays { get; set; }
    public decimal GrossEarnings { get; set; }
    public decimal GrossDeductions { get; set; }
    public decimal NetPay { get; set; }
    public List<CalculatedLineData> Lines { get; set; } = [];
}

public sealed class CalculatedLineData
{
    public long SalaryComponentId { get; set; }
    public string ComponentName { get; set; } = null!;
    public ComponentKind Kind { get; set; }
    public decimal Amount { get; set; }
}

public static class PayrollCalculationEngine
{
    public static CalculatedPayslipData Compute(
        EmployeeSalary salary,
        decimal paidDays,
        int daysInMonth,
        List<OneTimePayment> oneTimePayments,
        EmployeeLoan? activeLoan,
        List<SalaryRevision> pendingArrearsRevisions,
        bool isHeld)
    {
        var result = new CalculatedPayslipData
        {
            EmployeeId = salary.EmployeeId,
            PaidDays = paidDays
        };

        if (daysInMonth <= 0) daysInMonth = 30;
        decimal attendanceFactor = paidDays > 0 ? Math.Clamp(paidDays / (decimal)daysInMonth, 0m, 1m) : 0m;
        decimal monthlyCtc = Math.Round(salary.AnnualCtc / 12m, 2);

        decimal basicMonthly = 0m;
        // First pass: resolve basic or flat amounts
        foreach (SalaryStructureComponent sc in salary.Structure.Components.OrderBy(c => c.Component.Kind))
        {
            decimal monthlyAmount = 0m;
            if (sc.ValueType == SalaryValueType.FlatAmount && sc.FlatAmount.HasValue)
            {
                monthlyAmount = sc.FlatAmount.Value;
            }
            else if (sc.ValueType == SalaryValueType.Percentage && sc.Percentage.HasValue)
            {
                monthlyAmount = Math.Round(monthlyCtc * (sc.Percentage.Value / 100m), 2);
            }
            else if (sc.ValueType == SalaryValueType.Formula && !string.IsNullOrWhiteSpace(sc.Component.Formula))
            {
                monthlyAmount = EvaluateFormula(sc.Component.Formula, monthlyCtc, basicMonthly);
            }
            else
            {
                // Default fallback: split evenly if not specified
                monthlyAmount = Math.Round(monthlyCtc * 0.4m, 2);
            }

            if (sc.Component.Name.Contains("Basic", StringComparison.OrdinalIgnoreCase))
            {
                basicMonthly = monthlyAmount;
            }

            // Prorate by attendance for earnings
            decimal finalAmount = sc.Component.Kind == ComponentKind.Earning
                ? Math.Round(monthlyAmount * attendanceFactor, 2)
                : Math.Round(monthlyAmount, 2);

            result.Lines.Add(new CalculatedLineData
            {
                SalaryComponentId = sc.SalaryComponentId,
                ComponentName = sc.Component.Name,
                Kind = sc.Component.Kind,
                Amount = finalAmount
            });
        }

        // Add OneTimePayments
        foreach (OneTimePayment otp in oneTimePayments)
        {
            result.Lines.Add(new CalculatedLineData
            {
                SalaryComponentId = otp.SalaryComponentId,
                ComponentName = otp.Component?.Name ?? "One-Time Adjustment",
                Kind = otp.Component?.Kind ?? ComponentKind.Earning,
                Amount = otp.Amount
            });
        }

        // Add Loan Repayments
        if (activeLoan != null && activeLoan.MonthlyEmi > 0)
        {
            decimal emi = Math.Min(activeLoan.MonthlyEmi, activeLoan.PrincipalAmount);
            result.Lines.Add(new CalculatedLineData
            {
                SalaryComponentId = 0, // Special loan deduction line
                ComponentName = "Loan EMI Recovery",
                Kind = ComponentKind.Deduction,
                Amount = emi
            });
        }

        // Calculate Arrears from back-dated salary revisions
        foreach (SalaryRevision rev in pendingArrearsRevisions)
        {
            if (rev.NewCtc > rev.PreviousCtc)
            {
                decimal monthlyDiff = (rev.NewCtc - rev.PreviousCtc) / 12m;
                // Arrears amount for the difference
                decimal arrearsAmount = Math.Round(monthlyDiff, 2);
                if (arrearsAmount > 0)
                {
                    result.Lines.Add(new CalculatedLineData
                    {
                        SalaryComponentId = 0,
                        ComponentName = "Salary Revision Arrears",
                        Kind = ComponentKind.Earning,
                        Amount = arrearsAmount
                    });
                }
            }
        }

        // Tally Gross Earnings and Gross Deductions
        result.GrossEarnings = result.Lines
            .Where(l => l.Kind == ComponentKind.Earning)
            .Sum(l => l.Amount);

        result.GrossDeductions = result.Lines
            .Where(l => l.Kind == ComponentKind.Deduction || l.Kind == ComponentKind.Statutory)
            .Sum(l => l.Amount);

        // If salary is on hold, NetPay is 0 (or withheld)
        if (isHeld)
        {
            result.NetPay = 0m;
        }
        else
        {
            result.NetPay = Math.Max(0m, result.GrossEarnings - result.GrossDeductions);
        }

        return result;
    }

    private static decimal EvaluateFormula(string formula, decimal monthlyCtc, decimal basic)
    {
        // Simple formula parser: e.g. "0.5 * BASIC", "0.4 * CTC", "BASIC * 0.12"
        string f = formula.Trim().ToUpperInvariant();
        decimal multiplier = 1m;
        decimal baseVal = monthlyCtc;

        if (f.Contains("BASIC"))
        {
            baseVal = basic > 0 ? basic : monthlyCtc * 0.4m;
        }
        else if (f.Contains("CTC"))
        {
            baseVal = monthlyCtc;
        }

        string cleaned = f.Replace("BASIC", "").Replace("CTC", "").Replace("*", "").Trim();
        if (decimal.TryParse(cleaned, out decimal parsed))
        {
            multiplier = parsed;
        }

        return Math.Round(baseVal * multiplier, 2);
    }
}
