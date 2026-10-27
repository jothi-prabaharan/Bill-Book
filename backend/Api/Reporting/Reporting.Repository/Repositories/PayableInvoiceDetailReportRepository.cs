using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Payable Invoice Detail (TK-236): the Payable Invoice Summary line by line. Each vendor bill the
/// filters match, newest first and a page at a time, with its lines: the item, quantity, unit, price,
/// discount, taxable value, tax rate and amount and line total, in the bill's own currency, and its
/// total in base currency beside them. A line bought as a fixed asset is marked, with its asset category.
///
/// <b>The bills are the summary's.</b> The period, the vendor, the status rules, the status counts and
/// the order are TK-235's builders (<see cref="PayableInvoiceSummaryReportRepository.Figures"/>,
/// <c>PaidAmounts</c>, <c>Rows</c> and <c>WithStatus</c>), so the same bills appear with the same status and
/// the tabs say the same numbers; this report adds the lines. Debit notes are not listed.
///
/// <b>An item or category narrows the lines as well as the bills.</b> Only a bill with a line for that
/// item, or for an item in that category, is listed, and only those lines, so a bill's lines are no
/// longer the whole of it: its round-off is not given, and the totals are of the lines shown. With
/// neither filter a bill's lines add up to its total less the round-off, which is given, so lines,
/// round-off and total agree with the summary.
///
/// <b>The totals are worked out in the database</b> (<see cref="LineFigures"/>), over every line of every
/// bill the filters match, each line taken to base at its bill's rate and rounded to two places, so they
/// do not depend on the page in hand. The page's lines, the items, units, tax rates and asset categories
/// they name are read for the bills on the page only and put together in memory.
/// </summary>
public sealed class PayableInvoiceDetailReportRepository
{
    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public PayableInvoiceDetailReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>The refusals a request needs beyond both dates being given, with the field each belongs to. Pure.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(PayableInvoiceDetailReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from)
        {
            refusals.Add((nameof(PayableInvoiceDetailReportRequest.ToDate), "To date cannot be before the from date."));
        }

        if (request.Status is PayableInvoiceStatus status && !Enum.IsDefined(status))
        {
            refusals.Add((nameof(PayableInvoiceDetailReportRequest.Status), "Status must be one of the statuses offered."));
        }

        return refusals;
    }

    /// <summary>The refusals for a vendor, item or category that is not on this branch, with the field each belongs to. The query filter decides, so another branch's reads as none.</summary>
    public async Task<IReadOnlyList<(string Field, string Message)>> LookupRefusalsAsync(PayableInvoiceDetailReportRequest request, CancellationToken ct)
    {
        List<(string, string)> refusals = [];

        if (request.ContactId is long contactId && !await _db.Contacts.AnyAsync(c => c.ContactId == contactId, ct))
        {
            refusals.Add((nameof(PayableInvoiceDetailReportRequest.ContactId), "That vendor is not on this branch."));
        }

        if (request.ItemId is long itemId && !await _db.Items.AnyAsync(i => i.ItemId == itemId, ct))
        {
            refusals.Add((nameof(PayableInvoiceDetailReportRequest.ItemId), "That item is not on this branch."));
        }

        if (request.ItemCategoryId is long categoryId && !await _db.ItemCategories.AnyAsync(c => c.ItemCategoryId == categoryId, ct))
        {
            refusals.Add((nameof(PayableInvoiceDetailReportRequest.ItemCategoryId), "That item category is not on this branch."));
        }

        return refusals;
    }

    /// <summary>The vendors the filter offers, by name: the Aged Payables list.</summary>
    public Task<List<AgedPayablesVendorChoiceDto>> VendorsAsync(CancellationToken ct) =>
        AgedPayablesSummaryReportRepository.VendorChoices(_db.Contacts).ToListAsync(ct);

    /// <summary>The items the filter offers, by name. LINQ only.</summary>
    public Task<List<PayableInvoiceItemChoiceDto>> ItemsAsync(CancellationToken ct) =>
        ItemChoices(_db.Items).ToListAsync(ct);

    /// <summary>The item categories the filter offers, in their own order. LINQ only.</summary>
    public Task<List<PayableInvoiceCategoryChoiceDto>> CategoriesAsync(CancellationToken ct) =>
        CategoryChoices(_db.ItemCategories).ToListAsync(ct);

    /// <summary>The report for a request the controller has already validated.</summary>
    public async Task<PayableInvoiceDetailReportResultDto> RunAsync(PayableInvoiceDetailReportRequest request, CancellationToken ct)
    {
        DateOnly start = request.FromDate!.Value;
        DateOnly end = request.ToDate!.Value;
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

        bool filtered = LinesAreFiltered(request);
        IQueryable<BillDetailRead> lines = Lines(_db.BillDetails, _db.Items, request);

        IQueryable<PayableInvoiceFigure> figures = PayableInvoiceSummaryReportRepository.Figures(
            _db.Bills, _db.DebitNotes, SummaryRequestOf(request), start, end);

        if (filtered)
        {
            IQueryable<long> withLines = lines.Select(d => d.BillId);
            figures = figures.Where(f => withLines.Contains(f.DocumentId));
        }

        IQueryable<PayableInvoiceRow> rows = PayableInvoiceSummaryReportRepository.Rows(
            figures, PayableInvoiceSummaryReportRepository.PaidAmounts(_db.TransactionRatios, figures, end), end);

        List<PayableStatusCount> counts = await rows
            .GroupBy(r => r.Status)
            .Select(g => new PayableStatusCount { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        IQueryable<PayableInvoiceRow> matching = PayableInvoiceSummaryReportRepository.WithStatus(rows, request.Status);

        int matches = await matching.CountAsync(ct);

        PayableInvoiceDetailTotalsDto totals = await LineFigures(lines, matching)
            .GroupBy(x => 1)
            .Select(g => new PayableInvoiceDetailTotalsDto
            {
                Taxable = g.Sum(x => x.Taxable),
                Tax = g.Sum(x => x.Tax),
                Total = g.Sum(x => x.Total),
            })
            .FirstOrDefaultAsync(ct) ?? new PayableInvoiceDetailTotalsDto();

        List<PayableInvoiceRow> page = await matching
            .OrderByDescending(r => r.DocumentDate)
            .ThenByDescending(r => r.TypeCode)
            .ThenByDescending(r => r.DocumentId)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        List<long> billIds = [.. page.Select(r => r.DocumentId)];
        List<BillDetailRead> pageLines = billIds.Count == 0
            ? []
            : await lines
                .Where(d => billIds.Contains(d.BillId))
                .OrderBy(d => d.BillId)
                .ThenBy(d => d.LineNumber)
                .ToListAsync(ct);

        List<long> lineIds = [.. pageLines.Select(d => d.BillDetailId)];
        List<BillLineTaxRate> taxRates = lineIds.Count == 0
            ? []
            : await _db.BillDetailTaxes
                .Where(t => lineIds.Contains(t.BillDetailId))
                .Select(t => new BillLineTaxRate { BillDetailId = t.BillDetailId, Rate = t.Rate })
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

        List<long> categoryIds = [.. pageLines.Where(d => d.FixedAssetCategoryId != null).Select(d => d.FixedAssetCategoryId!.Value).Distinct()];
        List<AssetCategoryRef> assetCategories = categoryIds.Count == 0
            ? []
            : await _db.FixedAssetCategories
                .Where(c => categoryIds.Contains(c.FixedAssetCategoryId))
                .Select(c => new AssetCategoryRef { FixedAssetCategoryId = c.FixedAssetCategoryId, Name = c.CategoryName })
                .ToListAsync(ct);

        List<long> contactIds = [.. page.Select(r => r.ContactId).Distinct()];
        List<VendorRef> vendors = contactIds.Count == 0
            ? []
            : await _db.Contacts
                .Where(c => contactIds.Contains(c.ContactId))
                .Select(c => new VendorRef { ContactId = c.ContactId, Code = c.ContactCode, Name = c.DisplayName, IsMsme = c.IsMsme })
                .ToListAsync(ct);

        return Compose(request, page, matches, pageLines, taxRates, items, units, assetCategories, vendors, counts, totals, baseCurrency);
    }

    // --- builders: LINQ over the read models, so a test runs them over lists -------------

    /// <summary>Whether an item or a category narrows the lines. Pure.</summary>
    public static bool LinesAreFiltered(PayableInvoiceDetailReportRequest request) =>
        request.ItemId != null || request.ItemCategoryId != null;

    /// <summary>The request the summary's builders take, from this one: the same period, vendor and status, no debit notes, any currency. Pure.</summary>
    public static PayableInvoiceSummaryReportRequest SummaryRequestOf(PayableInvoiceDetailReportRequest request) => new()
    {
        FromDate = request.FromDate,
        ToDate = request.ToDate,
        ContactId = request.ContactId,
        Status = request.Status,
        IncludeDebitNotes = false,
    };

    /// <summary>
    /// The bill lines the report is about: every line, or only those for the item, or for an item in
    /// the category, asked for. LINQ only.
    /// </summary>
    public static IQueryable<BillDetailRead> Lines(
        IQueryable<BillDetailRead> details, IQueryable<ItemRead> items, PayableInvoiceDetailReportRequest request)
    {
        IQueryable<BillDetailRead> lines = details;

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
    /// Each line of the matching bills in base currency: the taxable value, the tax and the line total,
    /// taken to base at the bill's rate and rounded to two places. The totals are summed from this in the
    /// database. LINQ only.
    /// </summary>
    public static IQueryable<BillLineFigure> LineFigures(IQueryable<BillDetailRead> lines, IQueryable<PayableInvoiceRow> bills) =>
        from d in lines
        join r in bills on d.BillId equals r.DocumentId
        select new BillLineFigure
        {
            BillId = r.DocumentId,
            Taxable = Math.Round(d.TaxableAmount * r.ExchangeRate, 2),
            Tax = Math.Round(d.TaxAmount * r.ExchangeRate, 2),
            Total = Math.Round(d.LineTotal * r.ExchangeRate, 2),
        };

    /// <summary>The active items a filter offers, by name. LINQ only.</summary>
    public static IQueryable<PayableInvoiceItemChoiceDto> ItemChoices(IQueryable<ItemRead> items) =>
        items
            .Where(i => i.IsActive)
            .OrderBy(i => i.ItemName)
            .ThenBy(i => i.ItemId)
            .Select(i => new PayableInvoiceItemChoiceDto { ItemId = i.ItemId, ItemCode = i.ItemCode, ItemName = i.ItemName });

    /// <summary>The active item categories a filter offers, in their own order. LINQ only.</summary>
    public static IQueryable<PayableInvoiceCategoryChoiceDto> CategoryChoices(IQueryable<ItemCategoryRead> categories) =>
        categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.CategoryName)
            .ThenBy(c => c.ItemCategoryId)
            .Select(c => new PayableInvoiceCategoryChoiceDto { ItemCategoryId = c.ItemCategoryId, CategoryCode = c.CategoryCode, CategoryName = c.CategoryName });

    // --- compose: pure, so a test needs no database --------------------------------------

    /// <summary>
    /// The page's bills with their lines. A line takes the item's code and name, else its own
    /// description; its tax rate is its tax rows' rates added up; its unit is the unit's code; a capital
    /// line carries its asset category's name. A bill's subtotals are of the lines shown. Its round-off is
    /// its total in its own currency less all its lines, and is given only when no item or category
    /// narrows them. Every status has a count, a nought included. Pure.
    /// </summary>
    public static PayableInvoiceDetailReportResultDto Compose(
        PayableInvoiceDetailReportRequest request,
        IReadOnlyList<PayableInvoiceRow> page,
        int totalBills,
        IReadOnlyList<BillDetailRead> lines,
        IReadOnlyList<BillLineTaxRate> taxRates,
        IReadOnlyList<ItemRef> items,
        IReadOnlyList<UnitRef> units,
        IReadOnlyList<AssetCategoryRef> assetCategories,
        IReadOnlyList<VendorRef> vendors,
        IReadOnlyList<PayableStatusCount> counts,
        PayableInvoiceDetailTotalsDto totals,
        string baseCurrency)
    {
        bool filtered = LinesAreFiltered(request);
        Dictionary<long, VendorRef> names = vendors.GroupBy(c => c.ContactId).ToDictionary(g => g.Key, g => g.First());
        Dictionary<long, ItemRef> itemsById = items.GroupBy(i => i.ItemId).ToDictionary(g => g.Key, g => g.First());
        Dictionary<long, string> unitCodes = units.GroupBy(u => u.UomId).ToDictionary(g => g.Key, g => g.First().Code);
        Dictionary<long, string> assetNames = assetCategories.GroupBy(c => c.FixedAssetCategoryId).ToDictionary(g => g.Key, g => g.First().Name);
        Dictionary<long, decimal> rates = taxRates.GroupBy(t => t.BillDetailId).ToDictionary(g => g.Key, g => g.Sum(t => t.Rate));
        ILookup<long, BillDetailRead> byBill = lines.ToLookup(d => d.BillId);

        Dictionary<PayableInvoiceStatus, int> statusCounts = Enum.GetValues<PayableInvoiceStatus>().ToDictionary(s => s, _ => 0);
        foreach (PayableStatusCount count in counts)
        {
            statusCounts[count.Status] = count.Count;
        }

        List<PayableInvoiceDetailBillDto> bills = [];
        foreach (PayableInvoiceRow row in page)
        {
            VendorRef? vendor = names.GetValueOrDefault(row.ContactId);
            List<PayableInvoiceDetailLineDto> shown =
            [
                .. byBill[row.DocumentId]
                    .OrderBy(d => d.LineNumber)
                    .Select(d => LineOf(d, itemsById, unitCodes, assetNames, rates)),
            ];

            decimal linesTotal = shown.Sum(l => l.LineTotal);

            bills.Add(new PayableInvoiceDetailBillDto
            {
                TypeCode = row.TypeCode,
                DocumentId = row.DocumentId,
                DocumentNo = row.DocumentNo,
                VendorBillNo = row.VendorBillNo,
                DocumentDate = row.DocumentDate,
                DueDate = row.DueDate,
                ContactId = row.ContactId,
                ContactCode = vendor?.Code ?? string.Empty,
                ContactName = vendor?.Name ?? $"Vendor {row.ContactId}",
                CurrencyCode = row.CurrencyCode,
                Status = row.Status,
                ReverseCharge = row.ReverseCharge,
                TotalInCurrency = row.TotalSource,
                Total = row.Total,
                RoundOff = filtered ? null : row.TotalSource - linesTotal,
                Lines = shown,
                LinesTaxable = shown.Sum(l => l.Taxable),
                LinesTax = shown.Sum(l => l.TaxAmount),
                LinesTotal = linesTotal,
            });
        }

        return new PayableInvoiceDetailReportResultDto
        {
            FromDate = request.FromDate!.Value,
            ToDate = request.ToDate!.Value,
            BaseCurrencyCode = baseCurrency,
            LinesFiltered = filtered,
            Bills = bills,
            TotalBills = totalBills,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            StatusCounts = statusCounts,
            Totals = totals,
        };
    }

    private static PayableInvoiceDetailLineDto LineOf(
        BillDetailRead line,
        IReadOnlyDictionary<long, ItemRef> items,
        IReadOnlyDictionary<long, string> unitCodes,
        IReadOnlyDictionary<long, string> assetNames,
        IReadOnlyDictionary<long, decimal> rates)
    {
        ItemRef? item = line.ItemId is long id ? items.GetValueOrDefault(id) : null;
        string? description = string.IsNullOrWhiteSpace(line.Description) ? null : line.Description;
        bool capital = line.LineType == DocumentLineType.Capital;

        return new PayableInvoiceDetailLineDto
        {
            LineNumber = line.LineNumber,
            ItemId = line.ItemId,
            ItemCode = item?.Code,
            ItemName = item?.Name ?? description ?? string.Empty,
            Description = item is not null && description is not null && description != item.Name ? description : null,
            IsCapital = capital,
            AssetCategoryName = capital && line.FixedAssetCategoryId is long category ? assetNames.GetValueOrDefault(category) : null,
            Quantity = line.Quantity,
            UnitCode = line.UomId is long uom ? unitCodes.GetValueOrDefault(uom) : null,
            UnitPrice = line.UnitPrice,
            DiscountAmount = line.DiscountAmount,
            DiscountPercent = line.DiscountPercent,
            Taxable = line.TaxableAmount,
            TaxRate = rates.GetValueOrDefault(line.BillDetailId),
            TaxAmount = line.TaxAmount,
            LineTotal = line.LineTotal,
        };
    }
}

/// <summary>A line of the matching bills in base currency.</summary>
public sealed class BillLineFigure
{
    public long BillId { get; set; }

    public decimal Taxable { get; set; }

    public decimal Tax { get; set; }

    public decimal Total { get; set; }
}

/// <summary>One tax row's rate, for adding up the rates of a bill line.</summary>
public sealed class BillLineTaxRate
{
    public long BillDetailId { get; set; }

    public decimal Rate { get; set; }
}

/// <summary>A fixed-asset category's name.</summary>
public sealed class AssetCategoryRef
{
    public long FixedAssetCategoryId { get; set; }

    public string Name { get; set; } = string.Empty;
}
