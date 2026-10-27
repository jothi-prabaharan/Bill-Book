using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Inventory Item Detail (TK-244): stock card for each stocked item over a period.
/// Displays opening balance, each chronological stock movement in the period with document number,
/// source, contact, quantity in/out, unit cost, value, unit sale price, margin, running quantity
/// and value, and closing balance.
/// </summary>
public sealed class InventoryItemDetailReportRepository
{
    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public InventoryItemDetailReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>The refusals a request needs beyond both dates being given, with the field each belongs to. Pure.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(InventoryItemDetailReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from)
        {
            refusals.Add((nameof(InventoryItemDetailReportRequest.ToDate), "To date cannot be before the from date."));
        }

        return refusals;
    }

    /// <summary>The refusals for an item, category, or warehouse that is not on this branch. The query filter decides, so another branch's reads as none.</summary>
    public async Task<IReadOnlyList<(string Field, string Message)>> LookupRefusalsAsync(InventoryItemDetailReportRequest request, CancellationToken ct)
    {
        List<(string, string)> refusals = [];

        if (request.ItemId is long itemId && !await _db.Items.AnyAsync(i => i.ItemId == itemId, ct))
        {
            refusals.Add((nameof(InventoryItemDetailReportRequest.ItemId), "That item is not on this branch."));
        }

        if (request.ItemCategoryId is long categoryId && !await _db.ItemCategories.AnyAsync(c => c.ItemCategoryId == categoryId, ct))
        {
            refusals.Add((nameof(InventoryItemDetailReportRequest.ItemCategoryId), "That item category is not on this branch."));
        }

        if (request.WarehouseId is long warehouseId && !await _db.Warehouses.AnyAsync(w => w.WarehouseId == warehouseId, ct))
        {
            refusals.Add((nameof(InventoryItemDetailReportRequest.WarehouseId), "That warehouse is not on this branch."));
        }

        return refusals;
    }

    /// <summary>The stocked items the filter offers, by name. LINQ only.</summary>
    public Task<List<PayableInvoiceItemChoiceDto>> ItemsAsync(CancellationToken ct) =>
        InventoryItemSummaryReportRepository.StockItemChoices(_db.Items).ToListAsync(ct);

    /// <summary>The item categories the filter offers, in their own order.</summary>
    public Task<List<PayableInvoiceCategoryChoiceDto>> CategoriesAsync(CancellationToken ct) =>
        PayableInvoiceDetailReportRepository.CategoryChoices(_db.ItemCategories).ToListAsync(ct);

    /// <summary>The warehouses the filter offers.</summary>
    public Task<List<SalesAnalysisWarehouseChoiceDto>> WarehousesAsync(CancellationToken ct) =>
        SalesAnalysisReportRepository.WarehouseChoices(_db.Warehouses).ToListAsync(ct);

    /// <summary>The report for a request the controller has already validated.</summary>
    public async Task<InventoryItemDetailReportResultDto> RunAsync(InventoryItemDetailReportRequest request, CancellationToken ct)
    {
        DateOnly start = request.FromDate!.Value;
        DateOnly end = request.ToDate!.Value;
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

        IQueryable<ItemRead> items = Items(_db.Items, request);
        IQueryable<ItemOpeningFigure> openings = OpeningFigures(items, _db.StockMovements, start, request.WarehouseId);
        IQueryable<ItemPeriodFigure> periods = PeriodFigures(items, _db.StockMovements, start, end, request.WarehouseId, request.Source);

        var query = from i in items
                    join o in openings on i.ItemId equals o.ItemId
                    join p in periods on i.ItemId equals p.ItemId
                    join cv in _db.ItemCategories on i.ItemCategoryId equals (long?)cv.ItemCategoryId into categorised
                    from c in categorised.DefaultIfEmpty()
                    join uv in _db.UnitsOfMeasure on i.InventoryUomId equals uv.UomId into united
                    from u in united.DefaultIfEmpty()
                    where request.ItemId != null || p.Moves > 0 || o.OpeningQuantity != 0m
                    select new
                    {
                        i.ItemId,
                        i.ItemCode,
                        i.ItemName,
                        CategoryName = c != null ? c.CategoryName : null,
                        i.CostingType,
                        UnitCode = u != null ? u.UomCode : null,
                        o.OpeningQuantity,
                        o.OpeningValue,
                        OpeningUncosted = o.Uncosted,
                        p.InQuantity,
                        p.InValue,
                        p.OutQuantity,
                        p.OutValue,
                        p.Moves,
                        PeriodUncosted = p.Uncosted,
                    };

        var totals = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalOpeningValue = g.Sum(x => x.OpeningValue),
                TotalInValue = g.Sum(x => x.InValue),
                TotalOutValue = g.Sum(x => x.OutValue),
                TotalClosingValue = g.Sum(x => x.OpeningValue + x.InValue - x.OutValue),
                TotalMovements = g.Sum(x => x.Moves),
                CostPending = g.Sum(x => x.OpeningUncosted + x.PeriodUncosted) > 0,
            })
            .FirstOrDefaultAsync(ct);

        int totalCount = await query.CountAsync(ct);

        var pageItems = await query
            .OrderBy(x => x.ItemName)
            .ThenBy(x => x.ItemId)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        List<long> pageItemIds = pageItems.Select(x => x.ItemId).ToList();

        // Movements for the items on this page only
        IQueryable<StockMovementRead> movQuery = _db.StockMovements
            .Where(m => pageItemIds.Contains(m.ItemId) && m.MovementDate >= start && m.MovementDate <= end);

        if (request.WarehouseId is long wid)
        {
            movQuery = movQuery.Where(m => m.WarehouseId == wid);
        }

        if (!string.IsNullOrWhiteSpace(request.Source))
        {
            string src = request.Source.Trim();
            movQuery = movQuery.Where(m => m.SourceType == src || m.MovementType.ToString() == src);
        }

        List<StockMovementRead> movements = await movQuery
            .OrderBy(m => m.ItemId)
            .ThenBy(m => m.MovementDate)
            .ThenBy(m => m.StockMovementId)
            .ToListAsync(ct);

        // Fetch document and reference data for enriching page movements
        List<long> invoiceIds = movements.Where(m => (m.SourceType == "INV" || m.SourceType == "POS") && m.SourceId != null).Select(m => m.SourceId!.Value).Distinct().ToList();
        List<long> billIds = movements.Where(m => m.SourceType == "BIL" && m.SourceId != null).Select(m => m.SourceId!.Value).Distinct().ToList();
        List<long> grnIds = movements.Where(m => m.SourceType == "GRN" && m.SourceId != null).Select(m => m.SourceId!.Value).Distinct().ToList();
        List<long> challanIds = movements.Where(m => (m.SourceType == "DLC" || m.SourceType == "DEL") && m.SourceId != null).Select(m => m.SourceId!.Value).Distinct().ToList();
        List<long> crnIds = movements.Where(m => m.SourceType == "CRN" && m.SourceId != null).Select(m => m.SourceId!.Value).Distinct().ToList();
        List<long> dbnIds = movements.Where(m => m.SourceType == "DBN" && m.SourceId != null).Select(m => m.SourceId!.Value).Distinct().ToList();

        Dictionary<long, (string DocNo, long ContactId)> invoices = invoiceIds.Count > 0
            ? await _db.Invoices.Where(i => invoiceIds.Contains(i.InvoiceId)).ToDictionaryAsync(i => i.InvoiceId, i => (i.DocumentNo, i.ContactId), ct)
            : [];

        Dictionary<long, decimal> invoicePrices = invoiceIds.Count > 0
            ? await (from d in _db.InvoiceDetails
                     join m in _db.StockMovements on d.InvoiceDetailId equals m.SourceLineId
                     where invoiceIds.Contains(d.InvoiceId)
                     select new { m.StockMovementId, Price = d.UnitPrice })
                    .ToDictionaryAsync(x => x.StockMovementId, x => x.Price, ct)
            : [];

        Dictionary<long, (string DocNo, long ContactId)> bills = billIds.Count > 0
            ? await _db.Bills.Where(b => billIds.Contains(b.BillId)).ToDictionaryAsync(b => b.BillId, b => (b.VendorBillNo ?? b.DocumentNo, b.ContactId), ct)
            : [];

        Dictionary<long, (string DocNo, long ContactId)> grns = grnIds.Count > 0
            ? await _db.GoodsReceipts.Where(g => grnIds.Contains(g.GoodsReceiptId)).ToDictionaryAsync(g => g.GoodsReceiptId, g => (g.DocumentNo, g.ContactId), ct)
            : [];

        Dictionary<long, (string DocNo, long ContactId)> challans = challanIds.Count > 0
            ? await _db.DeliveryChallans.Where(c => challanIds.Contains(c.DeliveryChallanId)).ToDictionaryAsync(c => c.DeliveryChallanId, c => (c.DocumentNo, c.ContactId), ct)
            : [];

        Dictionary<long, (string DocNo, long ContactId)> crns = crnIds.Count > 0
            ? await _db.CreditNotes.Where(c => crnIds.Contains(c.CreditNoteId)).ToDictionaryAsync(c => c.CreditNoteId, c => (c.DocumentNo, c.ContactId), ct)
            : [];

        Dictionary<long, (string DocNo, long ContactId)> dbns = dbnIds.Count > 0
            ? await _db.DebitNotes.Where(d => dbnIds.Contains(d.DebitNoteId)).ToDictionaryAsync(d => d.DebitNoteId, d => (d.DocumentNo, d.ContactId), ct)
            : [];

        HashSet<long> contactIds = [
            .. invoices.Values.Select(v => v.ContactId),
            .. bills.Values.Select(v => v.ContactId),
            .. grns.Values.Select(v => v.ContactId),
            .. challans.Values.Select(v => v.ContactId),
            .. crns.Values.Select(v => v.ContactId),
            .. dbns.Values.Select(v => v.ContactId)
        ];

        Dictionary<long, (string Code, string Name)> contacts = contactIds.Count > 0
            ? await _db.Contacts.Where(c => contactIds.Contains(c.ContactId)).ToDictionaryAsync(c => c.ContactId, c => (c.ContactCode, c.DisplayName), ct)
            : [];

        List<long> warehouseIds = movements.Where(m => m.WarehouseId != null).Select(m => m.WarehouseId!.Value).Distinct().ToList();
        Dictionary<long, string> warehouses = warehouseIds.Count > 0
            ? await _db.Warehouses.Where(w => warehouseIds.Contains(w.WarehouseId)).ToDictionaryAsync(w => w.WarehouseId, w => w.WarehouseName, ct)
            : [];

        List<InventoryItemDetailGroupDto> itemsResult = [];

        foreach (var pItem in pageItems)
        {
            var itemMovements = movements.Where(m => m.ItemId == pItem.ItemId).ToList();

            var groupDto = new InventoryItemDetailGroupDto
            {
                ItemId = pItem.ItemId,
                ItemCode = pItem.ItemCode,
                ItemName = pItem.ItemName,
                CategoryName = pItem.CategoryName,
                CostingType = pItem.CostingType,
                UnitCode = pItem.UnitCode,
                OpeningQuantity = pItem.OpeningQuantity,
                OpeningValue = pItem.OpeningValue,
                InQuantity = pItem.InQuantity,
                InValue = pItem.InValue,
                OutQuantity = pItem.OutQuantity,
                OutValue = pItem.OutValue,
                ClosingQuantity = pItem.OpeningQuantity + pItem.InQuantity - pItem.OutQuantity,
                ClosingValue = pItem.OpeningValue + pItem.InValue - pItem.OutValue,
                CostPending = pItem.OpeningUncosted + pItem.PeriodUncosted > 0,
            };

            decimal runningQty = pItem.OpeningQuantity;
            decimal runningVal = pItem.OpeningValue;

            foreach (var m in itemMovements)
            {
                decimal qtySigned = m.Direction == StockDirection.In ? m.Quantity : -m.Quantity;
                decimal? valSigned = m.CostingStatus == CostingStatus.Costed
                    ? (m.Direction == StockDirection.In ? (m.TotalCost ?? 0m) : -(m.TotalCost ?? 0m))
                    : null;

                runningQty += qtySigned;
                if (valSigned.HasValue)
                {
                    runningVal += valSigned.Value;
                }

                (string? docNo, string? cCode, string? cName) = ResolveDocAndContact(m, invoices, bills, grns, challans, crns, dbns, contacts);
                invoicePrices.TryGetValue(m.StockMovementId, out decimal salePrice);
                decimal? unitSalePrice = salePrice > 0 ? salePrice : null;
                decimal? margin = null;
                decimal? marginPct = null;

                if (unitSalePrice.HasValue && m.UnitCost.HasValue)
                {
                    margin = unitSalePrice.Value - m.UnitCost.Value;
                    marginPct = unitSalePrice.Value != 0m ? Math.Round(margin.Value / unitSalePrice.Value * 100m, 2) : null;
                }

                string? whName = m.WarehouseId.HasValue && warehouses.TryGetValue(m.WarehouseId.Value, out string? wname) ? wname : null;

                groupDto.Movements.Add(new InventoryItemDetailMovementDto
                {
                    StockMovementId = m.StockMovementId,
                    Date = m.MovementDate,
                    DocumentNo = docNo,
                    Source = FormatSource(m),
                    SourceType = m.SourceType,
                    SourceId = m.SourceId,
                    ContactCode = cCode,
                    ContactName = cName,
                    WarehouseName = whName,
                    Direction = m.Direction,
                    QuantityIn = m.Direction == StockDirection.In ? m.Quantity : null,
                    QuantityOut = m.Direction == StockDirection.Out ? m.Quantity : null,
                    QuantitySigned = qtySigned,
                    UnitCost = m.UnitCost,
                    Value = valSigned,
                    UnitSalePrice = unitSalePrice,
                    Margin = margin,
                    MarginPercent = marginPct,
                    RunningQuantity = runningQty,
                    RunningValue = runningVal,
                    CostPending = m.CostingStatus != CostingStatus.Costed && m.CostingStatus != CostingStatus.Skipped,
                    Notes = m.Notes,
                });
            }

            itemsResult.Add(groupDto);
        }

        return new InventoryItemDetailReportResultDto
        {
            FromDate = start,
            ToDate = end,
            BaseCurrencyCode = baseCurrency,
            Items = itemsResult,
            TotalItems = totalCount,
            TotalMovements = totals?.TotalMovements ?? 0,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            Totals = new InventoryItemDetailTotalsDto
            {
                OpeningValue = totals?.TotalOpeningValue ?? 0m,
                InValue = totals?.TotalInValue ?? 0m,
                OutValue = totals?.TotalOutValue ?? 0m,
                ClosingValue = totals?.TotalClosingValue ?? 0m,
                TotalMovements = totals?.TotalMovements ?? 0,
                CostPending = totals?.CostPending ?? false,
            },
        };
    }

    // --- Static Builders (testable over in-memory lists without database) -----------------

    public static IQueryable<ItemRead> Items(IQueryable<ItemRead> items, InventoryItemDetailReportRequest request)
    {
        IQueryable<ItemRead> stocked = items.Where(i => i.TrackInventory);

        if (request.ItemCategoryId is long category)
        {
            stocked = stocked.Where(i => i.ItemCategoryId == category);
        }

        return request.ItemId is long item ? stocked.Where(i => i.ItemId == item) : stocked;
    }

    public static IQueryable<ItemOpeningFigure> OpeningFigures(
        IQueryable<ItemRead> items, IQueryable<StockMovementRead> movements, DateOnly start, long? warehouseId)
    {
        IQueryable<StockMovementRead> before = movements.Where(m => m.MovementDate < start);
        if (warehouseId is long wid)
        {
            before = before.Where(m => m.WarehouseId == wid);
        }

        IQueryable<ItemOpeningFigure> moved =
            from m in before
            join i in items on m.ItemId equals i.ItemId
            let qty = m.Direction == StockDirection.In ? m.Quantity : -m.Quantity
            let value = m.CostingStatus == CostingStatus.Costed ? (m.Direction == StockDirection.In ? (m.TotalCost ?? 0m) : -(m.TotalCost ?? 0m)) : 0m
            let uncosted = m.CostingStatus != CostingStatus.Costed && m.CostingStatus != CostingStatus.Skipped ? 1 : 0
            select new ItemOpeningFigure
            {
                ItemId = m.ItemId,
                OpeningQuantity = qty,
                OpeningValue = value,
                Uncosted = uncosted,
            };

        IQueryable<ItemOpeningFigure> none = items.Select(i => new ItemOpeningFigure
        {
            ItemId = i.ItemId,
            OpeningQuantity = 0m,
            OpeningValue = 0m,
            Uncosted = 0,
        });

        return moved.Concat(none)
            .GroupBy(f => f.ItemId)
            .Select(g => new ItemOpeningFigure
            {
                ItemId = g.Key,
                OpeningQuantity = g.Sum(f => f.OpeningQuantity),
                OpeningValue = g.Sum(f => f.OpeningValue),
                Uncosted = g.Sum(f => f.Uncosted),
            });
    }

    public static IQueryable<ItemPeriodFigure> PeriodFigures(
        IQueryable<ItemRead> items, IQueryable<StockMovementRead> movements, DateOnly start, DateOnly end, long? warehouseId, string? source)
    {
        IQueryable<StockMovementRead> period = movements.Where(m => m.MovementDate >= start && m.MovementDate <= end);
        if (warehouseId is long wid)
        {
            period = period.Where(m => m.WarehouseId == wid);
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            string src = source.Trim();
            period = period.Where(m => m.SourceType == src || m.MovementType.ToString() == src);
        }

        IQueryable<ItemPeriodFigure> moved =
            from m in period
            join i in items on m.ItemId equals i.ItemId
            let inQty = m.Direction == StockDirection.In ? m.Quantity : 0m
            let outQty = m.Direction == StockDirection.Out ? m.Quantity : 0m
            let inVal = m.CostingStatus == CostingStatus.Costed && m.Direction == StockDirection.In ? (m.TotalCost ?? 0m) : 0m
            let outVal = m.CostingStatus == CostingStatus.Costed && m.Direction == StockDirection.Out ? (m.TotalCost ?? 0m) : 0m
            let uncosted = m.CostingStatus != CostingStatus.Costed && m.CostingStatus != CostingStatus.Skipped ? 1 : 0
            select new ItemPeriodFigure
            {
                ItemId = m.ItemId,
                InQuantity = inQty,
                InValue = inVal,
                OutQuantity = outQty,
                OutValue = outVal,
                Moves = 1,
                Uncosted = uncosted,
            };

        IQueryable<ItemPeriodFigure> none = items.Select(i => new ItemPeriodFigure
        {
            ItemId = i.ItemId,
            InQuantity = 0m,
            InValue = 0m,
            OutQuantity = 0m,
            OutValue = 0m,
            Moves = 0,
            Uncosted = 0,
        });

        return moved.Concat(none)
            .GroupBy(f => f.ItemId)
            .Select(g => new ItemPeriodFigure
            {
                ItemId = g.Key,
                InQuantity = g.Sum(f => f.InQuantity),
                InValue = g.Sum(f => f.InValue),
                OutQuantity = g.Sum(f => f.OutQuantity),
                OutValue = g.Sum(f => f.OutValue),
                Moves = g.Sum(f => f.Moves),
                Uncosted = g.Sum(f => f.Uncosted),
            });
    }

    private static (string? DocNo, string? ContactCode, string? ContactName) ResolveDocAndContact(
        StockMovementRead m,
        Dictionary<long, (string DocNo, long ContactId)> invoices,
        Dictionary<long, (string DocNo, long ContactId)> bills,
        Dictionary<long, (string DocNo, long ContactId)> grns,
        Dictionary<long, (string DocNo, long ContactId)> challans,
        Dictionary<long, (string DocNo, long ContactId)> crns,
        Dictionary<long, (string DocNo, long ContactId)> dbns,
        Dictionary<long, (string Code, string Name)> contacts)
    {
        if (m.SourceId is long sid)
        {
            (string DocNo, long ContactId)? doc = m.SourceType switch
            {
                "INV" or "POS" => invoices.TryGetValue(sid, out var inv) ? inv : null,
                "BIL" => bills.TryGetValue(sid, out var bill) ? bill : null,
                "GRN" => grns.TryGetValue(sid, out var grn) ? grn : null,
                "DLC" or "DEL" => challans.TryGetValue(sid, out var chal) ? chal : null,
                "CRN" => crns.TryGetValue(sid, out var crn) ? crn : null,
                "DBN" => dbns.TryGetValue(sid, out var dbn) ? dbn : null,
                _ => null,
            };

            if (doc.HasValue)
            {
                contacts.TryGetValue(doc.Value.ContactId, out var c);
                return (doc.Value.DocNo, c.Code, c.Name);
            }

            return ($"{m.SourceType}-{sid}", null, null);
        }

        return (m.Notes, null, null);
    }

    private static string FormatSource(StockMovementRead m) =>
        m.MovementType switch
        {
            StockMovementType.Receipt => m.SourceType == "GRN" ? "Goods Receipt" : (m.SourceType == "BIL" ? "Vendor Bill" : "Receipt"),
            StockMovementType.Issue => m.SourceType == "INV" ? "Invoice" : (m.SourceType == "POS" ? "POS Sale" : (m.SourceType == "DLC" || m.SourceType == "DEL" ? "Delivery Challan" : "Issue")),
            StockMovementType.Adjustment => "Stock Adjustment",
            StockMovementType.Opening => "Opening Balance",
            StockMovementType.TransferIn => "Transfer In",
            StockMovementType.TransferOut => "Transfer Out",
            StockMovementType.SalesReturn => "Sales Return",
            StockMovementType.PurchaseReturn => "Purchase Return",
            _ => m.MovementType.ToString(),
        };
}

public sealed class ItemOpeningFigure
{
    public long ItemId { get; set; }
    public decimal OpeningQuantity { get; set; }
    public decimal OpeningValue { get; set; }
    public int Uncosted { get; set; }
}

public sealed class ItemPeriodFigure
{
    public long ItemId { get; set; }
    public decimal InQuantity { get; set; }
    public decimal InValue { get; set; }
    public decimal OutQuantity { get; set; }
    public decimal OutValue { get; set; }
    public int Moves { get; set; }
    public int Uncosted { get; set; }
}
