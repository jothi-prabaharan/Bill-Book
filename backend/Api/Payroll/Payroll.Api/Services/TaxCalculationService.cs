using Microsoft.EntityFrameworkCore;
using Payroll.Entity.Enums;
using Payroll.Entity.Models;
using Payroll.Entity.TableEntities;
using Payroll.Repository;

namespace Payroll.Api.Services;

public sealed class TaxComputationResult
{
    public long EmployeeId { get; set; }
    public string FinancialYear { get; set; } = "2026-2027";
    public string Regime { get; set; } = "New";
    public decimal GrossSalary { get; set; }
    public decimal PreviousEmployerGross { get; set; }
    public decimal TotalGrossIncome { get; set; }
    public decimal StandardDeduction { get; set; }
    public decimal Section80CDeduction { get; set; }
    public decimal OtherDeductions { get; set; }
    public decimal TotalTaxableIncome { get; set; }
    public decimal TaxBeforeCess { get; set; }
    public decimal Rebate87A { get; set; }
    public decimal CessAmount { get; set; }
    public decimal TotalAnnualTax { get; set; }
    public decimal PreviousEmployerTds { get; set; }
    public decimal NetTaxPayable { get; set; }
    public decimal MonthlyTds { get; set; }
}

public sealed class TaxCalculationService
{
    private readonly PayrollDbContext _db;

    public TaxCalculationService(PayrollDbContext db) => _db = db;

    public async Task<TaxDeclaration?> GetDeclarationAsync(long employeeId, string financialYear, CancellationToken ct) =>
        await _db.TaxDeclarations.AsNoTracking()
            .Include(d => d.Lines)
            .Include(d => d.RentDetails)
            .FirstOrDefaultAsync(d => d.EmployeeId == employeeId && d.FinancialYear == financialYear, ct);

    public async Task<PreviousEmployerIncome?> GetPreviousEmployerIncomeAsync(long employeeId, string financialYear, CancellationToken ct) =>
        await _db.PreviousEmployerIncomes.AsNoTracking()
            .FirstOrDefaultAsync(p => p.EmployeeId == employeeId && p.FinancialYear == financialYear, ct);

    public async Task<List<TaxSlab>> GetTaxSlabsAsync(string financialYear, string regime, CancellationToken ct) =>
        await _db.TaxSlabs.AsNoTracking()
            .Where(s => s.FinancialYear == financialYear && s.Regime == regime)
            .OrderBy(s => s.MinIncome)
            .ToListAsync(ct);

    public async Task<TaxComputationResult> ComputeTaxAsync(
        long employeeId,
        string financialYear,
        decimal currentAnnualGross,
        int remainingMonths = 12,
        CancellationToken ct = default)
    {
        var decl = await _db.TaxDeclarations.AsNoTracking()
            .Include(d => d.Lines)
            .Include(d => d.RentDetails)
            .FirstOrDefaultAsync(d => d.EmployeeId == employeeId && d.FinancialYear == financialYear, ct);

        string regime = decl?.Regime ?? "New";

        var prevIncome = await _db.PreviousEmployerIncomes.AsNoTracking()
            .FirstOrDefaultAsync(p => p.EmployeeId == employeeId && p.FinancialYear == financialYear, ct);

        decimal prevGross = prevIncome?.GrossIncome ?? 0m;
        decimal prevTds = prevIncome?.TotalTdsDeducted ?? 0m;
        decimal totalGross = currentAnnualGross + prevGross;

        // Standard deduction
        decimal stdDeduction = regime == "New" ? 75000m : 50000m;

        decimal sec80C = 0m;
        decimal otherDeductions = 0m;

        if (regime == "Old" && decl != null)
        {
            foreach (var line in decl.Lines)
            {
                decimal amount = line.VerifiedAmount > 0 ? line.VerifiedAmount : (line.ProofAmount > 0 ? line.ProofAmount : line.DeclaredAmount);
                if (line.Section == "80C")
                {
                    sec80C = Math.Min(150000m, sec80C + amount);
                }
                else
                {
                    otherDeductions += amount;
                }
            }
        }

        decimal totalDeductions = stdDeduction + sec80C + otherDeductions;
        decimal taxableIncome = Math.Max(0m, totalGross - totalDeductions);

        // Fetch tax slabs
        var slabs = await _db.TaxSlabs.AsNoTracking()
            .Where(s => s.FinancialYear == financialYear && s.Regime == regime)
            .OrderBy(s => s.MinIncome)
            .ToListAsync(ct);

        decimal taxBeforeCess = 0m;
        foreach (var slab in slabs)
        {
            if (taxableIncome > slab.MinIncome)
            {
                decimal taxableInSlab = Math.Min(taxableIncome, slab.MaxIncome) - slab.MinIncome;
                taxBeforeCess += Math.Round(taxableInSlab * (slab.TaxRate / 100m), 2);
            }
        }

        // Rebate u/s 87A:
        // New regime: rebate if taxable income <= 7,00,000 (tax is 0)
        // Old regime: rebate if taxable income <= 5,00,000 (tax is 0)
        decimal rebate = 0m;
        if (regime == "New" && taxableIncome <= 700000m)
        {
            rebate = taxBeforeCess;
            taxBeforeCess = 0m;
        }
        else if (regime == "Old" && taxableIncome <= 500000m)
        {
            rebate = taxBeforeCess;
            taxBeforeCess = 0m;
        }

        // Health & Education Cess = 4%
        decimal cess = Math.Round(taxBeforeCess * 0.04m, 2);
        decimal totalTax = taxBeforeCess + cess;

        // Deduct previous employer TDS
        decimal netTaxPayable = Math.Max(0m, totalTax - prevTds);
        decimal monthlyTds = remainingMonths > 0 ? Math.Round(netTaxPayable / remainingMonths, 2) : netTaxPayable;

        return new TaxComputationResult
        {
            EmployeeId = employeeId,
            FinancialYear = financialYear,
            Regime = regime,
            GrossSalary = currentAnnualGross,
            PreviousEmployerGross = prevGross,
            TotalGrossIncome = totalGross,
            StandardDeduction = stdDeduction,
            Section80CDeduction = sec80C,
            OtherDeductions = otherDeductions,
            TotalTaxableIncome = taxableIncome,
            TaxBeforeCess = taxBeforeCess,
            Rebate87A = rebate,
            CessAmount = cess,
            TotalAnnualTax = totalTax,
            PreviousEmployerTds = prevTds,
            NetTaxPayable = netTaxPayable,
            MonthlyTds = monthlyTds
        };
    }

    public async Task<long> SavePreviousEmployerIncomeAsync(SavePreviousEmployerIncomeRequest req, CancellationToken ct)
    {
        var existing = await _db.PreviousEmployerIncomes
            .FirstOrDefaultAsync(p => p.EmployeeId == req.EmployeeId && p.FinancialYear == req.FinancialYear, ct);

        if (existing is null)
        {
            var entity = new PreviousEmployerIncome
            {
                EmployeeId = req.EmployeeId,
                FinancialYear = req.FinancialYear,
                GrossIncome = req.GrossIncome,
                Exemptions = req.Exemptions,
                ProfessionalTax = req.ProfessionalTax,
                ProvidentFund = req.ProvidentFund,
                TotalTdsDeducted = req.TotalTdsDeducted,
                EmployerName = req.EmployerName
            };
            _db.PreviousEmployerIncomes.Add(entity);
            await _db.SaveChangesAsync(ct);
            return entity.PreviousEmployerIncomeId;
        }

        existing.GrossIncome = req.GrossIncome;
        existing.Exemptions = req.Exemptions;
        existing.ProfessionalTax = req.ProfessionalTax;
        existing.ProvidentFund = req.ProvidentFund;
        existing.TotalTdsDeducted = req.TotalTdsDeducted;
        existing.EmployerName = req.EmployerName;

        await _db.SaveChangesAsync(ct);
        return existing.PreviousEmployerIncomeId;
    }

    public async Task<long> SaveDeclarationAsync(SaveTaxDeclarationRequest req, CancellationToken ct)
    {
        var existing = await _db.TaxDeclarations
            .Include(d => d.Lines)
            .Include(d => d.RentDetails)
            .FirstOrDefaultAsync(d => d.EmployeeId == req.EmployeeId && d.FinancialYear == req.FinancialYear, ct);

        if (existing != null && existing.IsLocked)
        {
            throw new InvalidOperationException("Tax declaration is locked and cannot be edited.");
        }

        var lines = req.Lines.Select(l => new TaxDeclarationLine
        {
            Section = l.Section,
            DeclaredAmount = l.DeclaredAmount,
            ProofAmount = l.ProofAmount,
            VerifiedAmount = l.VerifiedAmount,
            Remarks = l.Remarks
        }).ToList();

        var rentDetails = req.RentDetails.Select(r => new RentDetail
        {
            Month = r.Month,
            RentAmount = r.RentAmount,
            LandlordPan = r.LandlordPan,
            LandlordName = r.LandlordName,
            LandlordAddress = r.LandlordAddress
        }).ToList();

        if (existing is null)
        {
            var decl = new TaxDeclaration
            {
                EmployeeId = req.EmployeeId,
                FinancialYear = req.FinancialYear,
                Regime = req.Regime,
                SubmittedAt = DateTimeOffset.UtcNow,
                Lines = lines,
                RentDetails = rentDetails
            };
            _db.TaxDeclarations.Add(decl);
            await _db.SaveChangesAsync(ct);
            return decl.TaxDeclarationId;
        }

        existing.Regime = req.Regime;
        existing.SubmittedAt = DateTimeOffset.UtcNow;
        _db.TaxDeclarationLines.RemoveRange(existing.Lines);
        _db.RentDetails.RemoveRange(existing.RentDetails);
        existing.Lines = lines;
        existing.RentDetails = rentDetails;

        await _db.SaveChangesAsync(ct);
        return existing.TaxDeclarationId;
    }

    public async Task SetDeclarationLockAsync(long declarationId, bool isLocked, CancellationToken ct)
    {
        var decl = await _db.TaxDeclarations.FirstOrDefaultAsync(d => d.TaxDeclarationId == declarationId, ct)
            ?? throw new KeyNotFoundException($"Tax declaration {declarationId} not found.");

        decl.IsLocked = isLocked;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Form16PartBModel> GenerateForm16PartBAsync(long employeeId, string financialYear, CancellationToken ct)
    {
        // 1. Fetch all payslips for this employee across runs for this financial year
        var payslips = await _db.Payslips.AsNoTracking()
            .Include(p => p.Run)
            .Where(p => p.EmployeeId == employeeId && p.Run.Status == PayrollRunStatus.Posted)
            .ToListAsync(ct);

        decimal currentGross = payslips.Sum(p => p.GrossEarnings);
        decimal tdsDeducted = payslips.Sum(p => p.GrossDeductions);

        var comp = await ComputeTaxAsync(employeeId, financialYear, currentGross, remainingMonths: 0, ct);

        return new Form16PartBModel
        {
            EmployeeId = employeeId,
            FinancialYear = financialYear,
            Regime = comp.Regime,
            GrossSalary = comp.TotalGrossIncome,
            TotalExemptions = 0m,
            StandardDeduction = comp.StandardDeduction,
            ChapterVIADeductions = comp.Section80CDeduction + comp.OtherDeductions,
            TotalTaxableIncome = comp.TotalTaxableIncome,
            TaxPayable = comp.TaxBeforeCess,
            Relief87A = comp.Rebate87A,
            Cess = comp.CessAmount,
            TotalTax = comp.TotalAnnualTax,
            TdsDeducted = tdsDeducted + comp.PreviousEmployerTds
        };
    }

    public async Task<Form12BaModel> GenerateForm12BaAsync(long employeeId, string financialYear, CancellationToken ct)
    {
        return new Form12BaModel
        {
            EmployeeId = employeeId,
            FinancialYear = financialYear,
            Perquisites = [],
            TotalPerquisites = 0m
        };
    }

    public async Task<Quarter24QDataModel> Generate24QDataAsync(string financialYear, int quarter, CancellationToken ct)
    {
        // Quarter 1: Apr, May, Jun (months 4, 5, 6)
        // Quarter 2: Jul, Aug, Sep (months 7, 8, 9)
        // Quarter 3: Oct, Nov, Dec (months 10, 11, 12)
        // Quarter 4: Jan, Feb, Mar (months 1, 2, 3)
        List<int> months = quarter switch
        {
            1 => [4, 5, 6],
            2 => [7, 8, 9],
            3 => [10, 11, 12],
            4 => [1, 2, 3],
            _ => throw new ArgumentOutOfRangeException(nameof(quarter), "Quarter must be between 1 and 4.")
        };

        var payslips = await _db.Payslips.AsNoTracking()
            .Include(p => p.Run)
            .Where(p => p.Run.Status == PayrollRunStatus.Posted && months.Contains(p.Run.Month.Month))
            .ToListAsync(ct);

        var grouped = payslips.GroupBy(p => p.EmployeeId).Select(g => new Quarter24QEmployeeLine
        {
            EmployeeId = g.Key,
            Pan = string.Empty,
            TotalAmountCredited = g.Sum(x => x.GrossEarnings),
            TotalTdsDeducted = g.Sum(x => x.GrossDeductions),
            TotalTdsDeposited = g.Sum(x => x.GrossDeductions),
            DateOfPayment = DateTime.UtcNow.ToString("yyyy-MM-dd")
        }).ToList();

        return new Quarter24QDataModel
        {
            FinancialYear = financialYear,
            Quarter = quarter,
            EmployeeLines = grouped
        };
    }
}
