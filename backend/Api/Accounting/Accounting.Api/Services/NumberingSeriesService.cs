using Accounting.Entity.Models;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;

namespace Accounting.Api.Services;

/// <summary>
/// The numbering series master behind Settings › Numbering series. CRUD, plus
/// three things that are not CRUD: the counter is moved by its own audited
/// action rather than as a field on the form, exactly one series per code and
/// branch is the default, and reordering is expressed as a drop between
/// neighbours so the server picks the value.
/// </summary>
public sealed class NumberingSeriesService
{
    /// <summary>
    /// Seeded gap between rows. Wide enough that a drop between two neighbours
    /// almost always lands in the middle without renumbering anything.
    /// </summary>
    private const int OrderGap = 10;

    private readonly AccountingDbContext _db;
    private readonly TimeProvider _clock;
    private readonly IFinancialYearProvider _financialYear;

    public NumberingSeriesService(
        AccountingDbContext db, TimeProvider clock, IFinancialYearProvider financialYear)
    {
        _db = db;
        _clock = clock;
        _financialYear = financialYear;
    }

    public async Task<IReadOnlyList<NumberingSeriesListItem>> ListAsync(
        bool includeInactive, string? seriesFor, CancellationToken ct)
    {
        IQueryable<NumberingSeries> query = _db.NumberingSeries;

        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        if (Enum.TryParse(seriesFor, ignoreCase: true, out SeriesFor filter))
        {
            query = query.Where(s => s.SeriesFor == filter);
        }

        List<NumberingSeries> rows = await query
            .OrderBy(s => s.SeriesFor)
            .ThenBy(s => s.DisplayOrder)
            .ThenBy(s => s.SeriesName)
            .ToListAsync(ct);

        // Resolved once for the whole list rather than per row: the preview on
        // every row is composed against the same branch's year.
        int startMonth = await _financialYear.GetStartMonthAsync(ct);

        return rows.Select(s => Map(s, startMonth)).ToList();
    }

    public async Task<NumberingSeriesListItem?> GetAsync(long numberingSeriesId, CancellationToken ct)
    {
        NumberingSeries? row = await _db.NumberingSeries
            .FirstOrDefaultAsync(s => s.NumberingSeriesId == numberingSeriesId, ct);

        return row is null ? null : Map(row, await _financialYear.GetStartMonthAsync(ct));
    }

    public async Task<SaveSeriesResult> CreateAsync(
        SaveNumberingSeriesRequest request, CancellationToken ct)
    {
        if (!TryParse(request, out SeriesFor seriesFor, out FinancialYearFormat fyFormat,
                out NumberResetFrequency reset))
        {
            return new SaveSeriesResult(SaveSeriesOutcome.InvalidValue, null);
        }

        SaveSeriesOutcome invalid = Validate(request, seriesFor);
        if (invalid != SaveSeriesOutcome.Ok)
        {
            return new SaveSeriesResult(invalid, null);
        }

        if (await NameTakenAsync(request.SeriesName, null, ct))
        {
            return new SaveSeriesResult(SaveSeriesOutcome.DuplicateName, null);
        }

        var series = new NumberingSeries
        {
            SeriesSystemName = null,
            SeriesCode = request.SeriesCode.Trim().ToUpperInvariant(),
            SeriesName = request.SeriesName.Trim(),
            SeriesFor = seriesFor,
            Prefix = request.Prefix,
            Suffix = request.Suffix,
            Separator = request.Separator,
            IncludeFinancialYear = request.IncludeFinancialYear,
            FinancialYearFormat = fyFormat,
            IncludeBranchCode = request.IncludeBranchCode,
            BranchCode = request.BranchCode,
            NumberLength = request.NumberLength,
            StartNumber = request.StartNumber,
            NextNumber = request.StartNumber,
            ResetFrequency = reset,
            AllowManualOverride = request.AllowManualOverride,
            IsSystem = false,
            IsActive = request.IsActive,
            DisplayOrder = await NextDisplayOrderAsync(ct),
        };

        // The first series for a code and branch has to be the default, or the
        // generator finds nothing to allocate from.
        series.IsDefault = !await _db.NumberingSeries.AnyAsync(
            s => s.SeriesCode == series.SeriesCode && s.IsDefault, ct);

        _db.NumberingSeries.Add(series);
        await _db.SaveChangesAsync(ct);

        return new SaveSeriesResult(SaveSeriesOutcome.Ok, series.NumberingSeriesId);
    }

    /// <summary>
    /// Edits the format. The counter is not touched here — changing padding or a
    /// prefix must not silently restart a live sequence.
    /// </summary>
    public async Task<SaveSeriesResult> UpdateAsync(
        long numberingSeriesId, SaveNumberingSeriesRequest request, CancellationToken ct)
    {
        NumberingSeries? series = await _db.NumberingSeries
            .FirstOrDefaultAsync(s => s.NumberingSeriesId == numberingSeriesId, ct);

        if (series is null)
        {
            return new SaveSeriesResult(SaveSeriesOutcome.NotFound, null);
        }

        if (!TryParse(request, out SeriesFor seriesFor, out FinancialYearFormat fyFormat,
                out NumberResetFrequency reset))
        {
            return new SaveSeriesResult(SaveSeriesOutcome.InvalidValue, null);
        }

        // What a series numbers is fixed at creation. A master series turned
        // into a document series would inherit a counter with gaps already in it,
        // and those gaps are exactly what a document series must not have.
        if (seriesFor != series.SeriesFor)
        {
            return new SaveSeriesResult(SaveSeriesOutcome.InvalidValue, null);
        }

        // A seeded series is what code looks up by. Its format is the user's to
        // change; its code is not, or every caller passing CUSTOMER stops resolving.
        string requestedCode = request.SeriesCode.Trim().ToUpperInvariant();
        if (series.IsSystem && requestedCode != series.SeriesCode)
        {
            return new SaveSeriesResult(SaveSeriesOutcome.SeriesCodeLocked, null);
        }

        SaveSeriesOutcome invalid = Validate(request, series.SeriesFor);
        if (invalid != SaveSeriesOutcome.Ok)
        {
            return new SaveSeriesResult(invalid, null);
        }

        if (await NameTakenAsync(request.SeriesName, numberingSeriesId, ct))
        {
            return new SaveSeriesResult(SaveSeriesOutcome.DuplicateName, null);
        }

        series.SeriesCode = requestedCode;
        series.SeriesName = request.SeriesName.Trim();
        series.Prefix = request.Prefix;
        series.Suffix = request.Suffix;
        series.Separator = request.Separator;
        series.IncludeFinancialYear = request.IncludeFinancialYear;
        series.FinancialYearFormat = fyFormat;
        series.IncludeBranchCode = request.IncludeBranchCode;
        series.BranchCode = request.BranchCode;
        series.NumberLength = request.NumberLength;
        series.StartNumber = request.StartNumber;
        series.ResetFrequency = reset;
        series.AllowManualOverride = request.AllowManualOverride;
        series.IsActive = request.IsActive;

        await _db.SaveChangesAsync(ct);
        return new SaveSeriesResult(SaveSeriesOutcome.Ok, series.NumberingSeriesId);
    }

    /// <summary>Moves the counter. Separate from the format edit because it is the risky one.</summary>
    public async Task<SetNextNumberResult> SetNextNumberAsync(
        long numberingSeriesId, long nextNumber, CancellationToken ct)
    {
        NumberingSeries? series = await _db.NumberingSeries
            .FirstOrDefaultAsync(s => s.NumberingSeriesId == numberingSeriesId, ct);

        if (series is null)
        {
            return new SetNextNumberResult(SaveSeriesOutcome.NotFound, false);
        }

        if (nextNumber < series.StartNumber)
        {
            return new SetNextNumberResult(SaveSeriesOutcome.NextNumberBelowStart, false);
        }

        bool lowered = nextNumber < series.NextNumber;
        series.NextNumber = nextNumber;
        await _db.SaveChangesAsync(ct);

        return new SetNextNumberResult(SaveSeriesOutcome.Ok, lowered);
    }

    /// <summary>
    /// Promotes a series to default and clears the previous one in the same
    /// transaction — the filtered unique index would reject two.
    /// </summary>
    public async Task<SaveSeriesOutcome> SetDefaultAsync(long numberingSeriesId, CancellationToken ct)
    {
        NumberingSeries? series = await _db.NumberingSeries
            .FirstOrDefaultAsync(s => s.NumberingSeriesId == numberingSeriesId, ct);

        if (series is null)
        {
            return SaveSeriesOutcome.NotFound;
        }

        List<NumberingSeries> siblings = await _db.NumberingSeries
            .Where(s => s.SeriesCode == series.SeriesCode
                && s.IsDefault
                && s.NumberingSeriesId != numberingSeriesId)
            .ToListAsync(ct);

        foreach (NumberingSeries sibling in siblings)
        {
            sibling.IsDefault = false;
        }

        series.IsDefault = true;
        await _db.SaveChangesAsync(ct);
        return SaveSeriesOutcome.Ok;
    }

    /// <summary>
    /// Deactivates. Series are never hard-deleted — a code it issued is sitting
    /// on records, and the row is how that code is explained later.
    /// </summary>
    public async Task<SaveSeriesOutcome> DeactivateAsync(long numberingSeriesId, CancellationToken ct)
    {
        NumberingSeries? series = await _db.NumberingSeries
            .FirstOrDefaultAsync(s => s.NumberingSeriesId == numberingSeriesId, ct);

        if (series is null)
        {
            return SaveSeriesOutcome.NotFound;
        }

        bool anotherActive = await _db.NumberingSeries.AnyAsync(
            s => s.SeriesCode == series.SeriesCode
                && s.NumberingSeriesId != numberingSeriesId
                && s.IsActive,
            ct);

        // Without this, deactivating the only CUSTOMER series makes every new
        // contact fail at save with an error about numbering, far from here.
        if (!anotherActive)
        {
            return SaveSeriesOutcome.LastActiveSeries;
        }

        series.IsActive = false;
        series.IsDefault = false;
        await _db.SaveChangesAsync(ct);
        return SaveSeriesOutcome.Ok;
    }

    /// <summary>
    /// Applies a drag-and-drop drop. The moved row lands midway between its new
    /// neighbours; when the gap is used up the whole list is renumbered in tens
    /// in the same transaction.
    /// </summary>
    public async Task<SaveSeriesOutcome> ReorderAsync(ReorderRequest request, CancellationToken ct)
    {
        NumberingSeries? moved = await _db.NumberingSeries
            .FirstOrDefaultAsync(s => s.NumberingSeriesId == request.MovedId, ct);

        if (moved is null)
        {
            return SaveSeriesOutcome.NotFound;
        }

        int? above = await OrderOfAsync(request.PreviousId, ct);
        int? below = await OrderOfAsync(request.NextId, ct);

        if (above is int a && below is int b)
        {
            if (b - a >= 2)
            {
                moved.DisplayOrder = a + ((b - a) / 2);
                await _db.SaveChangesAsync(ct);
                return SaveSeriesOutcome.Ok;
            }
        }
        else if (below is int onlyBelow && above is null)
        {
            if (onlyBelow > 0)
            {
                moved.DisplayOrder = onlyBelow / 2;
                await _db.SaveChangesAsync(ct);
                return SaveSeriesOutcome.Ok;
            }
        }
        else if (above is int onlyAbove && below is null)
        {
            moved.DisplayOrder = onlyAbove + OrderGap;
            await _db.SaveChangesAsync(ct);
            return SaveSeriesOutcome.Ok;
        }

        await RenumberAsync(moved, request, ct);
        return SaveSeriesOutcome.Ok;
    }

    /// <summary>Writes the default numbering series for a newly created organization.</summary>
    public async Task<int> SeedForOrganizationAsync(Guid orgId, CancellationToken ct)
    {
        if (await _db.NumberingSeries.IgnoreQueryFilters().AnyAsync(s => s.OrgId == orgId, ct))
        {
            return 0;
        }

        IReadOnlyList<NumberingSeries> seed = Repository.SeedData.NumberingSeriesSeed.Build(orgId);
        _db.NumberingSeries.AddRange(seed);
        await _db.SaveChangesAsync(ct);
        return seed.Count;
    }

    /// <summary>
    /// Rebuilds the order in tens with the moved row in its new slot. Only runs
    /// when the neighbours have no room between them.
    /// </summary>
    private async Task RenumberAsync(
        NumberingSeries moved, ReorderRequest request, CancellationToken ct)
    {
        List<NumberingSeries> all = await _db.NumberingSeries
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.SeriesName)
            .ToListAsync(ct);

        all.Remove(moved);

        int insertAt = all.Count;
        if (request.NextId is long nextId)
        {
            int index = all.FindIndex(s => s.NumberingSeriesId == nextId);
            insertAt = index < 0 ? all.Count : index;
        }
        else if (request.PreviousId is long previousId)
        {
            int index = all.FindIndex(s => s.NumberingSeriesId == previousId);
            insertAt = index < 0 ? all.Count : index + 1;
        }
        else
        {
            insertAt = 0;
        }

        all.Insert(insertAt, moved);

        for (int i = 0; i < all.Count; i++)
        {
            all[i].DisplayOrder = (i + 1) * OrderGap;
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<int?> OrderOfAsync(long? numberingSeriesId, CancellationToken ct)
    {
        if (numberingSeriesId is not long id)
        {
            return null;
        }

        NumberingSeries? row = await _db.NumberingSeries
            .FirstOrDefaultAsync(s => s.NumberingSeriesId == id, ct);

        return row?.DisplayOrder;
    }

    private async Task<int> NextDisplayOrderAsync(CancellationToken ct)
    {
        int highest = await _db.NumberingSeries
            .Select(s => (int?)s.DisplayOrder)
            .MaxAsync(ct) ?? 0;

        return highest + OrderGap;
    }

    private Task<bool> NameTakenAsync(string seriesName, long? exceptId, CancellationToken ct)
    {
        string name = seriesName.Trim();
        return _db.NumberingSeries.AnyAsync(
            s => s.SeriesName == name && (exceptId == null || s.NumberingSeriesId != exceptId), ct);
    }

    private static SaveSeriesOutcome Validate(SaveNumberingSeriesRequest request, SeriesFor seriesFor)
    {
        if (seriesFor == SeriesFor.Document && request.AllowManualOverride)
        {
            return SaveSeriesOutcome.ManualOverrideOnDocument;
        }

        if (request.IncludeBranchCode && string.IsNullOrWhiteSpace(request.BranchCode))
        {
            return SaveSeriesOutcome.BranchCodeRequired;
        }

        return SaveSeriesOutcome.Ok;
    }

    private static bool TryParse(
        SaveNumberingSeriesRequest request,
        out SeriesFor seriesFor,
        out FinancialYearFormat fyFormat,
        out NumberResetFrequency reset)
    {
        fyFormat = FinancialYearFormat.Compact;
        reset = NumberResetFrequency.Never;

        return Enum.TryParse(request.SeriesFor, ignoreCase: true, out seriesFor)
            && Enum.TryParse(request.FinancialYearFormat, ignoreCase: true, out fyFormat)
            && Enum.TryParse(request.ResetFrequency, ignoreCase: true, out reset);
    }

    private NumberingSeriesListItem Map(NumberingSeries s, int startMonth)
    {
        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        long next = NumberFormat.IsResetDue(s, today, startMonth)
            ? s.StartNumber
            : s.NextNumber;

        return new NumberingSeriesListItem
        {
            NumberingSeriesId = s.NumberingSeriesId,
            SeriesSystemName = s.SeriesSystemName,
            SeriesCode = s.SeriesCode,
            SeriesName = s.SeriesName,
            SeriesFor = s.SeriesFor.ToString(),
            Prefix = s.Prefix,
            Suffix = s.Suffix,
            Separator = s.Separator,
            IncludeFinancialYear = s.IncludeFinancialYear,
            FinancialYearFormat = s.FinancialYearFormat.ToString(),
            IncludeBranchCode = s.IncludeBranchCode,
            BranchCode = s.BranchCode,
            NumberLength = s.NumberLength,
            StartNumber = s.StartNumber,
            NextNumber = s.NextNumber,
            ResetFrequency = s.ResetFrequency.ToString(),
            LastResetOn = s.LastResetOn,
            AllowManualOverride = s.AllowManualOverride,
            IsDefault = s.IsDefault,
            IsSystem = s.IsSystem,
            IsActive = s.IsActive,
            DisplayOrder = s.DisplayOrder,
            Preview = NumberFormat.Compose(s, next, today, startMonth),
        };
    }
}
