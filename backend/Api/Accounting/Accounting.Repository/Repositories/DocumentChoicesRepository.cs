using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;

namespace Accounting.Repository.Repositories;

/// <summary>
/// The option lists a create page reads (TK-192): the tax rates, payment terms
/// and accounts a document may use, and its next number.
///
/// They exist apart from the tax master, payment term and chart-of-accounts
/// lists because those are behind <c>accounting.view</c>, and a clerk raising a
/// bill must be able to pick its tax rate without being able to read the
/// accounting masters. So each list here carries only what a picker shows, and
/// only what the side asking may use: a purchase document is offered rates,
/// terms and accounts flagged for purchase.
/// </summary>
public sealed class DocumentChoicesRepository
{
    private readonly AccountingDbContext _db;
    private readonly INumberGenerator _numbers;
    private readonly TimeProvider _clock;

    public DocumentChoicesRepository(AccountingDbContext db, INumberGenerator numbers, TimeProvider clock)
    {
        _db = db;
        _numbers = numbers;
        _clock = clock;
    }

    /// <summary>
    /// The rates in force on <paramref name="on"/>, one per tax group — the same
    /// rule <see cref="TaxMasterRepository.ResolveAsync"/> applies when a document
    /// is posted, so the page never offers a rate posting would refuse.
    /// </summary>
    public async Task<IReadOnlyList<TaxRateChoice>> TaxRatesAsync(
        ChoiceUsage usage, DateOnly? on, CancellationToken ct)
    {
        DateOnly date = on ?? Today();

        IQueryable<TaxMaster> query = _db.TaxMasters
            .Where(t => t.IsActive
                && t.EffectiveFrom <= date
                && (t.EffectiveTo == null || t.EffectiveTo >= date));

        query = usage switch
        {
            ChoiceUsage.Sales => query.Where(t => t.IsSales),
            ChoiceUsage.Purchase => query.Where(t => t.IsPurchase),
            _ => query,
        };

        List<TaxMaster> rows = await query
            .OrderByDescending(t => t.EffectiveFrom)
            .ToListAsync(ct);

        // A revision dated on the day the old rate ends leaves both in force
        // that day; the later one is the rate, as it is when posting.
        return rows
            .GroupBy(t => t.TaxGroupId)
            .Select(g => g.First())
            .OrderBy(t => t.TotalRate)
            .ThenBy(t => t.TaxName)
            .Select(t => new TaxRateChoice
            {
                TaxMasterId = t.TaxMasterId,
                TaxGroupId = t.TaxGroupId,
                TaxName = t.TaxName,
                TotalRate = t.TotalRate,
                CgstRate = t.CgstRate,
                SgstRate = t.SgstRate,
                IgstRate = t.IgstRate,
                CessRate = t.CessRate,
            })
            .ToList();
    }

    /// <summary>Active terms for the side asking, in the branch's own order.</summary>
    public async Task<IReadOnlyList<PaymentTermChoice>> PaymentTermsAsync(
        ChoiceUsage usage, CancellationToken ct)
    {
        IQueryable<PaymentTerm> query = _db.PaymentTerms.Where(t => t.IsActive);

        query = usage switch
        {
            ChoiceUsage.Sales => query.Where(t => t.IsSales),
            ChoiceUsage.Purchase => query.Where(t => t.IsPurchase),
            _ => query,
        };

        List<PaymentTerm> rows = await query
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.TermName)
            .ToListAsync(ct);

        return rows.Select(t => new PaymentTermChoice
        {
            PaymentTermId = t.PaymentTermId,
            TermName = t.TermName,
            TermType = t.TermType.ToString(),
            DueDays = t.DueDays,
            DueDayOfMonth = t.DueDayOfMonth,
            IsDefault = t.IsDefault,
        }).ToList();
    }

    /// <summary>
    /// Active accounts the side asking may post to. A posting-locked account is
    /// left out: it takes no posting of any kind, so offering it only sets up a
    /// refusal at save.
    /// </summary>
    public async Task<IReadOnlyList<AccountChoice>> AccountsAsync(
        ChoiceUsage usage, CancellationToken ct)
    {
        // Parent accounts cannot accept transactions — only leaf accounts can be posted to.
        var parentAccountIds = _db.Accounts
            .Where(a => a.ParentAccountId != null)
            .Select(a => a.ParentAccountId!.Value);

        IQueryable<Account> query = _db.Accounts.Where(a => a.IsActive && !a.IsLock && !parentAccountIds.Contains(a.AccountId));

        query = usage switch
        {
            ChoiceUsage.Sales => query.Where(a => a.IsSales),
            ChoiceUsage.Purchase => query.Where(a => a.IsPurchase),
            ChoiceUsage.Payment => query.Where(a => a.IsPayment),
            _ => query,
        };

        return await (
            from a in query
            join p in _db.Accounts on a.ParentAccountId equals p.AccountId into parents
            from p in parents.DefaultIfEmpty()
            orderby a.AccountTypeId, a.AccountCode
            select new AccountChoice
            {
                AccountId = a.AccountId,
                AccountCode = a.AccountCode,
                AccountName = a.AccountName,
                AccountTypeId = a.AccountTypeId,
                CurrencyCode = a.CurrencyCode,
                ParentAccountId = a.ParentAccountId,
                ParentAccountName = p != null ? p.AccountName : null,
                Depth = a.ParentAccountId != null ? 1 : 0,
            }).ToListAsync(ct);
    }

    /// <summary>
    /// The number the next document in <paramref name="seriesCode"/> would take
    /// on <paramref name="on"/>, through the generator's own choice of series, so
    /// the preview and the allocation cannot disagree about which series is used.
    /// </summary>
    public async Task<NextNumberChoice> NextNumberAsync(
        string seriesCode, DateOnly? on, CancellationToken ct) =>
        new()
        {
            SeriesCode = seriesCode,
            Number = await _numbers.PeekAsync(seriesCode, on ?? Today(), ct),
        };

    private DateOnly Today() => DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
}
