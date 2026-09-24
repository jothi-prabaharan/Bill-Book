using System.Text;
using Microsoft.EntityFrameworkCore;
using Payroll.Entity.Enums;
using Payroll.Entity.Models;
using Payroll.Entity.TableEntities;
using Payroll.Repository;

namespace Payroll.Api.Services;

public sealed class StatutoryService
{
    private readonly PayrollDbContext _db;

    public StatutoryService(PayrollDbContext db) => _db = db;

    // ---- PF Settings --------------------------------------------------------

    public async Task<PfSettingView?> GetPfSettingAsync(CancellationToken ct)
    {
        PfSetting? s = await _db.PfSettings.AsNoTracking().OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync(ct);
        return s is null ? null : new PfSettingView
        {
            PfSettingId = s.PfSettingId,
            EffectiveFrom = s.EffectiveFrom,
            EffectiveTo = s.EffectiveTo,
            EmployeeContributionRate = s.EmployeeContributionRate,
            EmployerContributionRate = s.EmployerContributionRate,
            WageCeiling = s.WageCeiling,
            RestrictToWageCeiling = s.RestrictToWageCeiling,
            AdminChargesRate = s.AdminChargesRate,
            EdliRate = s.EdliRate
        };
    }

    public async Task<long> SavePfSettingAsync(SavePfSettingRequest request, CancellationToken ct)
    {
        var setting = new PfSetting
        {
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            EmployeeContributionRate = request.EmployeeContributionRate,
            EmployerContributionRate = request.EmployerContributionRate,
            WageCeiling = request.WageCeiling,
            RestrictToWageCeiling = request.RestrictToWageCeiling,
            AdminChargesRate = request.AdminChargesRate,
            EdliRate = request.EdliRate
        };
        _db.PfSettings.Add(setting);
        await _db.SaveChangesAsync(ct);
        return setting.PfSettingId;
    }

    // ---- ESI Settings -------------------------------------------------------

    public async Task<EsiSettingView?> GetEsiSettingAsync(CancellationToken ct)
    {
        EsiSetting? s = await _db.EsiSettings.AsNoTracking().OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync(ct);
        return s is null ? null : new EsiSettingView
        {
            EsiSettingId = s.EsiSettingId,
            EffectiveFrom = s.EffectiveFrom,
            EffectiveTo = s.EffectiveTo,
            EmployeeContributionRate = s.EmployeeContributionRate,
            EmployerContributionRate = s.EmployerContributionRate,
            WageCeiling = s.WageCeiling
        };
    }

    public async Task<long> SaveEsiSettingAsync(SaveEsiSettingRequest request, CancellationToken ct)
    {
        var setting = new EsiSetting
        {
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            EmployeeContributionRate = request.EmployeeContributionRate,
            EmployerContributionRate = request.EmployerContributionRate,
            WageCeiling = request.WageCeiling
        };
        _db.EsiSettings.Add(setting);
        await _db.SaveChangesAsync(ct);
        return setting.EsiSettingId;
    }

    // ---- Professional Tax Slabs ---------------------------------------------

    public async Task<IReadOnlyList<ProfessionalTaxSlabView>> ListPtSlabsAsync(int? stateId, CancellationToken ct)
    {
        var query = _db.ProfessionalTaxSlabs.AsNoTracking().AsQueryable();
        if (stateId.HasValue) query = query.Where(x => x.StateId == stateId.Value);

        return await query.OrderBy(x => x.StateId).ThenBy(x => x.MinSalary)
            .Select(s => new ProfessionalTaxSlabView
            {
                ProfessionalTaxSlabId = s.ProfessionalTaxSlabId,
                StateId = s.StateId,
                EffectiveFrom = s.EffectiveFrom,
                EffectiveTo = s.EffectiveTo,
                MinSalary = s.MinSalary,
                MaxSalary = s.MaxSalary,
                MonthlyTax = s.MonthlyTax,
                Month12Tax = s.Month12Tax
            }).ToListAsync(ct);
    }

    public async Task<long> SavePtSlabAsync(SaveProfessionalTaxSlabRequest request, CancellationToken ct)
    {
        var slab = new ProfessionalTaxSlab
        {
            StateId = request.StateId,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            MinSalary = request.MinSalary,
            MaxSalary = request.MaxSalary,
            MonthlyTax = request.MonthlyTax,
            Month12Tax = request.Month12Tax
        };
        _db.ProfessionalTaxSlabs.Add(slab);
        await _db.SaveChangesAsync(ct);
        return slab.ProfessionalTaxSlabId;
    }

    // ---- Returns & Files Generator ------------------------------------------

    public async Task<string> GeneratePfEcrAsync(long runId, CancellationToken ct)
    {
        PayrollRun? run = await _db.PayrollRuns.AsNoTracking()
            .Include(r => r.Payslips).ThenInclude(p => p.Lines)
            .FirstOrDefaultAsync(r => r.PayrollRunId == runId, ct)
            ?? throw new KeyNotFoundException($"Payroll run {runId} not found.");

        PfSetting? pf = await _db.PfSettings.AsNoTracking()
            .Where(x => x.EffectiveFrom <= run.Month)
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(ct) ?? new PfSetting();

        var sb = new StringBuilder();
        // ECR format standard fields:
        // UAN#~#MEMBER_NAME#~#GROSS_WAGES#~#EPF_WAGES#~#EPS_WAGES#~#EDLI_WAGES#~#EE_SHARE#~#EPS_SHARE#~#ER_SHARE_DIFF#~#NCP_DAYS#~#REFUND
        foreach (Payslip slip in run.Payslips)
        {
            decimal gross = slip.GrossEarnings;
            decimal basic = slip.Lines
                .Where(l => l.Kind == ComponentKind.Earning)
                .Sum(l => l.Amount);

            decimal epfWages = pf.RestrictToWageCeiling ? Math.Min(basic, pf.WageCeiling) : basic;
            decimal epsWages = Math.Min(epfWages, pf.WageCeiling);
            decimal edliWages = epsWages;

            // EE Share = 12% of EPF wages
            decimal eeShare = Math.Round(epfWages * (pf.EmployeeContributionRate / 100m), 0);

            // EPS Share = 8.33% of EPS wages (max 1250)
            decimal epsShare = Math.Round(epsWages * 0.0833m, 0);

            // ER Share difference = 3.67% of EPF wages = Total 12% - EPS share
            decimal erShareDiff = Math.Max(0m, eeShare - epsShare);

            decimal ncpDays = Math.Max(0m, 30m - slip.PaidDays);

            string uan = $"100{slip.EmployeeId:D9}";
            string name = $"EMPLOYEE_{slip.EmployeeId}";

            sb.AppendLine($"#~#{uan}#~#{name}#~#{gross:F0}#~#{epfWages:F0}#~#{epsWages:F0}#~#{edliWages:F0}#~#{eeShare:F0}#~#{epsShare:F0}#~#{erShareDiff:F0}#~#{ncpDays:F0}#~#0");
        }

        string fileContent = sb.ToString();

        // Save statutory return record
        _db.StatutoryReturns.Add(new StatutoryReturn
        {
            Month = run.Month,
            ReturnType = "PF_ECR",
            FileContent = fileContent,
            GeneratedAt = DateTimeOffset.UtcNow,
            Status = "Generated"
        });
        await _db.SaveChangesAsync(ct);

        return fileContent;
    }

    public async Task<string> GenerateEsiReturnAsync(long runId, CancellationToken ct)
    {
        PayrollRun? run = await _db.PayrollRuns.AsNoTracking()
            .Include(r => r.Payslips)
            .FirstOrDefaultAsync(r => r.PayrollRunId == runId, ct)
            ?? throw new KeyNotFoundException($"Payroll run {runId} not found.");

        EsiSetting? esi = await _db.EsiSettings.AsNoTracking()
            .Where(x => x.EffectiveFrom <= run.Month)
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(ct) ?? new EsiSetting();

        var sb = new StringBuilder();
        sb.AppendLine("IP Number,IP Name,No of Days,Total Monthly Wages,IP Contribution,Employer Contribution");

        foreach (Payslip slip in run.Payslips)
        {
            if (slip.GrossEarnings <= esi.WageCeiling)
            {
                decimal ipContribution = Math.Ceiling(slip.GrossEarnings * (esi.EmployeeContributionRate / 100m));
                decimal erContribution = Math.Ceiling(slip.GrossEarnings * (esi.EmployerContributionRate / 100m));
                string ipNumber = $"3100{slip.EmployeeId:D6}";
                string name = $"EMPLOYEE_{slip.EmployeeId}";

                sb.AppendLine($"{ipNumber},{name},{slip.PaidDays:F0},{slip.GrossEarnings:F2},{ipContribution:F2},{erContribution:F2}");
            }
        }

        string fileContent = sb.ToString();

        _db.StatutoryReturns.Add(new StatutoryReturn
        {
            Month = run.Month,
            ReturnType = "ESI",
            FileContent = fileContent,
            GeneratedAt = DateTimeOffset.UtcNow,
            Status = "Generated"
        });
        await _db.SaveChangesAsync(ct);

        return fileContent;
    }

    public async Task<string> GeneratePtChallanAsync(long runId, int stateId, CancellationToken ct)
    {
        PayrollRun? run = await _db.PayrollRuns.AsNoTracking()
            .Include(r => r.Payslips)
            .FirstOrDefaultAsync(r => r.PayrollRunId == runId, ct)
            ?? throw new KeyNotFoundException($"Payroll run {runId} not found.");

        var slabs = await _db.ProfessionalTaxSlabs.AsNoTracking()
            .Where(s => s.StateId == stateId && s.EffectiveFrom <= run.Month)
            .ToListAsync(ct);

        decimal totalPt = 0m;
        int eligibleEmployees = 0;

        foreach (var slip in run.Payslips)
        {
            var matching = slabs.FirstOrDefault(s => slip.GrossEarnings >= s.MinSalary && slip.GrossEarnings <= s.MaxSalary);
            if (matching != null && matching.MonthlyTax > 0)
            {
                totalPt += matching.MonthlyTax;
                eligibleEmployees++;
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Professional Tax Challan - State: {stateId}");
        sb.AppendLine($"Month: {run.Month:MMMM yyyy}");
        sb.AppendLine($"Total Employees: {eligibleEmployees}");
        sb.AppendLine($"Total Tax Deducted: ₹{totalPt:F2}");

        return sb.ToString();
    }
}
