using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Api.Services;

/// <summary>
/// GST rates. Two things make this more than CRUD: rates are effective-dated,
/// so a change supersedes rather than overwrites; and every rate owns up to six
/// GST sub-accounts, provisioned automatically.
/// </summary>
public sealed class TaxMasterService
{
    private readonly AccountingDbContext _db;
    private readonly SubAccountService _subAccounts;
    private readonly TimeProvider _clock;

    public TaxMasterService(
        AccountingDbContext db, SubAccountService subAccounts, TimeProvider clock)
    {
        _db = db;
        _subAccounts = subAccounts;
        _clock = clock;
    }

    /// <summary>
    /// Current rates by default. History shows every superseded version, which
    /// is what a document dated in the past needs to resolve against.
    /// </summary>
    public async Task<IReadOnlyList<TaxMasterListItem>> ListAsync(
        bool includeHistory, bool includeInactive, CancellationToken ct)
    {
        IQueryable<TaxMaster> query = _db.TaxMasters;

        if (!includeHistory)
        {
            query = query.Where(t => t.EffectiveTo == null);
        }

        if (!includeInactive)
        {
            query = query.Where(t => t.IsActive);
        }

        List<TaxMaster> rows = await query
            .OrderBy(t => t.TotalRate)
            .ThenByDescending(t => t.EffectiveFrom)
            .ToListAsync(ct);

        return rows.Select(Map).ToList();
    }

    /// <summary>The rate in force for a group on a given date — what a document must use.</summary>
    public async Task<TaxMasterListItem?> ResolveAsync(
        long taxGroupId, DateOnly onDate, CancellationToken ct)
    {
        TaxMaster? row = await _db.TaxMasters
            .Where(t => t.TaxGroupId == taxGroupId
                && t.IsActive
                && t.EffectiveFrom <= onDate
                && (t.EffectiveTo == null || t.EffectiveTo >= onDate))
            .OrderByDescending(t => t.EffectiveFrom)
            .FirstOrDefaultAsync(ct);

        return row is null ? null : Map(row);
    }

    public async Task<TaxMasterListItem?> GetAsync(long taxMasterId, CancellationToken ct)
    {
        TaxMaster? row = await _db.TaxMasters.FirstOrDefaultAsync(t => t.TaxMasterId == taxMasterId, ct);
        return row is null ? null : Map(row);
    }

    public async Task<SaveTaxResult> CreateAsync(SaveTaxMasterRequest request, CancellationToken ct)
    {
        if (!request.IsSales && !request.IsPurchase)
        {
            return new SaveTaxResult(SaveTaxOutcome.NoApplicability, null);
        }

        var rate = new TaxMaster
        {
            TaxSystemName = null,
            TaxName = request.TaxName,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = null,
            IsSales = request.IsSales,
            IsPurchase = request.IsPurchase,
            IsActive = request.IsActive,
            CessRate = request.CessRate,
        };
        ApplySplit(rate, request.TotalRate);

        _db.TaxMasters.Add(rate);
        await _db.SaveChangesAsync(ct);

        // The group id is the first version's own id, so every later revision
        // can inherit it and keep one continuous sub-ledger.
        rate.TaxGroupId = rate.TaxMasterId;
        await _db.SaveChangesAsync(ct);

        await ProvisionSubAccountsAsync(rate, ct);
        return new SaveTaxResult(SaveTaxOutcome.Ok, rate.TaxMasterId);
    }

    /// <summary>
    /// Renames a rate. Display-only, so it is allowed on seeded rates too — the
    /// hidden TaxSystemName remains what code matches on.
    /// </summary>
    public async Task<SaveTaxOutcome> RenameAsync(
        long taxMasterId, string taxName, CancellationToken ct)
    {
        TaxMaster? rate = await _db.TaxMasters.FirstOrDefaultAsync(t => t.TaxMasterId == taxMasterId, ct);
        if (rate is null)
        {
            return SaveTaxOutcome.NotFound;
        }

        rate.TaxName = taxName;
        await _db.SaveChangesAsync(ct);

        // Sub-account labels carry the display name, so keep them in step.
        await ProvisionSubAccountsAsync(rate, ct);
        return SaveTaxOutcome.Ok;
    }

    /// <summary>
    /// Revises a rate: closes the current version the day before the new one
    /// starts and inserts a successor sharing its group id. Never an in-place
    /// edit — a document dated before the change must still resolve the old rate.
    /// </summary>
    public async Task<SaveTaxResult> ReviseAsync(
        long taxMasterId, SaveTaxMasterRequest request, CancellationToken ct)
    {
        if (!request.IsSales && !request.IsPurchase)
        {
            return new SaveTaxResult(SaveTaxOutcome.NoApplicability, null);
        }

        TaxMaster? current = await _db.TaxMasters.FirstOrDefaultAsync(t => t.TaxMasterId == taxMasterId, ct);
        if (current is null)
        {
            return new SaveTaxResult(SaveTaxOutcome.NotFound, null);
        }

        if (current.EffectiveTo is not null)
        {
            return new SaveTaxResult(SaveTaxOutcome.NotCurrentVersion, null);
        }

        if (request.EffectiveFrom <= current.EffectiveFrom)
        {
            return new SaveTaxResult(SaveTaxOutcome.EffectiveDateNotAfter, null);
        }

        current.EffectiveTo = request.EffectiveFrom.AddDays(-1);

        var successor = new TaxMaster
        {
            TaxGroupId = current.TaxGroupId,
            // A revision of a seeded rate keeps its canonical name.
            TaxSystemName = current.TaxSystemName,
            TaxName = request.TaxName,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = null,
            IsSales = request.IsSales,
            IsPurchase = request.IsPurchase,
            IsActive = request.IsActive,
            CessRate = request.CessRate,
        };
        ApplySplit(successor, request.TotalRate);

        _db.TaxMasters.Add(successor);
        await _db.SaveChangesAsync(ct);

        // Sub-accounts key on the group, so the revision reuses them — but the
        // applicability flags may have changed, which can add a direction.
        await ProvisionSubAccountsAsync(successor, ct);
        return new SaveTaxResult(SaveTaxOutcome.Ok, successor.TaxMasterId);
    }

    /// <summary>Deactivates a rate and its sub-accounts. History is never deleted.</summary>
    public async Task<SaveTaxOutcome> DeactivateAsync(long taxMasterId, CancellationToken ct)
    {
        TaxMaster? rate = await _db.TaxMasters.FirstOrDefaultAsync(t => t.TaxMasterId == taxMasterId, ct);
        if (rate is null)
        {
            return SaveTaxOutcome.NotFound;
        }

        rate.IsActive = false;
        await _db.SaveChangesAsync(ct);

        await _subAccounts.DeactivateAsync(SubAccountReferenceType.Tax, rate.TaxGroupId, ct);
        return SaveTaxOutcome.Ok;
    }

    /// <summary>Writes the six default GST rates for a newly created organization.</summary>
    public async Task<int> SeedForOrganizationAsync(Guid orgId, CancellationToken ct)
    {
        if (await _db.TaxMasters.IgnoreQueryFilters().AnyAsync(t => t.OrgId == orgId, ct))
        {
            return 0;
        }

        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        IReadOnlyList<TaxMaster> seed = Repository.SeedData.TaxMasterSeed.Build(orgId, today);
        _db.TaxMasters.AddRange(seed);
        await _db.SaveChangesAsync(ct);

        foreach (TaxMaster rate in seed)
        {
            rate.TaxGroupId = rate.TaxMasterId;
        }

        await _db.SaveChangesAsync(ct);

        foreach (TaxMaster rate in seed)
        {
            await ProvisionSubAccountsAsync(rate, ct);
        }

        return seed.Count;
    }

    /// <summary>
    /// CGST and SGST are half the total each, IGST is the whole total. Derived
    /// rather than accepted, so the split invariant cannot drift.
    /// </summary>
    private static void ApplySplit(TaxMaster rate, decimal totalRate)
    {
        rate.TotalRate = totalRate;
        rate.CgstRate = totalRate / 2m;
        rate.SgstRate = totalRate / 2m;
        rate.IgstRate = totalRate;
    }

    /// <summary>
    /// Up to six sub-accounts per rate — CGST, SGST and IGST beneath Input GST
    /// when it applies to purchases and Output GST when it applies to sales.
    /// Keyed on the group id so a revision reuses the existing sub-ledger.
    /// </summary>
    private Task ProvisionSubAccountsAsync(TaxMaster rate, CancellationToken ct) =>
        _subAccounts.ProvisionAsync(
            new ProvisionSubAccountsRequest
            {
                ReferenceType = SubAccountReferenceType.Tax,
                ReferenceId = rate.TaxGroupId,
                Name = rate.TaxName,
                ForSales = rate.IsSales,
                ForPurchase = rate.IsPurchase,
            },
            ct);

    private static TaxMasterListItem Map(TaxMaster t) => new()
    {
        TaxMasterId = t.TaxMasterId,
        TaxGroupId = t.TaxGroupId,
        TaxSystemName = t.TaxSystemName,
        TaxName = t.TaxName,
        TotalRate = t.TotalRate,
        CgstRate = t.CgstRate,
        SgstRate = t.SgstRate,
        IgstRate = t.IgstRate,
        CessRate = t.CessRate,
        EffectiveFrom = t.EffectiveFrom,
        EffectiveTo = t.EffectiveTo,
        IsSales = t.IsSales,
        IsPurchase = t.IsPurchase,
        IsActive = t.IsActive,
        IsCurrent = t.EffectiveTo is null,
        IsSystemRate = t.TaxSystemName is not null,
    };
}

public enum SaveTaxOutcome
{
    Ok = 1,
    NotFound = 2,
    NoApplicability = 3,
    NotCurrentVersion = 4,
    EffectiveDateNotAfter = 5,
}

public sealed record SaveTaxResult(SaveTaxOutcome Outcome, long? TaxMasterId);
