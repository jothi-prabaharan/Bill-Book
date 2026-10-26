using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Sales Analysis (TK-228): what sold in a period, added up by item, item category or customer,
/// largest net sales first, a page of groups at a time. Every figure is one line of a posted
/// invoice or POS sale (less one of a posted credit note, which is negative) taken to the branch's
/// base currency at the rate the document was booked at, so the groups, the totals and a comparison
/// period are all sums of the same lines.
///
/// <b>Net sales are before tax</b>: the line's taxable value, after discount. Gross and discount
/// are as keyed, so a price that includes tax shows it in Gross, and gross less discount is the net
/// only where prices exclude tax.
///
/// <b>Cost of sales comes from the stock movement a line caused</b>, found by the document's own
/// type, id and line id (how Sales records the issue and a return). Only a movement that has been
/// <i>costed</i> counts: costing is asynchronous, so a line whose movement is pending, in progress or
/// failed is counted in <c>CostPendingLines</c> and its cost is not taken as nothing; a group or total
/// with such a line has no margin, since the margin would be overstated. A line with no movement (a
/// service, or an item that is not stocked) costs nothing and is not pending. A credit note that
/// returns goods puts the cost back, as a negative; one that corrects a price or gives a discount
/// reduces net sales only, and no units.
/// </summary>
public sealed class SalesAnalysisReportRepository
{
    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public SalesAnalysisReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>The refusals a request needs beyond both dates being given, with the field each belongs to. Pure.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(SalesAnalysisReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from)
        {
            refusals.Add((nameof(SalesAnalysisReportRequest.ToDate), "To date cannot be before the from date."));
        }

        if (!Enum.IsDefined(request.GroupBy))
        {
            refusals.Add((nameof(SalesAnalysisReportRequest.GroupBy), "Group by must be one of the options offered."));
        }

        if (request.CompareFromDate is not null && request.CompareToDate is null)
        {
            refusals.Add((nameof(SalesAnalysisReportRequest.CompareToDate), "Compare to date is required when a compare from date is given."));
        }
        else if (request.CompareToDate is not null && request.CompareFromDate is null)
        {
            refusals.Add((nameof(SalesAnalysisReportRequest.CompareFromDate), "Compare from date is required when a compare to date is given."));
        }
        else if (request.CompareFromDate is DateOnly compareFrom && request.CompareToDate is DateOnly compareTo && compareTo < compareFrom)
        {
            refusals.Add((nameof(SalesAnalysisReportRequest.CompareToDate), "Compare to date cannot be before the compare from date."));
        }

        return refusals;
    }

    /// <summary>Whether a customer is on this branch; the query filter decides, so another branch's reads as none.</summary>
    public Task<bool> ContactExistsAsync(long contactId, CancellationToken ct) =>
        _db.Contacts.AnyAsync(c => c.ContactId == contactId, ct);

    /// <summary>Whether an item category is on this branch.</summary>
    public Task<bool> CategoryExistsAsync(long categoryId, CancellationToken ct) =>
        _db.ItemCategories.AnyAsync(c => c.ItemCategoryId == categoryId, ct);

    /// <summary>Whether a warehouse is on this branch.</summary>
    public Task<bool> WarehouseExistsAsync(long warehouseId, CancellationToken ct) =>
        _db.Warehouses.AnyAsync(w => w.WarehouseId == warehouseId, ct);

    /// <summary>The customers the filter offers, by name: the Aged Receivables list.</summary>
    public Task<List<AgedReceivablesCustomerChoiceDto>> CustomersAsync(CancellationToken ct) =>
        AgedReceivablesSummaryReportRepository.CustomerChoices(_db.Contacts).ToListAsync(ct);

    /// <summary>The item categories the filter offers: the Receivable Invoice Detail's list.</summary>
    public Task<List<ReceivableInvoiceCategoryChoiceDto>> CategoriesAsync(CancellationToken ct) =>
        ReceivableInvoiceDetailReportRepository.CategoryChoices(_db.ItemCategories).ToListAsync(ct);

    /// <summary>The warehouses the filter offers, by name.</summary>
    public Task<List<SalesAnalysisWarehouseChoiceDto>> WarehousesAsync(CancellationToken ct) =>
        WarehouseChoices(_db.Warehouses).ToListAsync(ct);

    /// <summary>The report for a request the controller has already validated.</summary>
    public async Task<SalesAnalysisReportResultDto> RunAsync(SalesAnalysisReportRequest request, CancellationToken ct)
    {
        DateOnly start = request.FromDate!.Value;
        DateOnly end = request.ToDate!.Value;
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

        IQueryable<AnalysisLine> lines = LinesFor(start, end, request);
        IQueryable<AnalysisGroup> groups = Groups(lines, request.GroupBy);

        AnalysisGroup totals = await Total(lines).FirstOrDefaultAsync(ct) ?? new AnalysisGroup();
        int groupCount = await groups.CountAsync(ct);

        List<AnalysisGroup> page = await groups
            .OrderByDescending(g => g.Net)
            .ThenBy(g => g.Key)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        List<AnalysisGroup> compare = [];
        decimal? compareTotal = null;
        if (request.CompareFromDate is DateOnly compareFrom && request.CompareToDate is DateOnly compareTo)
        {
            IQueryable<AnalysisLine> compareLines = LinesFor(compareFrom, compareTo, request);
            long[] keys = [.. page.Select(g => g.Key)];
            compare = keys.Length == 0 ? [] : await Groups(compareLines, request.GroupBy).Where(g => keys.Contains(g.Key)).ToListAsync(ct);
            compareTotal = (await Total(compareLines).FirstOrDefaultAsync(ct))?.Net ?? 0m;
        }

        List<LabelRef> labels = await LabelsAsync(request.GroupBy, [.. page.Select(g => g.Key).Where(k => k != 0)], ct);

        return Compose(request, page, groupCount, labels, compare, totals, compareTotal, baseCurrency);
    }

    private IQueryable<AnalysisLine> LinesFor(DateOnly start, DateOnly end, SalesAnalysisReportRequest request) =>
        Lines(_db.Invoices, _db.InvoiceDetails, _db.CreditNotes, _db.CreditNoteDetails, _db.Items, _db.StockMovements, request, start, end);

    private Task<List<LabelRef>> LabelsAsync(SalesAnalysisGroupBy by, IReadOnlyCollection<long> keys, CancellationToken ct) =>
        keys.Count == 0 ? Task.FromResult(new List<LabelRef>()) : Labels(_db, by, keys).ToListAsync(ct);

    /// <summary>The codes and names of the given items, categories or customers. LINQ only.</summary>
    public static IQueryable<LabelRef> Labels(ReportingDbContext db, SalesAnalysisGroupBy by, IReadOnlyCollection<long> keys) =>
        by switch
        {
            SalesAnalysisGroupBy.Item => db.Items
                .Where(i => keys.Contains(i.ItemId))
                .Select(i => new LabelRef { Key = i.ItemId, Code = i.ItemCode, Name = i.ItemName }),
            SalesAnalysisGroupBy.Category => db.ItemCategories
                .Where(c => keys.Contains(c.ItemCategoryId))
                .Select(c => new LabelRef { Key = c.ItemCategoryId, Code = c.CategoryCode, Name = c.CategoryName }),
            _ => db.Contacts
                .Where(c => keys.Contains(c.ContactId))
                .Select(c => new LabelRef { Key = c.ContactId, Code = c.ContactCode, Name = c.DisplayName }),
        };

    // --- builders: LINQ over the read models, so a test runs them over lists -------------

    /// <summary>
    /// Every line sold in the period, one row each, in base currency: the posted invoices' and POS
    /// sales' lines, and when asked the posted credit notes' lines as negatives. A line's cost is its
    /// stock movement's total when that is costed, and is pending when the movement is not; a line
    /// with no movement costs nothing. Narrowed by customer, item category and warehouse.
    /// </summary>
    public static IQueryable<AnalysisLine> Lines(
        IQueryable<InvoiceRead> invoices,
        IQueryable<InvoiceDetailRead> details,
        IQueryable<CreditNoteRead> creditNotes,
        IQueryable<CreditNoteDetailRead> creditDetails,
        IQueryable<ItemRead> items,
        IQueryable<StockMovementRead> movements,
        SalesAnalysisReportRequest request,
        DateOnly start,
        DateOnly end)
    {
        IQueryable<AnalysisLine> lines =
            from d in details
            join i in invoices on d.InvoiceId equals i.InvoiceId
            join mv in movements
                on new { Type = i.TransactionTypeCode, Id = (long?)i.InvoiceId, Line = d.InvoiceDetailId }
                equals new { Type = mv.SourceType, Id = mv.SourceId, Line = mv.SourceLineId }
                into moved
            from m in moved.DefaultIfEmpty()
            join iv in items on d.ItemId equals (long?)iv.ItemId into itemed
            from it in itemed.DefaultIfEmpty()
            where i.Status == DocumentStatus.Posted
                && (i.TransactionTypeCode == "INV" || i.TransactionTypeCode == "POS")
                && i.DocumentDate >= start && i.DocumentDate <= end
            select new AnalysisLine
            {
                LineKey = d.InvoiceDetailId,
                TypeCode = i.TransactionTypeCode,
                DocumentId = i.InvoiceId,
                DocumentNo = i.DocumentNo,
                DocumentDate = i.DocumentDate,
                LineNumber = d.LineNumber,
                UnitPrice = Math.Round(d.UnitPrice * i.ExchangeRate, 6),
                ContactId = i.ContactId,
                ItemId = d.ItemId ?? 0L,
                CategoryId = it == null ? 0L : (it.ItemCategoryId ?? 0L),
                WarehouseId = d.WarehouseId,
                Quantity = d.BaseQuantity,
                Gross = Math.Round(d.GrossAmount * i.ExchangeRate, 2),
                Discount = Math.Round(d.DiscountAmount * i.ExchangeRate, 2),
                Net = Math.Round(d.TaxableAmount * i.ExchangeRate, 2),
                Cost = m != null && m.CostingStatus == CostingStatus.Costed ? (m.TotalCost ?? 0m) : 0m,
                Pending = m != null && m.CostingStatus != CostingStatus.Costed && m.CostingStatus != CostingStatus.Skipped ? 1 : 0,
            };

        if (request.IncludeCreditNotes)
        {
            IQueryable<AnalysisLine> credits =
                from d in creditDetails
                join c in creditNotes on d.CreditNoteId equals c.CreditNoteId
                join mv in movements
                    on new { Type = c.TransactionTypeCode, Id = (long?)c.CreditNoteId, Line = d.CreditNoteDetailId }
                    equals new { Type = mv.SourceType, Id = mv.SourceId, Line = mv.SourceLineId }
                    into moved
                from m in moved.DefaultIfEmpty()
                join iv in items on d.ItemId equals (long?)iv.ItemId into itemed
                from it in itemed.DefaultIfEmpty()
                where c.Status == DocumentStatus.Posted
                    && c.DocumentDate >= start && c.DocumentDate <= end
                select new AnalysisLine
                {
                    LineKey = -d.CreditNoteDetailId,
                    TypeCode = c.TransactionTypeCode,
                    DocumentId = c.CreditNoteId,
                    DocumentNo = c.DocumentNo,
                    DocumentDate = c.DocumentDate,
                    LineNumber = d.LineNumber,
                    UnitPrice = Math.Round(d.UnitPrice * c.ExchangeRate, 6),
                    ContactId = c.ContactId,
                    ItemId = d.ItemId ?? 0L,
                    CategoryId = it == null ? 0L : (it.ItemCategoryId ?? 0L),
                    WarehouseId = d.WarehouseId,
                    Quantity = c.ReasonCode == CreditNoteReason.SalesReturn ? -d.BaseQuantity : 0m,
                    Gross = -Math.Round(d.GrossAmount * c.ExchangeRate, 2),
                    Discount = -Math.Round(d.DiscountAmount * c.ExchangeRate, 2),
                    Net = -Math.Round(d.TaxableAmount * c.ExchangeRate, 2),
                    Cost = m != null && m.CostingStatus == CostingStatus.Costed ? -(m.TotalCost ?? 0m) : 0m,
                    Pending = m != null && m.CostingStatus != CostingStatus.Costed && m.CostingStatus != CostingStatus.Skipped ? 1 : 0,
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

        if (request.WarehouseId is long warehouse)
        {
            lines = lines.Where(l => l.WarehouseId == warehouse);
        }

        return lines;
    }

    /// <summary>The lines added up by item, category or customer. A line with no item or category is the group with key nought.</summary>
    public static IQueryable<AnalysisGroup> Groups(IQueryable<AnalysisLine> lines, SalesAnalysisGroupBy by) =>
        by switch
        {
            SalesAnalysisGroupBy.Category => Summarise(lines.GroupBy(l => l.CategoryId)),
            SalesAnalysisGroupBy.Customer => Summarise(lines.GroupBy(l => l.ContactId)),
            _ => Summarise(lines.GroupBy(l => l.ItemId)),
        };

    private static IQueryable<AnalysisGroup> Summarise(IQueryable<IGrouping<long, AnalysisLine>> groups) =>
        groups.Select(g => new AnalysisGroup
        {
            Key = g.Key,
            Count = g.Count(),
            Quantity = g.Sum(l => l.Quantity),
            Gross = g.Sum(l => l.Gross),
            Discount = g.Sum(l => l.Discount),
            Net = g.Sum(l => l.Net),
            Cost = g.Sum(l => l.Cost),
            Pending = g.Sum(l => l.Pending),
        });

    /// <summary>The lines added up into one row, none if there are none.</summary>
    public static IQueryable<AnalysisGroup> Total(IQueryable<AnalysisLine> lines) =>
        lines
            .GroupBy(l => 1)
            .Select(g => new AnalysisGroup
            {
                Key = 0,
                Count = g.Count(),
                Quantity = g.Sum(l => l.Quantity),
                Gross = g.Sum(l => l.Gross),
                Discount = g.Sum(l => l.Discount),
                Net = g.Sum(l => l.Net),
                Cost = g.Sum(l => l.Cost),
                Pending = g.Sum(l => l.Pending),
            });

    /// <summary>The warehouses a filter offers, by name. LINQ only.</summary>
    public static IQueryable<SalesAnalysisWarehouseChoiceDto> WarehouseChoices(IQueryable<WarehouseRead> warehouses) =>
        warehouses
            .Where(w => w.IsActive)
            .OrderBy(w => w.WarehouseName)
            .ThenBy(w => w.WarehouseId)
            .Select(w => new SalesAnalysisWarehouseChoiceDto { WarehouseId = w.WarehouseId, WarehouseCode = w.WarehouseCode, WarehouseName = w.WarehouseName });

    // --- compose: pure, so a test needs no database --------------------------------------

    /// <summary>
    /// The page's groups with their names, margins and changes, and the totals. A margin is given
    /// only where no line's cost is pending; a change only where a comparison period was asked for
    /// and had sales. A group of lines with no item (or category) is named for that. Pure.
    /// </summary>
    public static SalesAnalysisReportResultDto Compose(
        SalesAnalysisReportRequest request,
        IReadOnlyList<AnalysisGroup> page,
        int totalGroups,
        IReadOnlyList<LabelRef> labels,
        IReadOnlyList<AnalysisGroup> compare,
        AnalysisGroup totals,
        decimal? compareTotal,
        string baseCurrency)
    {
        Dictionary<long, LabelRef> names = labels.GroupBy(l => l.Key).ToDictionary(g => g.Key, g => g.First());
        Dictionary<long, decimal> compared = compare.GroupBy(c => c.Key).ToDictionary(g => g.Key, g => g.Sum(c => c.Net));
        bool comparing = request.CompareFromDate is not null && request.CompareToDate is not null;

        SalesAnalysisGroupDto Row(AnalysisGroup g)
        {
            LabelRef? label = names.GetValueOrDefault(g.Key);
            (string code, string name) = label is not null
                ? (label.Code, label.Name)
                : g.Key == 0 ? (string.Empty, NoneLabel(request.GroupBy)) : (string.Empty, $"{request.GroupBy} {g.Key}");
            decimal? compareNet = comparing ? compared.GetValueOrDefault(g.Key) : null;
            (decimal? margin, decimal? marginPercent) = MarginOf(g);

            return new SalesAnalysisGroupDto
            {
                Key = g.Key,
                Code = code,
                Name = name,
                Quantity = g.Quantity,
                Gross = g.Gross,
                Discount = g.Discount,
                Net = g.Net,
                Cost = g.Cost,
                CostPendingLines = g.Pending,
                Margin = margin,
                MarginPercent = marginPercent,
                CompareNet = compareNet,
                ChangePercent = ChangeOf(g.Net, compareNet),
            };
        }

        (decimal? totalMargin, decimal? totalMarginPercent) = MarginOf(totals);

        return new SalesAnalysisReportResultDto
        {
            FromDate = request.FromDate!.Value,
            ToDate = request.ToDate!.Value,
            CompareFromDate = request.CompareFromDate,
            CompareToDate = request.CompareToDate,
            BaseCurrencyCode = baseCurrency,
            GroupBy = request.GroupBy,
            IncludesCreditNotes = request.IncludeCreditNotes,
            Groups = [.. page.Select(Row)],
            TotalGroups = totalGroups,
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
                MarginPercent = totalMarginPercent,
                CompareNet = comparing ? compareTotal ?? 0m : null,
                ChangePercent = ChangeOf(totals.Net, comparing ? compareTotal ?? 0m : null),
            },
        };
    }

    /// <summary>What a group with no item, category or customer is called.</summary>
    public static string NoneLabel(SalesAnalysisGroupBy by) =>
        by switch
        {
            SalesAnalysisGroupBy.Category => "(No category)",
            SalesAnalysisGroupBy.Customer => "(No customer)",
            _ => "(No item)",
        };

    /// <summary>Net sales less cost and as a share of net sales; both null while any line's cost is pending, the percent also with no net sales. Pure.</summary>
    public static (decimal? Margin, decimal? Percent) MarginOf(AnalysisGroup group)
    {
        if (group.Pending > 0)
        {
            return (null, null);
        }

        decimal margin = group.Net - group.Cost;
        return (margin, group.Net == 0m ? null : Math.Round(margin * 100m / group.Net, 2));
    }

    /// <summary>How far net sales moved from the comparison period, as a percentage of it; null with no comparison or none sold in it. Pure.</summary>
    public static decimal? ChangeOf(decimal net, decimal? compareNet) =>
        compareNet is decimal before && before != 0m ? Math.Round((net - before) * 100m / Math.Abs(before), 2) : null;
}

/// <summary>One line sold, in base currency, signed (a credit note's lines are negative).</summary>
public sealed class AnalysisLine
{
    /// <summary>Unique across invoice and credit note lines: an invoice line's id, or a credit note line's id negated.</summary>
    public long LineKey { get; set; }

    /// <summary>INV or POS for an invoice, CRN for a credit note.</summary>
    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    public int LineNumber { get; set; }

    /// <summary>The price of one unit in base currency, as booked (a credit note's is positive).</summary>
    public decimal UnitPrice { get; set; }

    public long ContactId { get; set; }

    /// <summary>The item, or nought for a line with none.</summary>
    public long ItemId { get; set; }

    /// <summary>The item's category, or nought for none.</summary>
    public long CategoryId { get; set; }

    public long? WarehouseId { get; set; }

    public decimal Quantity { get; set; }

    public decimal Gross { get; set; }

    public decimal Discount { get; set; }

    public decimal Net { get; set; }

    public decimal Cost { get; set; }

    /// <summary>One when the line's stock movement has not been costed yet, else nought.</summary>
    public int Pending { get; set; }
}

/// <summary>Lines added up by item, category or customer.</summary>
public sealed class AnalysisGroup
{
    public long Key { get; set; }

    /// <summary>How many lines were added up.</summary>
    public int Count { get; set; }

    public decimal Quantity { get; set; }

    public decimal Gross { get; set; }

    public decimal Discount { get; set; }

    public decimal Net { get; set; }

    public decimal Cost { get; set; }

    public int Pending { get; set; }
}

/// <summary>A code and a name for an item, category or customer.</summary>
public sealed class LabelRef
{
    public long Key { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
