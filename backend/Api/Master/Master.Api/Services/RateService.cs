using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;

namespace Master.Api.Services;

/// <summary>
/// Exchange and metal rate history in <c>rat</c>: the on-or-before lookup a
/// document takes its snapshot from, and hand entry by a platform operator
/// (TK-24).
///
/// <b>The latest rate on or before the date</b>, so a rate entered for Monday
/// answers for every day after it until a newer one is entered — a weekend or a
/// holiday with no publication reads Friday's figure. <b>On one date, a manual
/// row wins over a fetched one</b>: an operator enters a figure by hand because
/// the fetched one was wrong or missing, and the fix must take effect without
/// deleting what the source said.
/// </summary>
public sealed class RateService
{
    /// <summary>The most history one request returns.</summary>
    public const int MaxHistory = 500;

    private readonly AdminDbContext _db;

    public RateService(AdminDbContext db) => _db = db;

    public async Task<ExchangeRateDto?> LatestExchangeAsync(string from, string to, DateOnly on, CancellationToken ct)
    {
        from = Code(from);
        to = Code(to);

        if (from == to)
        {
            // A currency is worth one of itself on every date; there is no row to find.
            return new ExchangeRateDto(0, from, to, on, 1m, nameof(RateSource.Manual));
        }

        return await _db.ExchangeRates.AsNoTracking()
            .Where(r => r.FromCurrencyCode == from && r.ToCurrencyCode == to && r.RateDate <= on)
            .OrderByDescending(r => r.RateDate)
            .ThenBy(r => r.Source == RateSource.Manual ? 0 : 1)
            .ThenByDescending(r => r.ExchangeRateId)
            .Select(r => new ExchangeRateDto(
                r.ExchangeRateId, r.FromCurrencyCode, r.ToCurrencyCode, r.RateDate, r.Rate, r.Source.ToString()))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<MetalRateDto?> LatestMetalAsync(Metal metal, string purity, DateOnly on, CancellationToken ct)
    {
        purity = Purity(purity);

        return await _db.MetalRates.AsNoTracking()
            .Where(r => r.Metal == metal && r.PurityCode == purity && r.RateDate <= on)
            .OrderByDescending(r => r.RateDate)
            .ThenBy(r => r.Source == RateSource.Manual ? 0 : 1)
            .ThenByDescending(r => r.MetalRateId)
            .Select(r => new MetalRateDto(
                r.MetalRateId, r.Metal.ToString(), r.PurityCode, r.RateDate, r.RatePerGram, r.Source.ToString()))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<ExchangeRateDto>> ExchangeHistoryAsync(
        string? from, string? to, int take, CancellationToken ct)
    {
        IQueryable<ExchangeRate> query = _db.ExchangeRates.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(from))
        {
            string code = Code(from);
            query = query.Where(r => r.FromCurrencyCode == code);
        }

        if (!string.IsNullOrWhiteSpace(to))
        {
            string code = Code(to);
            query = query.Where(r => r.ToCurrencyCode == code);
        }

        return await query
            .OrderByDescending(r => r.RateDate)
            .ThenBy(r => r.FromCurrencyCode)
            .ThenBy(r => r.ToCurrencyCode)
            .ThenBy(r => r.Source)
            .Take(Math.Clamp(take, 1, MaxHistory))
            .Select(r => new ExchangeRateDto(
                r.ExchangeRateId, r.FromCurrencyCode, r.ToCurrencyCode, r.RateDate, r.Rate, r.Source.ToString()))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MetalRateDto>> MetalHistoryAsync(
        Metal? metal, string? purity, int take, CancellationToken ct)
    {
        IQueryable<MetalRate> query = _db.MetalRates.AsNoTracking();

        if (metal is Metal m)
        {
            query = query.Where(r => r.Metal == m);
        }

        if (!string.IsNullOrWhiteSpace(purity))
        {
            string code = Purity(purity);
            query = query.Where(r => r.PurityCode == code);
        }

        return await query
            .OrderByDescending(r => r.RateDate)
            .ThenBy(r => r.Metal)
            .ThenBy(r => r.PurityCode)
            .ThenBy(r => r.Source)
            .Take(Math.Clamp(take, 1, MaxHistory))
            .Select(r => new MetalRateDto(
                r.MetalRateId, r.Metal.ToString(), r.PurityCode, r.RateDate, r.RatePerGram, r.Source.ToString()))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Enters or corrects the manual rate for a pair and date. A fetched row for
    /// the same date is left alone; the manual one outranks it in the lookup.
    /// </summary>
    public async Task<RateSaveOutcome> SetExchangeAsync(SaveExchangeRateRequest request, CancellationToken ct)
    {
        string from = Code(request.FromCurrencyCode);
        string to = Code(request.ToCurrencyCode);
        DateOnly date = request.RateDate!.Value;

        if (from == to)
        {
            return RateSaveOutcome.SameCurrency;
        }

        int known = await _db.Currencies.CountAsync(c => c.Code == from || c.Code == to, ct);
        if (known < 2)
        {
            return RateSaveOutcome.UnknownCurrency;
        }

        ExchangeRate? row = await _db.ExchangeRates.FirstOrDefaultAsync(
            r => r.FromCurrencyCode == from && r.ToCurrencyCode == to && r.RateDate == date
                && r.Source == RateSource.Manual, ct);

        if (row is null)
        {
            row = new ExchangeRate
            {
                FromCurrencyCode = from,
                ToCurrencyCode = to,
                RateDate = date,
                Source = RateSource.Manual,
            };
            _db.ExchangeRates.Add(row);
        }

        row.Rate = request.Rate;
        await _db.SaveChangesAsync(ct);
        return RateSaveOutcome.Ok;
    }

    public async Task<RateSaveOutcome> SetMetalAsync(SaveMetalRateRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse(request.Metal, ignoreCase: true, out Metal metal) || !Enum.IsDefined(metal))
        {
            return RateSaveOutcome.UnknownMetal;
        }

        string purity = Purity(request.PurityCode);
        DateOnly date = request.RateDate!.Value;

        MetalRate? row = await _db.MetalRates.FirstOrDefaultAsync(
            r => r.Metal == metal && r.PurityCode == purity && r.RateDate == date
                && r.Source == RateSource.Manual, ct);

        if (row is null)
        {
            row = new MetalRate
            {
                Metal = metal,
                PurityCode = purity,
                RateDate = date,
                Source = RateSource.Manual,
            };
            _db.MetalRates.Add(row);
        }

        row.RatePerGram = request.RatePerGram;
        await _db.SaveChangesAsync(ct);
        return RateSaveOutcome.Ok;
    }

    /// <summary>
    /// Removes a hand-entered exchange rate. A fetched one cannot be removed
    /// here — it is what the source said, and a manual row on the same date is
    /// how it is overridden.
    /// </summary>
    public async Task<RateSaveOutcome> DeleteExchangeAsync(long id, CancellationToken ct)
    {
        ExchangeRate? row = await _db.ExchangeRates.FirstOrDefaultAsync(r => r.ExchangeRateId == id, ct);
        if (row is null)
        {
            return RateSaveOutcome.NotFound;
        }

        if (row.Source != RateSource.Manual)
        {
            return RateSaveOutcome.NotManual;
        }

        _db.ExchangeRates.Remove(row);
        await _db.SaveChangesAsync(ct);
        return RateSaveOutcome.Ok;
    }

    public async Task<RateSaveOutcome> DeleteMetalAsync(long id, CancellationToken ct)
    {
        MetalRate? row = await _db.MetalRates.FirstOrDefaultAsync(r => r.MetalRateId == id, ct);
        if (row is null)
        {
            return RateSaveOutcome.NotFound;
        }

        if (row.Source != RateSource.Manual)
        {
            return RateSaveOutcome.NotManual;
        }

        _db.MetalRates.Remove(row);
        await _db.SaveChangesAsync(ct);
        return RateSaveOutcome.Ok;
    }

    private static string Code(string value) => value.Trim().ToUpperInvariant();

    private static string Purity(string value) => value.Trim().ToUpperInvariant();
}
