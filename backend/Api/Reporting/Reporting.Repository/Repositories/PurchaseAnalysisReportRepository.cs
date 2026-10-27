using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Purchase Analysis (TK-239): what was bought in a period, added up by item, item category or
/// vendor, largest net purchases first, a page of groups at a time. Every figure is one line of a
/// posted bill (less one of a posted debit note, which is negative) taken to the branch's base
/// currency at the rate the document was booked at, so the groups, the totals and a comparison period
/// are all sums of the same lines.
///
/// <b>Net purchases are before tax</b>: the line's taxable value, after discount, which is what a bill
/// debits to Inventory for a stock line (GST that cannot be claimed is added to the line's cost in the
/// ledger and is not here). Gross and discount are as keyed, so a price that includes tax shows it in
/// Gross. <b>A line bought as a fixed asset is left out unless asked for</b>: it is an asset and not
/// something bought for stock or resale. A line billed against a receipt is counted all the same, since
/// the report is of what was billed.
///
/// A debit note takes units off only when it is a purchase return (the only reason that sends goods
/// back); a price correction or a discount takes net purchases off and no units. The <b>average
/// cost</b> is net purchases over units, and is given only to an item when adding up by item, where a unit
/// means one thing. The compare period is a second pair of dates and compares net purchases only.
/// </summary>
public sealed class PurchaseAnalysisReportRepository
{
    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public PurchaseAnalysisReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>The refusals a request needs beyond both dates being given, with the field each belongs to. Pure.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(PurchaseAnalysisReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from)
        {
            refusals.Add((nameof(PurchaseAnalysisReportRequest.ToDate), "To date cannot be before the from date."));
        }

        if (!Enum.IsDefined(request.GroupBy))
        {
            refusals.Add((nameof(PurchaseAnalysisReportRequest.GroupBy), "Group by must be one of the options offered."));
        }

        if (request.CompareFromDate is not null && request.CompareToDate is null)
        {
            refusals.Add((nameof(PurchaseAnalysisReportRequest.CompareToDate), "Compare to date is required when a compare from date is given."));
        }
        else if (request.CompareToDate is not null && request.CompareFromDate is null)
        {
            refusals.Add((nameof(PurchaseAnalysisReportRequest.CompareFromDate), "Compare from date is required when a compare to date is given."));
        }
        else if (request.CompareFromDate is DateOnly compareFrom && request.CompareToDate is DateOnly compareTo && compareTo < compareFrom)
        {
            refusals.Add((nameof(PurchaseAnalysisReportRequest.CompareToDate), "Compare to date cannot be before the compare from date."));
        }

        return refusals;
    }

    /// <summary>Whether a vendor is on this branch; the query filter decides, so another branch's reads as none.</summary>
    public Task<bool> ContactExistsAsync(long contactId, CancellationToken ct) =>
        _db.Contacts.AnyAsync(c => c.ContactId == contactId, ct);

    /// <summary>Whether an item category is on this branch.</summary>
    public Task<bool> CategoryExistsAsync(long categoryId, CancellationToken ct) =>
        _db.ItemCategories.AnyAsync(c => c.ItemCategoryId == categoryId, ct);

    /// <summary>The vendors the filter offers, by name: the Aged Payables list.</summary>
    public Task<List<AgedPayablesVendorChoiceDto>> VendorsAsync(CancellationToken ct) =>
        AgedPayablesSummaryReportRepository.VendorChoices(_db.Contacts).ToListAsync(ct);

    /// <summary>The item categories the filter offers: the Payable Invoice Detail's list.</summary>
    public Task<List<PayableInvoiceCategoryChoiceDto>> CategoriesAsync(CancellationToken ct) =>
        PayableInvoiceDetailReportRepository.CategoryChoices(_db.ItemCategories).ToListAsync(ct);

    /// <summary>The report for a request the controller has already validated.</summary>
    public async Task<PurchaseAnalysisReportResultDto> RunAsync(PurchaseAnalysisReportRequest request, CancellationToken ct)
    {
        DateOnly start = request.FromDate!.Value;
        DateOnly end = request.ToDate!.Value;
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

        IQueryable<PurchaseLine> lines = LinesFor(start, end, request);
        IQueryable<PurchaseGroup> groups = Groups(lines, request.GroupBy);

        PurchaseGroup totals = await Total(lines).FirstOrDefaultAsync(ct) ?? new PurchaseGroup();
        int groupCount = await groups.CountAsync(ct);

        List<PurchaseGroup> page = await groups
            .OrderByDescending(g => g.Net)
            .ThenBy(g => g.Key)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        List<PurchaseGroup> compare = [];
        decimal? compareTotal = null;
        if (request.CompareFromDate is DateOnly compareFrom && request.CompareToDate is DateOnly compareTo)
        {
            IQueryable<PurchaseLine> compareLines = LinesFor(compareFrom, compareTo, request);
            long[] keys = [.. page.Select(g => g.Key)];
            compare = keys.Length == 0 ? [] : await Groups(compareLines, request.GroupBy).Where(g => keys.Contains(g.Key)).ToListAsync(ct);
            compareTotal = (await Total(compareLines).FirstOrDefaultAsync(ct))?.Net ?? 0m;
        }

        List<LabelRef> labels = await LabelsAsync(request.GroupBy, [.. page.Select(g => g.Key).Where(k => k != 0)], ct);

        return Compose(request, page, groupCount, labels, compare, totals, compareTotal, baseCurrency);
    }

    private IQueryable<PurchaseLine> LinesFor(DateOnly start, DateOnly end, PurchaseAnalysisReportRequest request) =>
        Lines(_db.Bills, _db.BillDetails, _db.DebitNotes, _db.DebitNoteDetails, _db.Items, request, start, end);

    private Task<List<LabelRef>> LabelsAsync(PurchaseAnalysisGroupBy by, IReadOnlyCollection<long> keys, CancellationToken ct) =>
        keys.Count == 0 ? Task.FromResult(new List<LabelRef>()) : Labels(_db, by, keys).ToListAsync(ct);

    /// <summary>The codes and names of the given items, categories or vendors. LINQ only.</summary>
    public static IQueryable<LabelRef> Labels(ReportingDbContext db, PurchaseAnalysisGroupBy by, IReadOnlyCollection<long> keys) =>
        by switch
        {
            PurchaseAnalysisGroupBy.Item => db.Items
                .Where(i => keys.Contains(i.ItemId))
                .Select(i => new LabelRef { Key = i.ItemId, Code = i.ItemCode, Name = i.ItemName }),
            PurchaseAnalysisGroupBy.Category => db.ItemCategories
                .Where(c => keys.Contains(c.ItemCategoryId))
                .Select(c => new LabelRef { Key = c.ItemCategoryId, Code = c.CategoryCode, Name = c.CategoryName }),
            _ => db.Contacts
                .Where(c => keys.Contains(c.ContactId))
                .Select(c => new LabelRef { Key = c.ContactId, Code = c.ContactCode, Name = c.DisplayName }),
        };

    // --- builders: LINQ over the read models, so a test runs them over lists -------------

    /// <summary>
    /// Every line bought in the period, one row each, in base currency: the posted bills' lines, and
    /// when asked the posted debit notes' lines as negatives. Lines bought as fixed assets are left
    /// out unless asked for. Narrowed by vendor and item category.
    /// </summary>
    public static IQueryable<PurchaseLine> Lines(
        IQueryable<BillRead> bills,
        IQueryable<BillDetailRead> details,
        IQueryable<DebitNoteRead> debitNotes,
        IQueryable<DebitNoteDetailRead> debitDetails,
        IQueryable<ItemRead> items,
        PurchaseAnalysisReportRequest request,
        DateOnly start,
        DateOnly end)
    {
        IQueryable<BillDetailRead> billLines = request.IncludeFixedAssets
            ? details
            : details.Where(d => d.LineType != DocumentLineType.Capital);

        IQueryable<PurchaseLine> lines =
            from d in billLines
            join b in bills on d.BillId equals b.BillId
            join iv in items on d.ItemId equals (long?)iv.ItemId into itemed
            from it in itemed.DefaultIfEmpty()
            where b.Status == DocumentStatus.Posted && b.DocumentDate >= start && b.DocumentDate <= end
            select new PurchaseLine
            {
                LineKey = d.BillDetailId,
                TypeCode = b.TransactionTypeCode,
                DocumentId = b.BillId,
                DocumentNo = b.DocumentNo,
                DocumentDate = b.DocumentDate,
                LineNumber = d.LineNumber,
                ContactId = b.ContactId,
                ItemId = d.ItemId ?? 0L,
                CategoryId = it == null ? 0L : (it.ItemCategoryId ?? 0L),
                Quantity = d.BaseQuantity,
                Gross = Math.Round(d.GrossAmount * b.ExchangeRate, 2),
                Discount = Math.Round(d.DiscountAmount * b.ExchangeRate, 2),
                Net = Math.Round(d.TaxableAmount * b.ExchangeRate, 2),
            };

        if (request.IncludeDebitNotes)
        {
            IQueryable<DebitNoteDetailRead> debitLines = request.IncludeFixedAssets
                ? debitDetails
                : debitDetails.Where(d => d.LineType != DocumentLineType.Capital);

            IQueryable<PurchaseLine> credits =
                from d in debitLines
                join n in debitNotes on d.DebitNoteId equals n.DebitNoteId
                join iv in items on d.ItemId equals (long?)iv.ItemId into itemed
                from it in itemed.DefaultIfEmpty()
                where n.Status == DocumentStatus.Posted && n.DocumentDate >= start && n.DocumentDate <= end
                select new PurchaseLine
                {
                    LineKey = -d.DebitNoteDetailId,
                    TypeCode = n.TransactionTypeCode,
                    DocumentId = n.DebitNoteId,
                    DocumentNo = n.DocumentNo,
                    DocumentDate = n.DocumentDate,
                    LineNumber = d.LineNumber,
                    ContactId = n.ContactId,
                    ItemId = d.ItemId ?? 0L,
                    CategoryId = it == null ? 0L : (it.ItemCategoryId ?? 0L),
                    Quantity = n.ReasonCode == DebitNoteReason.PurchaseReturn ? -d.BaseQuantity : 0m,
                    Gross = -Math.Round(d.GrossAmount * n.ExchangeRate, 2),
                    Discount = -Math.Round(d.DiscountAmount * n.ExchangeRate, 2),
                    Net = -Math.Round(d.TaxableAmount * n.ExchangeRate, 2),
                };

            lines = lines.Concat(credits);
        }

        if (request.ContactId is long contact)
        {
            lines = lines.Where(l => l.ContactId == contact);
        }

        if (request.ItemCategoryId is long category)
        {
            lines = lines.Where(l => l.CategoryId == category);
        }

        return lines;
    }

    /// <summary>The lines added up by item, category or vendor. A line with no item or category is the group with key nought.</summary>
    public static IQueryable<PurchaseGroup> Groups(IQueryable<PurchaseLine> lines, PurchaseAnalysisGroupBy by) =>
        by switch
        {
            PurchaseAnalysisGroupBy.Category => Summarise(lines.GroupBy(l => l.CategoryId)),
            PurchaseAnalysisGroupBy.Vendor => Summarise(lines.GroupBy(l => l.ContactId)),
            _ => Summarise(lines.GroupBy(l => l.ItemId)),
        };

    private static IQueryable<PurchaseGroup> Summarise(IQueryable<IGrouping<long, PurchaseLine>> groups) =>
        groups.Select(g => new PurchaseGroup
        {
            Key = g.Key,
            Count = g.Count(),
            Quantity = g.Sum(l => l.Quantity),
            Gross = g.Sum(l => l.Gross),
            Discount = g.Sum(l => l.Discount),
            Net = g.Sum(l => l.Net),
        });

    /// <summary>The lines added up into one row, none if there are none.</summary>
    public static IQueryable<PurchaseGroup> Total(IQueryable<PurchaseLine> lines) =>
        lines
            .GroupBy(l => 1)
            .Select(g => new PurchaseGroup
            {
                Key = 0,
                Count = g.Count(),
                Quantity = g.Sum(l => l.Quantity),
                Gross = g.Sum(l => l.Gross),
                Discount = g.Sum(l => l.Discount),
                Net = g.Sum(l => l.Net),
            });

    // --- compose: pure, so a test needs no database --------------------------------------

    /// <summary>
    /// The page's groups with their names, average costs, shares and changes, and the totals. The
    /// average cost is given only when adding up by item and the group has units; a share only when the
    /// total is something; a change only where a comparison period was asked for and had purchases. A
    /// group of lines with no item (or category) is named for that. Pure.
    /// </summary>
    public static PurchaseAnalysisReportResultDto Compose(
        PurchaseAnalysisReportRequest request,
        IReadOnlyList<PurchaseGroup> page,
        int totalGroups,
        IReadOnlyList<LabelRef> labels,
        IReadOnlyList<PurchaseGroup> compare,
        PurchaseGroup totals,
        decimal? compareTotal,
        string baseCurrency)
    {
        Dictionary<long, LabelRef> names = labels.GroupBy(l => l.Key).ToDictionary(g => g.Key, g => g.First());
        Dictionary<long, decimal> compared = compare.GroupBy(c => c.Key).ToDictionary(g => g.Key, g => g.Sum(c => c.Net));
        bool comparing = request.CompareFromDate is not null && request.CompareToDate is not null;

        PurchaseAnalysisGroupDto Row(PurchaseGroup g)
        {
            LabelRef? label = names.GetValueOrDefault(g.Key);
            (string code, string name) = label is not null
                ? (label.Code, label.Name)
                : g.Key == 0 ? (string.Empty, NoneLabel(request.GroupBy)) : (string.Empty, $"{request.GroupBy} {g.Key}");
            decimal? compareNet = comparing ? compared.GetValueOrDefault(g.Key) : null;

            return new PurchaseAnalysisGroupDto
            {
                Key = g.Key,
                Code = code,
                Name = name,
                Quantity = g.Quantity,
                Gross = g.Gross,
                Discount = g.Discount,
                Net = g.Net,
                AverageCost = AverageCostOf(g, request.GroupBy),
                SharePercent = ShareOf(g.Net, totals.Net),
                CompareNet = compareNet,
                ChangePercent = ChangeOf(g.Net, compareNet),
            };
        }

        return new PurchaseAnalysisReportResultDto
        {
            FromDate = request.FromDate!.Value,
            ToDate = request.ToDate!.Value,
            CompareFromDate = request.CompareFromDate,
            CompareToDate = request.CompareToDate,
            BaseCurrencyCode = baseCurrency,
            GroupBy = request.GroupBy,
            IncludesDebitNotes = request.IncludeDebitNotes,
            IncludesFixedAssets = request.IncludeFixedAssets,
            Groups = [.. page.Select(Row)],
            TotalGroups = totalGroups,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            Totals = new PurchaseAnalysisTotalsDto
            {
                Quantity = totals.Quantity,
                Gross = totals.Gross,
                Discount = totals.Discount,
                Net = totals.Net,
                CompareNet = comparing ? compareTotal ?? 0m : null,
                ChangePercent = ChangeOf(totals.Net, comparing ? compareTotal ?? 0m : null),
            },
        };
    }

    /// <summary>What a group with no item, category or vendor is called.</summary>
    public static string NoneLabel(PurchaseAnalysisGroupBy by) =>
        by switch
        {
            PurchaseAnalysisGroupBy.Category => "(No category)",
            PurchaseAnalysisGroupBy.Vendor => "(No vendor)",
            _ => "(No item)",
        };

    /// <summary>
    /// Net purchases per unit, to six places as a unit price is kept; null unless adding up by item, for the
    /// group of lines with no item (a service, freight: nothing there is counted in one unit), and when there
    /// are no units. The whole is not given one either, since it mixes every item's unit. Pure.
    /// </summary>
    public static decimal? AverageCostOf(PurchaseGroup group, PurchaseAnalysisGroupBy by) =>
        by == PurchaseAnalysisGroupBy.Item && group.Key != 0 && group.Quantity > 0m ? Math.Round(group.Net / group.Quantity, 6) : null;

    /// <summary>A group's share of the whole, as a percentage to two places; null when the whole is nought. Pure.</summary>
    public static decimal? ShareOf(decimal net, decimal totalNet) =>
        totalNet == 0m ? null : Math.Round(net * 100m / totalNet, 2);

    /// <summary>How far net purchases moved from the comparison period, as a percentage of it; null with no comparison or none bought in it. Pure.</summary>
    public static decimal? ChangeOf(decimal net, decimal? compareNet) =>
        compareNet is decimal before && before != 0m ? Math.Round((net - before) * 100m / Math.Abs(before), 2) : null;
}

/// <summary>One line bought, in base currency, signed (a debit note's lines are negative).</summary>
public sealed class PurchaseLine
{
    /// <summary>Unique across bill and debit note lines: a bill line's id, or a debit note line's id negated.</summary>
    public long LineKey { get; set; }

    /// <summary>BIL for a bill, DBN for a debit note.</summary>
    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    public int LineNumber { get; set; }

    public long ContactId { get; set; }

    /// <summary>The item, or nought for a line with none.</summary>
    public long ItemId { get; set; }

    /// <summary>The item's category, or nought for none.</summary>
    public long CategoryId { get; set; }

    public decimal Quantity { get; set; }

    public decimal Gross { get; set; }

    public decimal Discount { get; set; }

    public decimal Net { get; set; }
}

/// <summary>Lines added up by item, category or vendor.</summary>
public sealed class PurchaseGroup
{
    public long Key { get; set; }

    /// <summary>How many lines were added up.</summary>
    public int Count { get; set; }

    public decimal Quantity { get; set; }

    public decimal Gross { get; set; }

    public decimal Discount { get; set; }

    public decimal Net { get; set; }
}
