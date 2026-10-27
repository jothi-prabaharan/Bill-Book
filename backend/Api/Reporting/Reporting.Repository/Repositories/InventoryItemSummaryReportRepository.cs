using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Inventory Item Summary (TK-243): each stocked item over a period, by name and a page of items at a
/// time, with its opening quantity and value, what was purchased, sold and adjusted, its closing quantity
/// and value, and the profit on what was sold. <b>Stock is one pool per branch</b>: warehouses are a
/// location only, so every figure is the item's across them all.
///
/// <b>Every quantity and value is added up from the stock movements</b>, which is what makes the columns
/// add up: <i>opening</i> is every movement before the From date, and each movement in the period is in
/// exactly one other column. <i>Purchased</i> is goods received less goods sent back to the vendor;
/// <i>sold</i> is goods issued less goods customers sent back; <i>adjusted</i> is everything else, signed:
/// counts, opening entries and transfers (which net to nothing). So opening plus purchased less sold plus
/// adjusted is the closing quantity, and likewise the closing value, on every item.
///
/// <b>A movement is valued once it has been costed</b>: costing is asynchronous, so a movement still
/// pending, in progress or failed is not taken as costing nothing. The item says so (<c>CostPending</c>)
/// and, when what was sold is among them, has no profit, since the profit would be overstated. Values are
/// the movement's total cost; with every movement costed the closing values add up to the Inventory
/// control account's balance at the To date.
///
/// <b>Sales are the invoices' net sales before tax</b> (the Sales Analysis' own figure: posted invoices and
/// POS sales less posted credit notes, each line taken to base at its document's rate), so sales less cost
/// of sales is the profit, as the ledger's Sales Revenue less Cost of Goods Sold would show it.
///
/// <b>The count, the totals and the page are each one query over every item the filters match</b>: the
/// movements, and the sales, are added up by item in the database, and only the page's items are put
/// together in memory. An item is listed when it had stock at the From date or moved in the period, unless
/// asked to list them all.
/// </summary>
public sealed class InventoryItemSummaryReportRepository
{
    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public InventoryItemSummaryReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>The refusals a request needs beyond both dates being given, with the field each belongs to. Pure.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(InventoryItemSummaryReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from)
        {
            refusals.Add((nameof(InventoryItemSummaryReportRequest.ToDate), "To date cannot be before the from date."));
        }

        return refusals;
    }

    /// <summary>The refusals for an item or category that is not on this branch, with the field each belongs to. The query filter decides, so another branch's reads as none.</summary>
    public async Task<IReadOnlyList<(string Field, string Message)>> LookupRefusalsAsync(InventoryItemSummaryReportRequest request, CancellationToken ct)
    {
        List<(string, string)> refusals = [];

        if (request.ItemId is long itemId && !await _db.Items.AnyAsync(i => i.ItemId == itemId, ct))
        {
            refusals.Add((nameof(InventoryItemSummaryReportRequest.ItemId), "That item is not on this branch."));
        }

        if (request.ItemCategoryId is long categoryId && !await _db.ItemCategories.AnyAsync(c => c.ItemCategoryId == categoryId, ct))
        {
            refusals.Add((nameof(InventoryItemSummaryReportRequest.ItemCategoryId), "That item category is not on this branch."));
        }

        return refusals;
    }

    /// <summary>The stocked items the filter offers, by name. LINQ only.</summary>
    public Task<List<PayableInvoiceItemChoiceDto>> ItemsAsync(CancellationToken ct) =>
        StockItemChoices(_db.Items).ToListAsync(ct);

    /// <summary>The item categories the filter offers, in their own order.</summary>
    public Task<List<PayableInvoiceCategoryChoiceDto>> CategoriesAsync(CancellationToken ct) =>
        PayableInvoiceDetailReportRepository.CategoryChoices(_db.ItemCategories).ToListAsync(ct);

    /// <summary>The report for a request the controller has already validated.</summary>
    public async Task<InventoryItemSummaryReportResultDto> RunAsync(InventoryItemSummaryReportRequest request, CancellationToken ct)
    {
        DateOnly start = request.FromDate!.Value;
        DateOnly end = request.ToDate!.Value;
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

        IQueryable<ItemRead> items = Items(_db.Items, request);
        IQueryable<ItemFigure> figures = ItemFigures(MovementFigures(items, _db.StockMovements, start, end));
        IQueryable<ItemSale> sales = ItemSales(items, _db.Invoices, _db.InvoiceDetails, _db.CreditNotes, _db.CreditNoteDetails, start, end);
        IQueryable<InventoryItemSummaryEntry> rows = Rows(items, _db.ItemCategories, _db.UnitsOfMeasure, figures, sales, request.ShowItemsWithNoMovement);

        InventoryItemSummaryEntry totals = await rows
            .GroupBy(r => 1)
            .Select(g => new InventoryItemSummaryEntry
            {
                OpeningValue = g.Sum(r => r.OpeningValue),
                PurchasedValue = g.Sum(r => r.PurchasedValue),
                Sales = g.Sum(r => r.Sales),
                SoldCost = g.Sum(r => r.SoldCost),
                AdjustedValue = g.Sum(r => r.AdjustedValue),
                ClosingValue = g.Sum(r => r.ClosingValue),
                Uncosted = g.Sum(r => r.Uncosted),
                SoldUncosted = g.Sum(r => r.SoldUncosted),
            })
            .FirstOrDefaultAsync(ct) ?? new InventoryItemSummaryEntry();

        int matches = await rows.CountAsync(ct);

        List<InventoryItemSummaryEntry> page = await rows
            .OrderBy(r => r.ItemName)
            .ThenBy(r => r.ItemId)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        return Compose(request, page, matches, totals, baseCurrency);
    }

    // --- builders: LINQ over the read models, so a test runs them over lists -------------

    /// <summary>The stocked items the report is about, narrowed by category and item. LINQ only.</summary>
    public static IQueryable<ItemRead> Items(IQueryable<ItemRead> items, InventoryItemSummaryReportRequest request)
    {
        IQueryable<ItemRead> stocked = items.Where(i => i.TrackInventory);

        if (request.ItemCategoryId is long category)
        {
            stocked = stocked.Where(i => i.ItemCategoryId == category);
        }

        return request.ItemId is long item ? stocked.Where(i => i.ItemId == item) : stocked;
    }

    /// <summary>The stocked items a filter offers, by name. LINQ only.</summary>
    public static IQueryable<PayableInvoiceItemChoiceDto> StockItemChoices(IQueryable<ItemRead> items) =>
        items
            .Where(i => i.IsActive && i.TrackInventory)
            .OrderBy(i => i.ItemName)
            .ThenBy(i => i.ItemId)
            .Select(i => new PayableInvoiceItemChoiceDto { ItemId = i.ItemId, ItemCode = i.ItemCode, ItemName = i.ItemName });

    /// <summary>
    /// One row for each stock movement of those items up to the To date, with its quantity and value in
    /// the column it belongs to; and one row of nothing for each item, so that every item has a row to
    /// join even when it never moved. A quantity is positive in and negative out; a value is the
    /// movement's total cost once it is costed, else nothing, and the movement is counted as uncosted
    /// unless it was skipped. Each movement before the From date is opening; each in the period is
    /// purchased (a receipt or a purchase return), sold (an issue or a sales return, so a return is a
    /// negative of what was sold) or adjusted (any other type). LINQ only.
    /// </summary>
    public static IQueryable<ItemMovementFigure> MovementFigures(
        IQueryable<ItemRead> items, IQueryable<StockMovementRead> movements, DateOnly start, DateOnly end)
    {
        IQueryable<ItemMovementFigure> moved =
            from m in movements
            join i in items on m.ItemId equals i.ItemId
            where m.MovementDate <= end
            let qty = m.Direction == StockDirection.In ? m.Quantity : -m.Quantity
            let value = m.CostingStatus == CostingStatus.Costed ? (m.Direction == StockDirection.In ? (m.TotalCost ?? 0m) : -(m.TotalCost ?? 0m)) : 0m
            let inPeriod = m.MovementDate >= start
            let purchase = m.MovementType == StockMovementType.Receipt || m.MovementType == StockMovementType.PurchaseReturn
            let sale = m.MovementType == StockMovementType.Issue || m.MovementType == StockMovementType.SalesReturn
            let uncosted = m.CostingStatus != CostingStatus.Costed && m.CostingStatus != CostingStatus.Skipped
            select new ItemMovementFigure
            {
                ItemId = m.ItemId,
                OpeningQuantity = inPeriod ? 0m : qty,
                OpeningValue = inPeriod ? 0m : value,
                PurchasedQuantity = inPeriod && purchase ? qty : 0m,
                PurchasedValue = inPeriod && purchase ? value : 0m,
                SoldQuantity = inPeriod && sale ? -qty : 0m,
                SoldCost = inPeriod && sale ? -value : 0m,
                AdjustedQuantity = inPeriod && !purchase && !sale ? qty : 0m,
                AdjustedValue = inPeriod && !purchase && !sale ? value : 0m,
                Moves = inPeriod ? 1 : 0,
                Uncosted = uncosted ? 1 : 0,
                SoldUncosted = inPeriod && sale && uncosted ? 1 : 0,
            };

        // Every property is assigned on both sides of the union, as EF Core requires.
        IQueryable<ItemMovementFigure> none = items.Select(i => new ItemMovementFigure
        {
            ItemId = i.ItemId,
            OpeningQuantity = 0m,
            OpeningValue = 0m,
            PurchasedQuantity = 0m,
            PurchasedValue = 0m,
            SoldQuantity = 0m,
            SoldCost = 0m,
            AdjustedQuantity = 0m,
            AdjustedValue = 0m,
            Moves = 0,
            Uncosted = 0,
            SoldUncosted = 0,
        });

        return moved.Concat(none);
    }

    /// <summary>The movements added up by item: one row for every item. LINQ only.</summary>
    public static IQueryable<ItemFigure> ItemFigures(IQueryable<ItemMovementFigure> movements) =>
        movements
            .GroupBy(f => f.ItemId)
            .Select(g => new ItemFigure
            {
                ItemId = g.Key,
                OpeningQuantity = g.Sum(f => f.OpeningQuantity),
                OpeningValue = g.Sum(f => f.OpeningValue),
                PurchasedQuantity = g.Sum(f => f.PurchasedQuantity),
                PurchasedValue = g.Sum(f => f.PurchasedValue),
                SoldQuantity = g.Sum(f => f.SoldQuantity),
                SoldCost = g.Sum(f => f.SoldCost),
                AdjustedQuantity = g.Sum(f => f.AdjustedQuantity),
                AdjustedValue = g.Sum(f => f.AdjustedValue),
                Moves = g.Sum(f => f.Moves),
                Uncosted = g.Sum(f => f.Uncosted),
                SoldUncosted = g.Sum(f => f.SoldUncosted),
            });

    /// <summary>
    /// Each item's net sales in the period: the posted invoices' and POS sales' lines less the posted
    /// credit notes' lines, each line's taxable value taken to base at its document's rate and rounded to
    /// two places, as the Sales Analysis has it; one row of nothing for each item so every item has one.
    /// LINQ only.
    /// </summary>
    public static IQueryable<ItemSale> ItemSales(
        IQueryable<ItemRead> items,
        IQueryable<InvoiceRead> invoices,
        IQueryable<InvoiceDetailRead> invoiceDetails,
        IQueryable<CreditNoteRead> creditNotes,
        IQueryable<CreditNoteDetailRead> creditDetails,
        DateOnly start,
        DateOnly end)
    {
        IQueryable<ItemSaleLine> sold =
            from d in invoiceDetails
            join i in invoices on d.InvoiceId equals i.InvoiceId
            join it in items on d.ItemId equals (long?)it.ItemId
            where i.Status == DocumentStatus.Posted
                && (i.TransactionTypeCode == "INV" || i.TransactionTypeCode == "POS")
                && i.DocumentDate >= start && i.DocumentDate <= end
            select new ItemSaleLine { ItemId = it.ItemId, Net = Math.Round(d.TaxableAmount * i.ExchangeRate, 2) };

        IQueryable<ItemSaleLine> credited =
            from d in creditDetails
            join c in creditNotes on d.CreditNoteId equals c.CreditNoteId
            join it in items on d.ItemId equals (long?)it.ItemId
            where c.Status == DocumentStatus.Posted && c.DocumentDate >= start && c.DocumentDate <= end
            select new ItemSaleLine { ItemId = it.ItemId, Net = -Math.Round(d.TaxableAmount * c.ExchangeRate, 2) };

        IQueryable<ItemSaleLine> none = items.Select(i => new ItemSaleLine { ItemId = i.ItemId, Net = 0m });

        return sold.Concat(credited).Concat(none)
            .GroupBy(l => l.ItemId)
            .Select(g => new ItemSale { ItemId = g.Key, Net = g.Sum(l => l.Net) });
    }

    /// <summary>
    /// One row for each item listed, with its category, unit, movements and sales, and its closing quantity
    /// and value. An item is listed when it had stock at the From date or moved in the period, or when asked
    /// to list them all. LINQ only.
    /// </summary>
    public static IQueryable<InventoryItemSummaryEntry> Rows(
        IQueryable<ItemRead> items,
        IQueryable<ItemCategoryRead> categories,
        IQueryable<UnitOfMeasureRead> units,
        IQueryable<ItemFigure> figures,
        IQueryable<ItemSale> sales,
        bool showAll) =>
        from i in items
        join f in figures on i.ItemId equals f.ItemId
        join s in sales on i.ItemId equals s.ItemId
        join cv in categories on i.ItemCategoryId equals (long?)cv.ItemCategoryId into categorised
        from c in categorised.DefaultIfEmpty()
        join uv in units on i.InventoryUomId equals uv.UomId into united
        from u in united.DefaultIfEmpty()
        where showAll || f.Moves > 0 || f.OpeningQuantity != 0m
        select new InventoryItemSummaryEntry
        {
            ItemId = i.ItemId,
            ItemCode = i.ItemCode,
            ItemName = i.ItemName,
            ItemType = i.ItemType,
            CategoryName = c == null ? null : c.CategoryName,
            CostingType = i.CostingType,
            UnitCode = u == null ? null : u.UomCode,
            UnitName = u == null ? null : u.UomName,
            OpeningQuantity = f.OpeningQuantity,
            OpeningValue = f.OpeningValue,
            PurchasedQuantity = f.PurchasedQuantity,
            PurchasedValue = f.PurchasedValue,
            SoldQuantity = f.SoldQuantity,
            SoldCost = f.SoldCost,
            Sales = s.Net,
            AdjustedQuantity = f.AdjustedQuantity,
            AdjustedValue = f.AdjustedValue,
            ClosingQuantity = f.OpeningQuantity + f.PurchasedQuantity - f.SoldQuantity + f.AdjustedQuantity,
            ClosingValue = f.OpeningValue + f.PurchasedValue - f.SoldCost + f.AdjustedValue,
            Uncosted = f.Uncosted,
            SoldUncosted = f.SoldUncosted,
        };

    // --- compose: pure, so a test needs no database --------------------------------------

    /// <summary>
    /// The page's items with their figures, and the totals. An item's profit is its sales less its cost of
    /// sales, and is blank while a movement of what was sold has not been costed; the total's likewise. An
    /// item or the total is marked cost pending while any movement is uncosted. Pure.
    /// </summary>
    public static InventoryItemSummaryReportResultDto Compose(
        InventoryItemSummaryReportRequest request,
        IReadOnlyList<InventoryItemSummaryEntry> page,
        int totalItems,
        InventoryItemSummaryEntry totals,
        string baseCurrency) =>
        new()
        {
            FromDate = request.FromDate!.Value,
            ToDate = request.ToDate!.Value,
            BaseCurrencyCode = baseCurrency,
            Items =
            [
                .. page.Select(r => new InventoryItemSummaryItemDto
                {
                    ItemId = r.ItemId,
                    ItemCode = r.ItemCode,
                    ItemName = r.ItemName,
                    CategoryName = r.CategoryName,
                    CostingType = r.CostingType,
                    UnitCode = r.UnitCode,
                    OpeningQuantity = r.OpeningQuantity,
                    OpeningValue = r.OpeningValue,
                    PurchasedQuantity = r.PurchasedQuantity,
                    PurchasedValue = r.PurchasedValue,
                    SoldQuantity = r.SoldQuantity,
                    SalesValue = r.Sales,
                    Cogs = r.SoldCost,
                    Profit = r.SoldUncosted == 0 ? r.Sales - r.SoldCost : null,
                    AdjustedQuantity = r.AdjustedQuantity,
                    AdjustedValue = r.AdjustedValue,
                    ClosingQuantity = r.ClosingQuantity,
                    ClosingValue = r.ClosingValue,
                    CostPending = r.Uncosted > 0,
                }),
            ],
            TotalItems = totalItems,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            Totals = new InventoryItemSummaryTotalsDto
            {
                OpeningValue = totals.OpeningValue,
                PurchasedValue = totals.PurchasedValue,
                SalesValue = totals.Sales,
                Cogs = totals.SoldCost,
                Profit = totals.SoldUncosted == 0 ? totals.Sales - totals.SoldCost : null,
                AdjustedValue = totals.AdjustedValue,
                ClosingValue = totals.ClosingValue,
                CostPending = totals.Uncosted > 0,
            },
        };
}

/// <summary>One stock movement, or one item that has none, in the columns it adds to.</summary>
public sealed class ItemMovementFigure
{
    public long ItemId { get; set; }

    public decimal OpeningQuantity { get; set; }

    public decimal OpeningValue { get; set; }

    public decimal PurchasedQuantity { get; set; }

    public decimal PurchasedValue { get; set; }

    public decimal SoldQuantity { get; set; }

    public decimal SoldCost { get; set; }

    public decimal AdjustedQuantity { get; set; }

    public decimal AdjustedValue { get; set; }

    /// <summary>One for a movement in the period.</summary>
    public int Moves { get; set; }

    /// <summary>One for a movement up to the To date that has not been costed.</summary>
    public int Uncosted { get; set; }

    /// <summary>One for a movement in the period of something sold that has not been costed.</summary>
    public int SoldUncosted { get; set; }
}

/// <summary>An item's movements added up.</summary>
public sealed class ItemFigure
{
    public long ItemId { get; set; }

    public decimal OpeningQuantity { get; set; }

    public decimal OpeningValue { get; set; }

    public decimal PurchasedQuantity { get; set; }

    public decimal PurchasedValue { get; set; }

    public decimal SoldQuantity { get; set; }

    public decimal SoldCost { get; set; }

    public decimal AdjustedQuantity { get; set; }

    public decimal AdjustedValue { get; set; }

    public int Moves { get; set; }

    public int Uncosted { get; set; }

    public int SoldUncosted { get; set; }
}

/// <summary>One sale or credit line of an item in base currency, or an item with none.</summary>
public sealed class ItemSaleLine
{
    public long ItemId { get; set; }

    public decimal Net { get; set; }
}

/// <summary>An item's net sales in the period.</summary>
public sealed class ItemSale
{
    public long ItemId { get; set; }

    public decimal Net { get; set; }
}

/// <summary>An item with its movements, sales and closing stock.</summary>
public sealed class InventoryItemSummaryEntry
{
    public long ItemId { get; set; }

    public string ItemCode { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;

    public ItemType ItemType { get; set; }

    public string? CategoryName { get; set; }

    public CostingType CostingType { get; set; }

    public string? UnitCode { get; set; }

    public string? UnitName { get; set; }

    public decimal OpeningQuantity { get; set; }

    public decimal OpeningValue { get; set; }

    public decimal PurchasedQuantity { get; set; }

    public decimal PurchasedValue { get; set; }

    public decimal SoldQuantity { get; set; }

    public decimal SoldCost { get; set; }

    public decimal Sales { get; set; }

    public decimal AdjustedQuantity { get; set; }

    public decimal AdjustedValue { get; set; }

    public decimal ClosingQuantity { get; set; }

    public decimal ClosingValue { get; set; }

    public int Uncosted { get; set; }

    public int SoldUncosted { get; set; }
}
