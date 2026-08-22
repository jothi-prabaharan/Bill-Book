using Microsoft.EntityFrameworkCore;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;

namespace Master.Api.Services;

public sealed class OrgCurrencyService
{
    private readonly AdminDbContext _db;
    private readonly IMasterCurrencies _master;

    public OrgCurrencyService(AdminDbContext db, IMasterCurrencies master)
    {
        _db = db;
        _master = master;
    }

    /// <summary>The org's currencies. Active only by default — the list page shows those.</summary>
    public async Task<IReadOnlyList<OrgCurrencyDto>> ListAsync(
        Guid orgId, bool includeInactive, CancellationToken ct)
    {
        IQueryable<OrgCurrency> query = _db.OrgCurrencies.Where(c => c.OrgId == orgId);
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        List<OrgCurrency> rows = await query.ToListAsync(ct);
        IReadOnlyList<MasterCurrency> master = await _master.GetAllAsync(ct);

        return rows
            .Join(master, r => r.CurrencyId, m => m.CurrencyId, (r, m) => new OrgCurrencyDto
            {
                OrgCurrencyId = r.OrgCurrencyId,
                CurrencyId = m.CurrencyId,
                Code = m.Code,
                Name = m.Name,
                Symbol = m.Symbol,
                Format = m.Format,
                DecimalPlaces = m.DecimalPlaces,
                IsBaseCurrency = r.IsBaseCurrency,
                IsActive = r.IsActive,
            })
            // Base currency first, then alphabetical — the base is the anchor.
            .OrderByDescending(c => c.IsBaseCurrency)
            .ThenBy(c => c.Code)
            .ToList();
    }

    /// <summary>Currencies not yet added to this org — populates the Add dropdown.</summary>
    public async Task<IReadOnlyList<MasterCurrency>> AvailableAsync(Guid orgId, CancellationToken ct)
    {
        List<int> taken = await _db.OrgCurrencies
            .Where(c => c.OrgId == orgId)
            .Select(c => c.CurrencyId)
            .ToListAsync(ct);

        IReadOnlyList<MasterCurrency> master = await _master.GetAllAsync(ct);
        return master.Where(m => !taken.Contains(m.CurrencyId)).OrderBy(m => m.Code).ToList();
    }

    /// <summary>Adds a currency to the org, active. Returns false if already present.</summary>
    public async Task<bool> AddAsync(Guid orgId, int currencyId, CancellationToken ct)
    {
        bool exists = await _db.OrgCurrencies
            .AnyAsync(c => c.OrgId == orgId && c.CurrencyId == currencyId, ct);
        if (exists)
        {
            return false;
        }

        _db.OrgCurrencies.Add(new OrgCurrency
        {
            OrgCurrencyId = Guid.NewGuid(),
            OrgId = orgId,
            CurrencyId = currencyId,
            IsBaseCurrency = false,
            IsActive = true,
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Toggles a currency's active flag. The base currency can never be
    /// deactivated — every posting converts to it, so turning it off would
    /// break base-currency amounts on every future transaction.
    /// </summary>
    public async Task<SetActiveResult> SetActiveAsync(
        Guid orgId, Guid orgCurrencyId, bool isActive, CancellationToken ct)
    {
        OrgCurrency? row = await _db.OrgCurrencies
            .FirstOrDefaultAsync(c => c.OrgId == orgId && c.OrgCurrencyId == orgCurrencyId, ct);
        if (row is null)
        {
            return SetActiveResult.NotFound;
        }

        if (row.IsBaseCurrency && !isActive)
        {
            return SetActiveResult.BaseCurrencyLocked;
        }

        row.IsActive = isActive;
        await _db.SaveChangesAsync(ct);
        return SetActiveResult.Ok;
    }
}

public enum SetActiveResult
{
    Ok = 1,
    NotFound = 2,
    BaseCurrencyLocked = 3,
}
