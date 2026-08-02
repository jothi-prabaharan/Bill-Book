using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Ordering;

namespace Accounting.Api.Services;

/// <summary>
/// Payment terms, and the one piece of logic that matters about them: turning a
/// document date into a due date. That calculation lives here and is exposed
/// over the API so Sales and Purchase never reimplement it — two copies would
/// eventually disagree about what End of Month means.
/// </summary>
public sealed class PaymentTermService
{
    private readonly AccountingDbContext _db;
    private readonly TimeProvider _clock;

    public PaymentTermService(AccountingDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<PaymentTermListItem>> ListAsync(
        bool includeInactive, CancellationToken ct)
    {
        IQueryable<PaymentTerm> query = _db.PaymentTerms;

        if (!includeInactive)
        {
            query = query.Where(t => t.IsActive);
        }

        List<PaymentTerm> rows = await query
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.TermName)
            .ToListAsync(ct);

        return rows.Select(Map).ToList();
    }

    public async Task<PaymentTermListItem?> GetAsync(long paymentTermId, CancellationToken ct)
    {
        PaymentTerm? row = await _db.PaymentTerms
            .FirstOrDefaultAsync(t => t.PaymentTermId == paymentTermId, ct);

        return row is null ? null : Map(row);
    }

    /// <summary>
    /// The due date and discount deadline for a document. Sales and Purchase
    /// call this rather than carrying their own copy of the rules.
    /// </summary>
    public async Task<DueDateResult?> ResolveDueDateAsync(
        long paymentTermId, DateOnly documentDate, CancellationToken ct)
    {
        PaymentTerm? term = await _db.PaymentTerms
            .FirstOrDefaultAsync(t => t.PaymentTermId == paymentTermId, ct);

        return term is null ? null : DueDateFor(term, documentDate);
    }

    public async Task<SaveTermResult> CreateAsync(SavePaymentTermRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse(request.TermType, ignoreCase: true, out PaymentTermType termType))
        {
            return new SaveTermResult(SaveTermOutcome.InvalidValue, null);
        }

        SaveTermOutcome invalid = Validate(request, termType);
        if (invalid != SaveTermOutcome.Ok)
        {
            return new SaveTermResult(invalid, null);
        }

        if (await NameTakenAsync(request.TermName, null, ct))
        {
            return new SaveTermResult(SaveTermOutcome.DuplicateName, null);
        }

        var term = new PaymentTerm
        {
            TermSystemName = null,
            TermName = request.TermName.Trim(),
            TermType = termType,
            DueDays = termType == PaymentTermType.DueOnReceipt ? 0 : request.DueDays,
            DueDayOfMonth = termType == PaymentTermType.DayOfNextMonth ? request.DueDayOfMonth : null,
            DiscountPercent = request.DiscountPercent,
            DiscountDays = request.DiscountDays,
            IsSales = request.IsSales,
            IsPurchase = request.IsPurchase,
            IsSystem = false,
            IsActive = request.IsActive,
            DisplayOrder = await NextDisplayOrderAsync(ct),
        };

        // The first term an organization has must be the default, or every new
        // contact opens with an empty terms field.
        term.IsDefault = !await _db.PaymentTerms.AnyAsync(t => t.IsDefault, ct);

        _db.PaymentTerms.Add(term);
        await _db.SaveChangesAsync(ct);

        return new SaveTermResult(SaveTermOutcome.Ok, term.PaymentTermId);
    }

    public async Task<SaveTermResult> UpdateAsync(
        long paymentTermId, SavePaymentTermRequest request, CancellationToken ct)
    {
        PaymentTerm? term = await _db.PaymentTerms
            .FirstOrDefaultAsync(t => t.PaymentTermId == paymentTermId, ct);

        if (term is null)
        {
            return new SaveTermResult(SaveTermOutcome.NotFound, null);
        }

        if (!Enum.TryParse(request.TermType, ignoreCase: true, out PaymentTermType termType))
        {
            return new SaveTermResult(SaveTermOutcome.InvalidValue, null);
        }

        // A seeded term's rule is fixed: contacts and unpaid documents point at
        // it, and changing Net 30 into Net 90 would silently restate their due
        // dates. The display name stays editable, as everywhere else.
        if (term.IsSystem && ChangesRule(term, request, termType))
        {
            return new SaveTermResult(SaveTermOutcome.SystemTermLocked, null);
        }

        SaveTermOutcome invalid = Validate(request, termType);
        if (invalid != SaveTermOutcome.Ok)
        {
            return new SaveTermResult(invalid, null);
        }

        if (await NameTakenAsync(request.TermName, paymentTermId, ct))
        {
            return new SaveTermResult(SaveTermOutcome.DuplicateName, null);
        }

        term.TermName = request.TermName.Trim();
        term.TermType = termType;
        term.DueDays = termType == PaymentTermType.DueOnReceipt ? 0 : request.DueDays;
        term.DueDayOfMonth = termType == PaymentTermType.DayOfNextMonth ? request.DueDayOfMonth : null;
        term.DiscountPercent = request.DiscountPercent;
        term.DiscountDays = request.DiscountDays;
        term.IsSales = request.IsSales;
        term.IsPurchase = request.IsPurchase;
        term.IsActive = request.IsActive;

        await _db.SaveChangesAsync(ct);
        return new SaveTermResult(SaveTermOutcome.Ok, term.PaymentTermId);
    }

    public async Task<SaveTermOutcome> SetDefaultAsync(long paymentTermId, CancellationToken ct)
    {
        PaymentTerm? term = await _db.PaymentTerms
            .FirstOrDefaultAsync(t => t.PaymentTermId == paymentTermId, ct);

        if (term is null)
        {
            return SaveTermOutcome.NotFound;
        }

        List<PaymentTerm> previous = await _db.PaymentTerms
            .Where(t => t.IsDefault && t.PaymentTermId != paymentTermId)
            .ToListAsync(ct);

        foreach (PaymentTerm row in previous)
        {
            row.IsDefault = false;
        }

        term.IsDefault = true;
        await _db.SaveChangesAsync(ct);
        return SaveTermOutcome.Ok;
    }

    /// <summary>
    /// Deactivates. Terms are never hard-deleted — contacts and unpaid documents
    /// point at them, and the row is how a historical due date is explained.
    /// </summary>
    public async Task<SaveTermOutcome> DeactivateAsync(long paymentTermId, CancellationToken ct)
    {
        PaymentTerm? term = await _db.PaymentTerms
            .FirstOrDefaultAsync(t => t.PaymentTermId == paymentTermId, ct);

        if (term is null)
        {
            return SaveTermOutcome.NotFound;
        }

        term.IsActive = false;
        term.IsDefault = false;
        await _db.SaveChangesAsync(ct);
        return SaveTermOutcome.Ok;
    }

    public async Task<SaveTermOutcome> ReorderAsync(ReorderRequest request, CancellationToken ct)
    {
        List<PaymentTerm> all = await _db.PaymentTerms
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.TermName)
            .ToListAsync(ct);

        if (!Reordering.Apply(all, request, t => t.PaymentTermId, t => t.DisplayOrder,
                (t, order) => t.DisplayOrder = order))
        {
            return SaveTermOutcome.NotFound;
        }

        await _db.SaveChangesAsync(ct);
        return SaveTermOutcome.Ok;
    }

    /// <summary>
    /// Writes the default payment terms for an organization, adding only what is
    /// missing. Safe to re-run: a term added to the seed list later reaches
    /// organizations created before it existed, which a bail-out on "has any
    /// rows" never would. Matched on <c>TermSystemName</c>, so a term the
    /// customer has renamed is still recognised as present.
    /// </summary>
    public async Task<int> SeedForOrganizationAsync(Guid orgId, CancellationToken ct)
    {
        List<string> existing = await _db.PaymentTerms
            .IgnoreQueryFilters()
            .Where(t => t.OrgId == orgId && t.TermSystemName != null)
            .Select(t => t.TermSystemName!)
            .ToListAsync(ct);

        HashSet<string> present = [.. existing];

        // TermName is unique per organization too. A customer who created their
        // own "Net 15" before this seed row existed would otherwise make the
        // whole seeding call throw, which during provisioning fails the branch.
        List<string> names = await _db.PaymentTerms
            .IgnoreQueryFilters()
            .Where(t => t.OrgId == orgId)
            .Select(t => t.TermName)
            .ToListAsync(ct);

        HashSet<string> taken = [.. names];

        List<PaymentTerm> missing = Repository.SeedData.PaymentTermsSeed.Build(orgId)
            .Where(t => t.TermSystemName is not null
                && !present.Contains(t.TermSystemName)
                && !taken.Contains(t.TermName))
            .ToList();

        if (missing.Count == 0)
        {
            return 0;
        }

        // One default per organization is a filtered unique index. Backfilling
        // Due on Receipt into an organization that has since made Net 30 its
        // default would violate it, so the seed's flag only stands when there is
        // no default to displace.
        if (await _db.PaymentTerms
            .IgnoreQueryFilters()
            .AnyAsync(t => t.OrgId == orgId && t.IsDefault, ct))
        {
            foreach (PaymentTerm term in missing)
            {
                term.IsDefault = false;
            }
        }

        _db.PaymentTerms.AddRange(missing);
        await _db.SaveChangesAsync(ct);
        return missing.Count;
    }

    /// <summary>
    /// The rules, in one place. DayOfNextMonth clamps rather than overflows: the
    /// 31st in February is the 28th, not the 3rd of March.
    /// </summary>
    private static DueDateResult DueDateFor(PaymentTerm term, DateOnly documentDate)
    {
        DateOnly due = term.TermType switch
        {
            PaymentTermType.DueOnReceipt => documentDate,
            PaymentTermType.Net => documentDate.AddDays(term.DueDays),
            PaymentTermType.EndOfMonth => EndOfMonth(documentDate).AddDays(term.DueDays),
            PaymentTermType.DayOfNextMonth => DayOfNextMonth(documentDate, term.DueDayOfMonth ?? 1),
            _ => documentDate,
        };

        DateOnly? discountUntil = term.DiscountPercent > 0 && term.DiscountDays > 0
            ? documentDate.AddDays(term.DiscountDays)
            : null;

        return new DueDateResult(due, discountUntil, term.DiscountPercent);
    }

    private static DateOnly EndOfMonth(DateOnly date) =>
        new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));

    private static DateOnly DayOfNextMonth(DateOnly date, int day)
    {
        DateOnly nextMonth = date.AddMonths(1);
        int daysInMonth = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
        return new DateOnly(nextMonth.Year, nextMonth.Month, Math.Min(day, daysInMonth));
    }

    private static SaveTermOutcome Validate(SavePaymentTermRequest request, PaymentTermType termType)
    {
        if (!request.IsSales && !request.IsPurchase)
        {
            return SaveTermOutcome.NoApplicability;
        }

        if (termType == PaymentTermType.DayOfNextMonth && request.DueDayOfMonth is null)
        {
            return SaveTermOutcome.DayOfMonthRequired;
        }

        if (termType == PaymentTermType.Net && request.DiscountDays > request.DueDays)
        {
            return SaveTermOutcome.DiscountWindowTooLong;
        }

        if (request.DiscountPercent > 0 && request.DiscountDays <= 0)
        {
            return SaveTermOutcome.DiscountDaysRequired;
        }

        return SaveTermOutcome.Ok;
    }

    private static bool ChangesRule(
        PaymentTerm term, SavePaymentTermRequest request, PaymentTermType termType) =>
        term.TermType != termType
        || term.DueDays != request.DueDays
        || term.DueDayOfMonth != request.DueDayOfMonth
        || term.DiscountPercent != request.DiscountPercent
        || term.DiscountDays != request.DiscountDays;

    private async Task<int> NextDisplayOrderAsync(CancellationToken ct)
    {
        int highest = await _db.PaymentTerms.Select(t => (int?)t.DisplayOrder).MaxAsync(ct) ?? 0;
        return highest + Reordering.Gap;
    }

    private Task<bool> NameTakenAsync(string termName, long? exceptId, CancellationToken ct)
    {
        string name = termName.Trim();
        return _db.PaymentTerms.AnyAsync(
            t => t.TermName == name && (exceptId == null || t.PaymentTermId != exceptId), ct);
    }

    private PaymentTermListItem Map(PaymentTerm t)
    {
        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

        return new PaymentTermListItem
        {
            PaymentTermId = t.PaymentTermId,
            TermSystemName = t.TermSystemName,
            TermName = t.TermName,
            TermType = t.TermType.ToString(),
            DueDays = t.DueDays,
            DueDayOfMonth = t.DueDayOfMonth,
            DiscountPercent = t.DiscountPercent,
            DiscountDays = t.DiscountDays,
            IsSales = t.IsSales,
            IsPurchase = t.IsPurchase,
            IsDefault = t.IsDefault,
            IsSystem = t.IsSystem,
            IsActive = t.IsActive,
            DisplayOrder = t.DisplayOrder,
            SampleDueDate = DueDateFor(t, today).DueDate,
        };
    }
}
