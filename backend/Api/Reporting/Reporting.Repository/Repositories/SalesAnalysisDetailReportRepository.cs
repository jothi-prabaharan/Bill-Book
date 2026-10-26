using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Sales Analysis Detail Report (TK-229): the Sales Analysis before it is added up, one row for
/// each line sold, grouped by item, item category or customer, a page of lines at a time. It is the
/// Sales Analysis's own lines (<see cref="SalesAnalysisReportRepository.Lines"/>, optionally narrowed to
/// one item), so what a line is worth in base currency, how it is costed and when its cost is pending
/// are that report's, and <b>each group's subtotal is that report's row for the group</b>: it is the
/// same sum, over every line of the group and not only those on the page.
///
/// The lines are ordered by group (by name), then date and document, so a group's lines sit together;
/// a group can carry on over the next page, which then repeats its heading. Only the page's lines are
/// read, with the subtotals of the groups on it, so a large branch costs one page. A line pending its
/// cost has no margin of its own, and a group with one has none either, as in the analysis.
/// </summary>
public sealed class SalesAnalysisDetailReportRepository
{
    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public SalesAnalysisDetailReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>The refusals a request needs beyond both dates being given, with the field each belongs to. Pure.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(SalesAnalysisDetailReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from)
        {
            refusals.Add((nameof(SalesAnalysisDetailReportRequest.ToDate), "To date cannot be before the from date."));
        }

        if (!Enum.IsDefined(request.GroupBy))
        {
            refusals.Add((nameof(SalesAnalysisDetailReportRequest.GroupBy), "Group by must be one of the options offered."));
        }

        return refusals;
    }

    /// <summary>Whether a customer is on this branch; the query filter decides, so another branch's reads as none.</summary>
    public Task<bool> ContactExistsAsync(long contactId, CancellationToken ct) =>
        _db.Contacts.AnyAsync(c => c.ContactId == contactId, ct);

    /// <summary>Whether an item is on this branch.</summary>
    public Task<bool> ItemExistsAsync(long itemId, CancellationToken ct) =>
        _db.Items.AnyAsync(i => i.ItemId == itemId, ct);

    /// <summary>Whether an item category is on this branch.</summary>
    public Task<bool> CategoryExistsAsync(long categoryId, CancellationToken ct) =>
        _db.ItemCategories.AnyAsync(c => c.ItemCategoryId == categoryId, ct);

    /// <summary>Whether a warehouse is on this branch.</summary>
    public Task<bool> WarehouseExistsAsync(long warehouseId, CancellationToken ct) =>
        _db.Warehouses.AnyAsync(w => w.WarehouseId == warehouseId, ct);

    /// <summary>The customers the filter offers, by name: the Aged Receivables list.</summary>
    public Task<List<AgedReceivablesCustomerChoiceDto>> CustomersAsync(CancellationToken ct) =>
        AgedReceivablesSummaryReportRepository.CustomerChoices(_db.Contacts).ToListAsync(ct);

    /// <summary>The active items the filter offers: the Receivable Invoice Detail's list.</summary>
    public Task<List<ReceivableInvoiceItemChoiceDto>> ItemsAsync(CancellationToken ct) =>
        ReceivableInvoiceDetailReportRepository.ItemChoices(_db.Items).ToListAsync(ct);

    /// <summary>The item categories the filter offers: the Receivable Invoice Detail's list.</summary>
    public Task<List<ReceivableInvoiceCategoryChoiceDto>> CategoriesAsync(CancellationToken ct) =>
        ReceivableInvoiceDetailReportRepository.CategoryChoices(_db.ItemCategories).ToListAsync(ct);

    /// <summary>The warehouses the filter offers: the Sales Analysis's list.</summary>
    public Task<List<SalesAnalysisWarehouseChoiceDto>> WarehousesAsync(CancellationToken ct) =>
        SalesAnalysisReportRepository.WarehouseChoices(_db.Warehouses).ToListAsync(ct);

    /// <summary>The report for a request the controller has already validated.</summary>
    public async Task<SalesAnalysisDetailReportResultDto> RunAsync(SalesAnalysisDetailReportRequest request, CancellationToken ct)
    {
        DateOnly start = request.FromDate!.Value;
        DateOnly end = request.ToDate!.Value;
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

        IQueryable<AnalysisLine> lines = Narrowed(
            SalesAnalysisReportRepository.Lines(
                _db.Invoices, _db.InvoiceDetails, _db.CreditNotes, _db.CreditNoteDetails, _db.Items, _db.StockMovements,
                AnalysisRequestOf(request), start, end),
            request);

        AnalysisGroup totals = await SalesAnalysisReportRepository.Total(lines).FirstOrDefaultAsync(ct) ?? new AnalysisGroup();

        List<AnalysisLine> page = await Ordered(lines, _db.Items, _db.ItemCategories, _db.Contacts, request.GroupBy)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        long[] keys = [.. page.Select(l => KeyOf(l, request.GroupBy)).Distinct()];
        List<AnalysisGroup> subtotals = keys.Length == 0
            ? []
            : await SalesAnalysisReportRepository.Groups(lines, request.GroupBy).Where(g => keys.Contains(g.Key)).ToListAsync(ct);

        List<LabelRef> groupLabels = keys.Length == 0
            ? []
            : await SalesAnalysisReportRepository.Labels(_db, request.GroupBy, [.. keys.Where(k => k != 0)]).ToListAsync(ct);

        long[] itemIds = [.. page.Select(l => l.ItemId).Where(i => i != 0).Distinct()];
        List<LabelRef> items = itemIds.Length == 0 ? [] : await SalesAnalysisReportRepository.Labels(_db, SalesAnalysisGroupBy.Item, itemIds).ToListAsync(ct);

        long[] contactIds = [.. page.Select(l => l.ContactId).Distinct()];
        List<LabelRef> contacts = contactIds.Length == 0 ? [] : await SalesAnalysisReportRepository.Labels(_db, SalesAnalysisGroupBy.Customer, contactIds).ToListAsync(ct);

        return Compose(request, page, totals, subtotals, groupLabels, items, contacts, baseCurrency);
    }

    // --- builders: LINQ over the read models, so a test runs them over lists -------------

    /// <summary>The analysis's request for the filters this one shares: customer, category, warehouse and credit notes.</summary>
    public static SalesAnalysisReportRequest AnalysisRequestOf(SalesAnalysisDetailReportRequest request) =>
        new()
        {
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            GroupBy = request.GroupBy,
            ContactId = request.ContactId,
            ItemCategoryId = request.ItemCategoryId,
            WarehouseId = request.WarehouseId,
            IncludeCreditNotes = request.IncludeCreditNotes,
        };

    /// <summary>The lines narrowed to one item when one is chosen. LINQ only.</summary>
    public static IQueryable<AnalysisLine> Narrowed(IQueryable<AnalysisLine> lines, SalesAnalysisDetailReportRequest request) =>
        request.ItemId is long item ? lines.Where(l => l.ItemId == item) : lines;

    /// <summary>
    /// The lines in the order they are listed: by group name, then date, then document and line, so a
    /// group's lines are together and the order is the same on every page. A line with no group (no item,
    /// no category) sorts first, under an empty name.
    /// </summary>
    public static IQueryable<AnalysisLine> Ordered(
        IQueryable<AnalysisLine> lines,
        IQueryable<ItemRead> items,
        IQueryable<ItemCategoryRead> categories,
        IQueryable<ContactRead> contacts,
        SalesAnalysisGroupBy by)
    {
        switch (by)
        {
            case SalesAnalysisGroupBy.Category:
                return (from l in lines
                        join c in categories on l.CategoryId equals c.ItemCategoryId into gc
                        from c in gc.DefaultIfEmpty()
                        orderby (c == null ? string.Empty : c.CategoryName), l.CategoryId, l.DocumentDate, l.TypeCode, l.DocumentId, l.LineKey
                        select l);
            case SalesAnalysisGroupBy.Customer:
                return (from l in lines
                        join c in contacts on l.ContactId equals c.ContactId into gc
                        from c in gc.DefaultIfEmpty()
                        orderby (c == null ? string.Empty : c.DisplayName), l.ContactId, l.DocumentDate, l.TypeCode, l.DocumentId, l.LineKey
                        select l);
            default:
                return (from l in lines
                        join i in items on l.ItemId equals i.ItemId into gi
                        from i in gi.DefaultIfEmpty()
                        orderby (i == null ? string.Empty : i.ItemName), l.ItemId, l.DocumentDate, l.TypeCode, l.DocumentId, l.LineKey
                        select l);
        }
    }

    /// <summary>The key a line is grouped under. Pure.</summary>
    public static long KeyOf(AnalysisLine line, SalesAnalysisGroupBy by) =>
        by switch
        {
            SalesAnalysisGroupBy.Category => line.CategoryId,
            SalesAnalysisGroupBy.Customer => line.ContactId,
            _ => line.ItemId,
        };

    // --- compose: pure, so a test needs no database --------------------------------------

    /// <summary>
    /// The page's lines gathered into their groups, in the order they came (a group's lines are
    /// together), each with its subtotal over every line it has. A line's margin is its net sales
    /// less its cost, and none while its cost is pending. Pure.
    /// </summary>
    public static SalesAnalysisDetailReportResultDto Compose(
        SalesAnalysisDetailReportRequest request,
        IReadOnlyList<AnalysisLine> page,
        AnalysisGroup totals,
        IReadOnlyList<AnalysisGroup> subtotals,
        IReadOnlyList<LabelRef> groupLabels,
        IReadOnlyList<LabelRef> itemLabels,
        IReadOnlyList<LabelRef> contactLabels,
        string baseCurrency)
    {
        Dictionary<long, AnalysisGroup> byKey = subtotals.GroupBy(g => g.Key).ToDictionary(g => g.Key, g => g.First());
        Dictionary<long, LabelRef> groupNames = groupLabels.GroupBy(l => l.Key).ToDictionary(g => g.Key, g => g.First());
        Dictionary<long, LabelRef> items = itemLabels.GroupBy(l => l.Key).ToDictionary(g => g.Key, g => g.First());
        Dictionary<long, LabelRef> contacts = contactLabels.GroupBy(l => l.Key).ToDictionary(g => g.Key, g => g.First());

        SalesAnalysisDetailLineDto Line(AnalysisLine l)
        {
            LabelRef? item = items.GetValueOrDefault(l.ItemId);
            LabelRef? contact = contacts.GetValueOrDefault(l.ContactId);
            return new SalesAnalysisDetailLineDto
            {
                LineKey = l.LineKey,
                TypeCode = l.TypeCode,
                DocumentId = l.DocumentId,
                DocumentNo = l.DocumentNo,
                DocumentDate = l.DocumentDate,
                LineNumber = l.LineNumber,
                ContactId = l.ContactId,
                ContactName = contact?.Name ?? $"Customer {l.ContactId}",
                ItemId = l.ItemId,
                ItemCode = item?.Code ?? string.Empty,
                ItemName = item?.Name ?? (l.ItemId == 0 ? SalesAnalysisReportRepository.NoneLabel(SalesAnalysisGroupBy.Item) : $"Item {l.ItemId}"),
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                Gross = l.Gross,
                Discount = l.Discount,
                Net = l.Net,
                Cost = l.Cost,
                CostPending = l.Pending > 0,
                Margin = l.Pending > 0 ? null : l.Net - l.Cost,
            };
        }

        List<SalesAnalysisDetailGroupDto> groups = [];
        foreach (IGrouping<long, AnalysisLine> inOrder in GroupAdjacent(page, request.GroupBy))
        {
            long key = inOrder.Key;
            AnalysisGroup sum = byKey.GetValueOrDefault(key) ?? new AnalysisGroup { Key = key };
            LabelRef? label = groupNames.GetValueOrDefault(key);
            (decimal? margin, decimal? percent) = SalesAnalysisReportRepository.MarginOf(sum);

            groups.Add(new SalesAnalysisDetailGroupDto
            {
                Key = key,
                Code = label?.Code ?? string.Empty,
                Name = label?.Name ?? (key == 0 ? SalesAnalysisReportRepository.NoneLabel(request.GroupBy) : $"{request.GroupBy} {key}"),
                Lines = [.. inOrder.Select(Line)],
                LineCount = sum.Count,
                Subtotal = new SalesAnalysisGroupDto
                {
                    Key = key,
                    Code = label?.Code ?? string.Empty,
                    Name = label?.Name ?? (key == 0 ? SalesAnalysisReportRepository.NoneLabel(request.GroupBy) : $"{request.GroupBy} {key}"),
                    Quantity = sum.Quantity,
                    Gross = sum.Gross,
                    Discount = sum.Discount,
                    Net = sum.Net,
                    Cost = sum.Cost,
                    CostPendingLines = sum.Pending,
                    Margin = margin,
                    MarginPercent = percent,
                },
            });
        }

        (decimal? totalMargin, decimal? totalPercent) = SalesAnalysisReportRepository.MarginOf(totals);

        return new SalesAnalysisDetailReportResultDto
        {
            FromDate = request.FromDate!.Value,
            ToDate = request.ToDate!.Value,
            BaseCurrencyCode = baseCurrency,
            GroupBy = request.GroupBy,
            IncludesCreditNotes = request.IncludeCreditNotes,
            Groups = groups,
            TotalLines = totals.Count,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            Totals = new SalesAnalysisTotalsDto
            {
                Quantity = totals.Quantity,
                Gross = totals.Gross,
                Discount = totals.Discount,
                Net = totals.Net,
                Cost = totals.Cost,
                CostPendingLines = totals.Pending,
                Margin = totalMargin,
                MarginPercent = totalPercent,
            },
        };
    }

    /// <summary>The lines in runs of the same group, in the order given: a group is one run because the page is ordered by group. Pure.</summary>
    public static IEnumerable<IGrouping<long, AnalysisLine>> GroupAdjacent(IEnumerable<AnalysisLine> lines, SalesAnalysisGroupBy by)
    {
        List<AdjacentGroup> runs = [];
        foreach (AnalysisLine line in lines)
        {
            long key = KeyOf(line, by);
            if (runs.Count > 0 && runs[^1].Key == key)
            {
                runs[^1].Add(line);
            }
            else
            {
                runs.Add(new AdjacentGroup(key) { line });
            }
        }

        return runs;
    }

    private sealed class AdjacentGroup(long key) : List<AnalysisLine>, IGrouping<long, AnalysisLine>
    {
        public long Key { get; } = key;
    }
}
