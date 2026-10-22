using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository.SeedData;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Apps;

namespace Master.Repository.Repositories;

/// <summary>
/// The prices of the paid plans (D-71, TK-211): seeded once, read by the Plan
/// screen, and edited by an operator in the table.
/// </summary>
public sealed class PlanPriceRepository
{
    private readonly AdminDbContext _db;

    public PlanPriceRepository(AdminDbContext db) => _db = db;

    /// <summary>
    /// Adds every price in <see cref="PlanPriceSeed"/> that the table does not
    /// have, at every start. Idempotent, and it never touches a row that is
    /// there: a price an operator changed stays as they set it.
    /// </summary>
    public static async Task EnsureSeededAsync(AdminDbContext db, CancellationToken ct)
    {
        List<PlanPrice> have = await db.PlanPrices.AsNoTracking().ToListAsync(ct);

        HashSet<(BusinessType, LicenseType, BillingCycle)> keys =
            [.. have.Select(p => (p.BusinessType, p.Plan, p.BillingCycle))];

        List<PlanPrice> missing =
            [.. PlanPriceSeed.Build().Where(p => !keys.Contains((p.BusinessType, p.Plan, p.BillingCycle)))];
        if (missing.Count == 0)
        {
            return;
        }

        db.PlanPrices.AddRange(missing);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>What a paid plan allows, read from the table a licence is given its limits from.</summary>
    private static PlanTierInfo Tier(LicenseType type)
    {
        PlanCatalogRepository.PlanLimits limits = PlanCatalogRepository.DefaultsFor(type);
        return new PlanTierInfo
        {
            Plan = PlanCatalogRepository.Name(type),
            LicenseType = type,
            MaxUsers = limits.MaxUsers,
            MonthlySalesTransactionLimit = limits.MonthlySalesTransactions,
            MonthlyPurchaseTransactionLimit = limits.MonthlyPurchaseTransactions,
        };
    }

    /// <summary>The prices for the trade of one branch: the signed-in one, named by the token.</summary>
    public async Task<PlanPricesResponse> GetForBranchAsync(Guid orgId, CancellationToken ct)
    {
        BusinessType? type = await _db.Organizations.AsNoTracking()
            .Where(o => o.OrgId == orgId)
            .Select(o => o.BusinessType)
            .FirstOrDefaultAsync(ct);

        return await GetAsync(type, ct);
    }

    /// <summary>
    /// Both paid plans for each billing cycle, for one trade. A branch with no
    /// trade, or a trade nobody has priced, gets no cycles: its price is agreed
    /// with the platform's team, and the screen says so.
    /// </summary>
    public async Task<PlanPricesResponse> GetAsync(BusinessType? businessType, CancellationToken ct)
    {
        var response = new PlanPricesResponse
        {
            BusinessType = businessType?.ToString(),
            Tiers = [Tier(LicenseType.Pro), Tier(LicenseType.Elite)],
        };
        if (businessType is null)
        {
            return response;
        }

        List<PlanPrice> rows = await _db.PlanPrices.AsNoTracking()
            .Where(p => p.BusinessType == businessType && p.IsActive)
            .ToListAsync(ct);

        foreach (BillingCycle cycle in Enum.GetValues<BillingCycle>())
        {
            decimal? professional = rows.FirstOrDefault(p => p.Plan == LicenseType.Pro && p.BillingCycle == cycle)?.Amount;
            decimal? elite = rows.FirstOrDefault(p => p.Plan == LicenseType.Elite && p.BillingCycle == cycle)?.Amount;

            // A cycle is shown only when both plans have a price in it: a half
            // comparison would be worse than none.
            if (professional is decimal pro && elite is decimal top)
            {
                response.Cycles.Add(new PlanCyclePrice
                {
                    Cycle = cycle,
                    Months = PlanPriceSeed.Months(cycle),
                    Professional = pro,
                    Elite = top,
                });
            }
        }

        response.CurrencyCode = rows.Select(p => p.CurrencyCode).FirstOrDefault() ?? "INR";
        return response;
    }
}
