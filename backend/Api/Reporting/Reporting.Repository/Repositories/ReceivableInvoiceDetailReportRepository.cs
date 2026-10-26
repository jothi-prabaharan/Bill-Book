using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Receivable Invoice Detail (TK-225): the Receivable Invoice Summary line by line. Each
/// invoice the filters match, newest first and a page at a time, with its lines: the item, quantity,
/// unit, price, discount, taxable value, tax rate and amount and line total, in the invoice's own
/// currency, and its total in base currency beside them.
///
/// <b>The invoices are the summary's.</b> The period, the customer, the status rules, the status
/// counts and the order are TK-224's builders (<see cref="ReceivableInvoiceSummaryReportRepository.Figures"/>,
/// <c>ReceivedAmounts</c>, <c>Rows</c> and <c>WithStatus</c>), so the same invoices appear with the same
/// status and the tabs say the same numbers; this report adds the lines. Credit notes are not listed.
///
/// <b>An item or category narrows the lines as well as the invoices.</b> Only an invoice with a line
/// for that item, or for an item in that category, is listed, and only those lines, so an invoice's
/// lines are no longer the whole of it: its round-off is not given, and the totals are of the lines
/// shown. With neither filter an invoice's lines add up to its total less the round-off, which is
/// given, so lines, round-off and total agree with the summary.
///
/// <b>The totals are worked out in the database</b> (<see cref="LineFigures"/>), over every line of
/// every invoice the filters match, each line taken to base at its invoice's rate and rounded to two
/// places, so they do not depend on the page in hand. The page's lines, the items, units and tax rates
/// they name are read for the invoices on the page only and put together in memory.
/// </summary>
public sealed class ReceivableInvoiceDetailReportRepository
{
    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public ReceivableInvoiceDetailReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>The refusals a request needs beyond both dates being given, with the field each belongs to. Pure.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(ReceivableInvoiceDetailReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from)
        {
            refusals.Add((nameof(ReceivableInvoiceDetailReportRequest.ToDate), "To date cannot be before the from date."));
        }

        if (request.Status is ReceivableInvoiceStatus status && !Enum.IsDefined(status))
        {
            refusals.Add((nameof(ReceivableInvoiceDetailReportRequest.Status), "Status must be one of the statuses offered."));
        }

        return refusals;
    }

    /// <summary>The refusals for an item or category that is not on this branch, with the field each belongs to. The query filter decides, so another branch's reads as none.</summary>
    public async Task<IReadOnlyList<(string Field, string Message)>> LookupRefusalsAsync(ReceivableInvoiceDetailReportRequest request, CancellationToken ct)
    {
        List<(string, string)> refusals = [];

        if (request.ContactId is long contactId && !await _db.Contacts.AnyAsync(c => c.ContactId == contactId, ct))
        {
            refusals.Add((nameof(ReceivableInvoiceDetailReportRequest.ContactId), "That customer is not on this branch."));
        }

        if (request.ItemId is long itemId && !await _db.Items.AnyAsync(i => i.ItemId == itemId, ct))
        {
            refusals.Add((nameof(ReceivableInvoiceDetailReportRequest.ItemId), "That item is not on this branch."));
        }

        if (request.ItemCategoryId is long categoryId && !await _db.ItemCategories.AnyAsync(c => c.ItemCategoryId == categoryId, ct))
        {
            refusals.Add((nameof(ReceivableInvoiceDetailReportRequest.ItemCategoryId), "That item category is not on this branch."));
        }

        return refusals;
    }

    /// <summary>The customers the filter offers, by name: the Aged Receivables list.</summary>
    public Task<List<AgedReceivablesCustomerChoiceDto>> CustomersAsync(CancellationToken ct) =>
        AgedReceivablesSummaryReportRepository.CustomerChoices(_db.Contacts).ToListAsync(ct);

    /// <summary>The items the filter offers, by name. LINQ only.</summary>
    public Task<List<ReceivableInvoiceItemChoiceDto>> ItemsAsync(CancellationToken ct) =>
        ItemChoices(_db.Items).ToListAsync(ct);

    /// <summary>The item categories the filter offers, in their own order. LINQ only.</summary>
    public Task<List<ReceivableInvoiceCategoryChoiceDto>> CategoriesAsync(CancellationToken ct) =>
        CategoryChoices(_db.ItemCategories).ToListAsync(ct);

    /// <summary>The report for a request the controller has already validated.</summary>
    public async Task<ReceivableInvoiceDetailReportResultDto> RunAsync(ReceivableInvoiceDetailReportRequest request, CancellationToken ct)
    {
        DateOnly start = request.FromDate!.Value;
        DateOnly end = request.ToDate!.Value;
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

        bool filtered = LinesAreFiltered(request);
        IQueryable<InvoiceDetailRead> lines = Lines(_db.InvoiceDetails, _db.Items, request);

        IQueryable<ReceivableInvoiceFigure> figures = ReceivableInvoiceSummaryReportRepository.Figures(
            _db.Invoices, _db.CreditNotes, SummaryRequestOf(request), start, end);

        if (filtered)
        {
            IQueryable<long> withLines = lines.Select(d => d.InvoiceId);
            figures = figures.Where(f => withLines.Contains(f.DocumentId));
        }

        IQueryable<ReceivableInvoiceRow> rows = ReceivableInvoiceSummaryReportRepository.Rows(
            figures, ReceivableInvoiceSummaryReportRepository.ReceivedAmounts(_db.TransactionRatios, figures, end), end);

        List<StatusCount> counts = await rows
            .GroupBy(r => r.Status)
            .Select(g => new StatusCount { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        IQueryable<ReceivableInvoiceRow> matching = ReceivableInvoiceSummaryReportRepository.WithStatus(rows, request.Status);

        int matches = await matching.CountAsync(ct);

        ReceivableInvoiceDetailTotalsDto totals = await LineFigures(lines, matching)
            .GroupBy(x => 1)
            .Select(g => new ReceivableInvoiceDetailTotalsDto
            {
                Taxable = g.Sum(x => x.Taxable),
                Tax = g.Sum(x => x.Tax),
                Total = g.Sum(x => x.Total),
            })
            .FirstOrDefaultAsync(ct) ?? new ReceivableInvoiceDetailTotalsDto();

        List<ReceivableInvoiceRow> page = await matching
            .OrderByDescending(r => r.DocumentDate)
            .ThenByDescending(r => r.TypeCode)
            .ThenByDescending(r => r.DocumentId)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        List<long> invoiceIds = [.. page.Select(r => r.DocumentId)];
        List<InvoiceDetailRead> pageLines = invoiceIds.Count == 0
            ? []
            : await lines
                .Where(d => invoiceIds.Contains(d.InvoiceId))
                .OrderBy(d => d.InvoiceId)
                .ThenBy(d => d.LineNumber)
                .ToListAsync(ct);

        List<long> lineIds = [.. pageLines.Select(d => d.InvoiceDetailId)];
        List<LineTaxRate> taxRates = lineIds.Count == 0
            ? []
            : await _db.InvoiceDetailTaxes
                .Where(t => lineIds.Contains(t.InvoiceDetailId))
                .Select(t => new LineTaxRate { InvoiceDetailId = t.InvoiceDetailId, Rate = t.Rate })
                .ToListAsync(ct);

        List<long> itemIds = [.. pageLines.Where(d => d.ItemId != null).Select(d => d.ItemId!.Value).Distinct()];
        List<ItemRef> items = itemIds.Count == 0
            ? []
            : await _db.Items
                .Where(i => itemIds.Contains(i.ItemId))
                .Select(i => new ItemRef { ItemId = i.ItemId, Code = i.ItemCode, Name = i.ItemName })
                .ToListAsync(ct);

        List<long> unitIds = [.. pageLines.Where(d => d.UomId != null).Select(d => d.UomId!.Value).Distinct()];
        List<UnitRef> units = unitIds.Count == 0
            ? []
            : await _db.UnitsOfMeasure
                .Where(u => unitIds.Contains(u.UomId))
                .Select(u => new UnitRef { UomId = u.UomId, Code = u.UomCode })
                .ToListAsync(ct);

        List<long> contactIds = [.. page.Select(r => r.ContactId).Distinct()];
        List<CustomerRef> customers = contactIds.Count == 0
            ? []
            : await _db.Contacts
                .Where(c => contactIds.Contains(c.ContactId))
                .Select(c => new CustomerRef { ContactId = c.ContactId, Code = c.ContactCode, Name = c.DisplayName })
                .ToListAsync(ct);

        return Compose(request, page, matches, pageLines, taxRates, items, units, customers, counts, totals, baseCurrency);
    }

    // --- builders: LINQ over the read models, so a test runs them over lists -------------

    /// <summary>Whether an item or a category narrows the lines. Pure.</summary>
    public static bool LinesAreFiltered(ReceivableInvoiceDetailReportRequest request) =>
        request.ItemId != null || request.ItemCategoryId != null;

    /// <summary>The request the summary's builders take, from this one: the same period, customer and status, no credit notes. Pure.</summary>
    public static ReceivableInvoiceSummaryReportRequest SummaryRequestOf(ReceivableInvoiceDetailReportRequest request) => new()
    {
        FromDate = request.FromDate,
        ToDate = request.ToDate,
        ContactId = request.ContactId,
        Status = request.Status,
        IncludeCreditNotes = false,
    };

    /// <summary>
    /// The invoice lines the report is about: every line, or only those for the item, or for an item
    /// in the category, asked for. LINQ only.
    /// </summary>
    public static IQueryable<InvoiceDetailRead> Lines(
        IQueryable<InvoiceDetailRead> details, IQueryable<ItemRead> items, ReceivableInvoiceDetailReportRequest request)
    {
        IQueryable<InvoiceDetailRead> lines = details;

        if (request.ItemId is long itemId)
        {
            lines = lines.Where(d => d.ItemId == itemId);
        }

        if (request.ItemCategoryId is long categoryId)
        {
            lines = from d in lines
                    join it in items on d.ItemId equals (long?)it.ItemId
                    where it.ItemCategoryId == categoryId
                    select d;
        }

        return lines;
    }

    /// <summary>
    /// Each line of the matching invoices in base currency: the taxable value, the tax and the line
    /// total, taken to base at the invoice's rate and rounded to two places. The totals are summed
    /// from this in the database. LINQ only.
    /// </summary>
    public static IQueryable<LineFigure> LineFigures(IQueryable<InvoiceDetailRead> lines, IQueryable<ReceivableInvoiceRow> invoices) =>
        from d in lines
        join r in invoices on d.InvoiceId equals r.DocumentId
        select new LineFigure
        {
            InvoiceId = r.DocumentId,
            Taxable = Math.Round(d.TaxableAmount * r.ExchangeRate, 2),
            Tax = Math.Round(d.TaxAmount * r.ExchangeRate, 2),
            Total = Math.Round(d.LineTotal * r.ExchangeRate, 2),
        };

    /// <summary>The active items a filter offers, by name. LINQ only.</summary>
    public static IQueryable<ReceivableInvoiceItemChoiceDto> ItemChoices(IQueryable<ItemRead> items) =>
        items
            .Where(i => i.IsActive)
            .OrderBy(i => i.ItemName)
            .ThenBy(i => i.ItemId)
            .Select(i => new ReceivableInvoiceItemChoiceDto { ItemId = i.ItemId, ItemCode = i.ItemCode, ItemName = i.ItemName });

    /// <summary>The active item categories a filter offers, in their own order. LINQ only.</summary>
    public static IQueryable<ReceivableInvoiceCategoryChoiceDto> CategoryChoices(IQueryable<ItemCategoryRead> categories) =>
        categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.CategoryName)
            .ThenBy(c => c.ItemCategoryId)
            .Select(c => new ReceivableInvoiceCategoryChoiceDto { ItemCategoryId = c.ItemCategoryId, CategoryCode = c.CategoryCode, CategoryName = c.CategoryName });

    // --- compose: pure, so a test needs no database --------------------------------------

    /// <summary>
    /// The page's invoices with their lines. A line takes the item's code and name, else its own
    /// description; its tax rate is its tax rows' rates added up; its unit is the unit's code. An
    /// invoice's subtotals are of the lines shown. Its round-off is its total in its own currency less
    /// all its lines, and is given only when no item or category narrows them. Every status has a
    /// count, a nought included. Pure.
    /// </summary>
    public static ReceivableInvoiceDetailReportResultDto Compose(
        ReceivableInvoiceDetailReportRequest request,
        IReadOnlyList<ReceivableInvoiceRow> page,
        int totalInvoices,
        IReadOnlyList<InvoiceDetailRead> lines,
        IReadOnlyList<LineTaxRate> taxRates,
        IReadOnlyList<ItemRef> items,
        IReadOnlyList<UnitRef> units,
        IReadOnlyList<CustomerRef> customers,
        IReadOnlyList<StatusCount> counts,
        ReceivableInvoiceDetailTotalsDto totals,
        string baseCurrency)
    {
        bool filtered = LinesAreFiltered(request);
        Dictionary<long, CustomerRef> names = customers.GroupBy(c => c.ContactId).ToDictionary(g => g.Key, g => g.First());
        Dictionary<long, ItemRef> itemsById = items.GroupBy(i => i.ItemId).ToDictionary(g => g.Key, g => g.First());
        Dictionary<long, string> unitCodes = units.GroupBy(u => u.UomId).ToDictionary(g => g.Key, g => g.First().Code);
        Dictionary<long, decimal> rates = taxRates.GroupBy(t => t.InvoiceDetailId).ToDictionary(g => g.Key, g => g.Sum(t => t.Rate));
        ILookup<long, InvoiceDetailRead> byInvoice = lines.ToLookup(d => d.InvoiceId);

        Dictionary<ReceivableInvoiceStatus, int> statusCounts = Enum.GetValues<ReceivableInvoiceStatus>().ToDictionary(s => s, _ => 0);
        foreach (StatusCount count in counts)
        {
            statusCounts[count.Status] = count.Count;
        }

        List<ReceivableInvoiceDetailInvoiceDto> invoices = [];
        foreach (ReceivableInvoiceRow row in page)
        {
            CustomerRef? customer = names.GetValueOrDefault(row.ContactId);
            List<ReceivableInvoiceDetailLineDto> shown =
            [
                .. byInvoice[row.DocumentId]
                    .OrderBy(d => d.LineNumber)
                    .Select(d => LineOf(d, itemsById, unitCodes, rates)),
            ];

            decimal linesTotal = shown.Sum(l => l.LineTotal);

            invoices.Add(new ReceivableInvoiceDetailInvoiceDto
            {
                TypeCode = row.TypeCode,
                DocumentId = row.DocumentId,
                DocumentNo = row.DocumentNo,
                DocumentDate = row.DocumentDate,
                DueDate = row.DueDate,
                ContactId = row.ContactId,
                ContactCode = customer?.Code ?? string.Empty,
                ContactName = customer?.Name ?? $"Customer {row.ContactId}",
                CurrencyCode = row.CurrencyCode,
                Status = row.Status,
                TotalInCurrency = row.TotalSource,
                Total = row.Total,
                RoundOff = filtered ? null : row.TotalSource - linesTotal,
                Lines = shown,
                LinesTaxable = shown.Sum(l => l.Taxable),
                LinesTax = shown.Sum(l => l.TaxAmount),
                LinesTotal = linesTotal,
            });
        }

        return new ReceivableInvoiceDetailReportResultDto
        {
            FromDate = request.FromDate!.Value,
            ToDate = request.ToDate!.Value,
            BaseCurrencyCode = baseCurrency,
            LinesFiltered = filtered,
            Invoices = invoices,
            TotalInvoices = totalInvoices,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            StatusCounts = statusCounts,
            Totals = totals,
        };
    }

    private static ReceivableInvoiceDetailLineDto LineOf(
        InvoiceDetailRead line,
        IReadOnlyDictionary<long, ItemRef> items,
        IReadOnlyDictionary<long, string> unitCodes,
        IReadOnlyDictionary<long, decimal> rates)
    {
        ItemRef? item = line.ItemId is long id ? items.GetValueOrDefault(id) : null;
        string? description = string.IsNullOrWhiteSpace(line.Description) ? null : line.Description;

        return new ReceivableInvoiceDetailLineDto
        {
            LineNumber = line.LineNumber,
            ItemId = line.ItemId,
            ItemCode = item?.Code,
            ItemName = item?.Name ?? description ?? string.Empty,
            Description = item is not null && description is not null && description != item.Name ? description : null,
            Quantity = line.Quantity,
            UnitCode = line.UomId is long uom ? unitCodes.GetValueOrDefault(uom) : null,
            UnitPrice = line.UnitPrice,
            DiscountAmount = line.DiscountAmount,
            DiscountPercent = line.DiscountPercent,
            Taxable = line.TaxableAmount,
            TaxRate = rates.GetValueOrDefault(line.InvoiceDetailId),
            TaxAmount = line.TaxAmount,
            LineTotal = line.LineTotal,
        };
    }
}

/// <summary>A line of the matching invoices in base currency.</summary>
public sealed class LineFigure
{
    public long InvoiceId { get; set; }

    public decimal Taxable { get; set; }

    public decimal Tax { get; set; }

    public decimal Total { get; set; }
}

/// <summary>One tax row's rate, for adding up the rates of a line.</summary>
public sealed class LineTaxRate
{
    public long InvoiceDetailId { get; set; }

    public decimal Rate { get; set; }
}

/// <summary>An item's code and name.</summary>
public sealed class ItemRef
{
    public long ItemId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}

/// <summary>A unit of measure's code.</summary>
public sealed class UnitRef
{
    public long UomId { get; set; }

    public string Code { get; set; } = string.Empty;
}
