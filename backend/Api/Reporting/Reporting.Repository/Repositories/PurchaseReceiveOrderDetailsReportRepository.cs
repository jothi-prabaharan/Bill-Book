using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Purchase Receive Order Details (TK-241): every line of every goods receipt dated in a period.
/// Each posted receipt the filters match, newest first and a page at a time, with its lines: the item,
/// the warehouse it went into, what was delivered, rejected, accepted and billed, what a unit cost and
/// what the accepted goods are worth, and the batch or serial numbers the receipt created. A draft
/// receipt received nothing, and a voided one was never posted (a posted receipt cannot be voided), so
/// neither is listed.
///
/// <b>A line is valued the way the receipt posted it</b> (<c>GoodsReceiptRepository.PostAsync</c>): a
/// unit cost of the line's taxable value over what was <i>delivered</i>, so that rejecting a carton does
/// not raise the cost of the cartons kept, times what was <i>accepted</i>, rounded to two places. That is
/// the debit to Inventory and the credit to Goods Received Not Invoiced, and the accepted quantity is the
/// quantity of the stock-in movement the receipt made. A line is in its receipt's own currency; the value
/// is also given in base at the receipt's rate, and the totals are in base.
///
/// <b>An item or a warehouse narrows the lines as well as the receipts</b>: only a receipt with such a
/// line is listed, and only those lines, so a receipt's value is then of the lines shown.
///
/// <b>The count and the totals are worked out in the database</b> over every line of every receipt the
/// filters match. The page's lines, and the items, units, warehouses, batches, serials, orders and
/// billed quantities they name, are read for the page's receipts only and put together in memory.
/// </summary>
public sealed class PurchaseReceiveOrderDetailsReportRepository
{
    /// <summary>The most serial numbers a line carries in the answer; the count of all of them is always given.</summary>
    public const int MaxSerialsPerLine = 50;

    /// <summary>The most purchase orders the filter offers: the newest ones.</summary>
    public const int MaxOrderChoices = 500;

    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public PurchaseReceiveOrderDetailsReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>The refusals a request needs beyond both dates being given, with the field each belongs to. Pure.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(PurchaseReceiveOrderDetailsReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from)
        {
            refusals.Add((nameof(PurchaseReceiveOrderDetailsReportRequest.ToDate), "To date cannot be before the from date."));
        }

        return refusals;
    }

    /// <summary>The refusals for a vendor, item, warehouse or purchase order that is not on this branch, with the field each belongs to. The query filter decides, so another branch's reads as none.</summary>
    public async Task<IReadOnlyList<(string Field, string Message)>> LookupRefusalsAsync(PurchaseReceiveOrderDetailsReportRequest request, CancellationToken ct)
    {
        List<(string, string)> refusals = [];

        if (request.ContactId is long contactId && !await _db.Contacts.AnyAsync(c => c.ContactId == contactId, ct))
        {
            refusals.Add((nameof(PurchaseReceiveOrderDetailsReportRequest.ContactId), "That vendor is not on this branch."));
        }

        if (request.ItemId is long itemId && !await _db.Items.AnyAsync(i => i.ItemId == itemId, ct))
        {
            refusals.Add((nameof(PurchaseReceiveOrderDetailsReportRequest.ItemId), "That item is not on this branch."));
        }

        if (request.WarehouseId is long warehouseId && !await _db.Warehouses.AnyAsync(w => w.WarehouseId == warehouseId, ct))
        {
            refusals.Add((nameof(PurchaseReceiveOrderDetailsReportRequest.WarehouseId), "That warehouse is not on this branch."));
        }

        if (request.PurchaseOrderId is long orderId && !await _db.PurchaseOrders.AnyAsync(o => o.PurchaseOrderId == orderId, ct))
        {
            refusals.Add((nameof(PurchaseReceiveOrderDetailsReportRequest.PurchaseOrderId), "That purchase order is not on this branch."));
        }

        return refusals;
    }

    /// <summary>The vendors the filter offers, by name: the Aged Payables list.</summary>
    public Task<List<AgedPayablesVendorChoiceDto>> VendorsAsync(CancellationToken ct) =>
        AgedPayablesSummaryReportRepository.VendorChoices(_db.Contacts).ToListAsync(ct);

    /// <summary>The items the filter offers, by name.</summary>
    public Task<List<PayableInvoiceItemChoiceDto>> ItemsAsync(CancellationToken ct) =>
        PayableInvoiceDetailReportRepository.ItemChoices(_db.Items).ToListAsync(ct);

    /// <summary>The warehouses the filter offers, by name.</summary>
    public Task<List<SalesAnalysisWarehouseChoiceDto>> WarehousesAsync(CancellationToken ct) =>
        SalesAnalysisReportRepository.WarehouseChoices(_db.Warehouses).ToListAsync(ct);

    /// <summary>The purchase orders the filter offers: the newest ones a posted receipt was raised against.</summary>
    public Task<List<PurchaseReceiveOrderDetailsOrderChoiceDto>> OrdersAsync(CancellationToken ct) =>
        OrderChoices(_db.PurchaseOrders, _db.GoodsReceipts, _db.Contacts).ToListAsync(ct);

    /// <summary>The report for a request the controller has already validated.</summary>
    public async Task<PurchaseReceiveOrderDetailsReportResultDto> RunAsync(PurchaseReceiveOrderDetailsReportRequest request, CancellationToken ct)
    {
        DateOnly start = request.FromDate!.Value;
        DateOnly end = request.ToDate!.Value;
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

        bool filtered = LinesAreFiltered(request);
        IQueryable<GoodsReceiptDetailRead> lines = Lines(_db.GoodsReceiptDetails, request);
        IQueryable<GoodsReceiptRead> receipts = Receipts(_db.GoodsReceipts, request, start, end);

        if (filtered)
        {
            IQueryable<long> withLines = lines.Select(d => d.GoodsReceiptId);
            receipts = receipts.Where(r => withLines.Contains(r.GoodsReceiptId));
        }

        int matches = await receipts.CountAsync(ct);

        PurchaseReceiveOrderDetailsTotalsDto totals = await LineFigures(lines, receipts)
            .GroupBy(x => 1)
            .Select(g => new PurchaseReceiveOrderDetailsTotalsDto { Lines = g.Count(), Value = g.Sum(x => x.ValueBase) })
            .FirstOrDefaultAsync(ct) ?? new PurchaseReceiveOrderDetailsTotalsDto();

        List<GoodsReceiptRead> page = await receipts
            .OrderByDescending(r => r.DocumentDate)
            .ThenByDescending(r => r.GoodsReceiptId)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        long[] receiptIds = [.. page.Select(r => r.GoodsReceiptId)];
        List<GoodsReceiptDetailRead> pageLines = receiptIds.Length == 0
            ? []
            : await lines
                .Where(d => receiptIds.Contains(d.GoodsReceiptId))
                .OrderBy(d => d.GoodsReceiptId)
                .ThenBy(d => d.LineNumber)
                .ToListAsync(ct);

        long[] lineIds = [.. pageLines.Select(d => d.GoodsReceiptDetailId)];
        List<ReceiptLineBilled> billed = lineIds.Length == 0
            ? []
            : await BilledQuantities(_db.Bills, _db.BillDetails, lineIds).ToListAsync(ct);

        long[] itemIds = [.. pageLines.Where(d => d.ItemId != null).Select(d => d.ItemId!.Value).Distinct()];
        List<ItemRef> items = itemIds.Length == 0
            ? []
            : await _db.Items
                .Where(i => itemIds.Contains(i.ItemId))
                .Select(i => new ItemRef { ItemId = i.ItemId, Code = i.ItemCode, Name = i.ItemName })
                .ToListAsync(ct);

        long[] unitIds = [.. pageLines.Where(d => d.UomId != null).Select(d => d.UomId!.Value).Distinct()];
        List<UnitRef> units = unitIds.Length == 0
            ? []
            : await _db.UnitsOfMeasure
                .Where(u => unitIds.Contains(u.UomId))
                .Select(u => new UnitRef { UomId = u.UomId, Code = u.UomCode })
                .ToListAsync(ct);

        long[] warehouseIds = [.. pageLines.Where(d => d.WarehouseId != null).Select(d => d.WarehouseId!.Value).Distinct()];
        List<ReceiptWarehouseRef> warehouses = warehouseIds.Length == 0
            ? []
            : await _db.Warehouses
                .Where(w => warehouseIds.Contains(w.WarehouseId))
                .Select(w => new ReceiptWarehouseRef { WarehouseId = w.WarehouseId, Name = w.WarehouseName })
                .ToListAsync(ct);

        long[] batchIds = [.. pageLines.Where(d => d.ItemBatchId != null).Select(d => d.ItemBatchId!.Value).Distinct()];
        List<ReceiptBatchRef> batches = batchIds.Length == 0
            ? []
            : await _db.ItemBatches
                .Where(b => batchIds.Contains(b.ItemBatchId))
                .Select(b => new ReceiptBatchRef { ItemBatchId = b.ItemBatchId, BatchNumber = b.BatchNumber, ExpiryDate = b.ExpiryDate })
                .ToListAsync(ct);

        List<ReceivedSerial> serials = receiptIds.Length == 0
            ? []
            : await Serials(_db.ItemSerials, _db.StockMovements, receiptIds).ToListAsync(ct);

        long[] orderIds = [.. page.Where(r => r.PurchaseOrderId != null).Select(r => r.PurchaseOrderId!.Value).Distinct()];
        List<ReceiptOrderRef> orders = orderIds.Length == 0
            ? []
            : await _db.PurchaseOrders
                .Where(o => orderIds.Contains(o.PurchaseOrderId))
                .Select(o => new ReceiptOrderRef { PurchaseOrderId = o.PurchaseOrderId, DocumentNo = o.DocumentNo })
                .ToListAsync(ct);

        long[] contactIds = [.. page.Select(r => r.ContactId).Distinct()];
        List<VendorRef> vendors = contactIds.Length == 0
            ? []
            : await _db.Contacts
                .Where(c => contactIds.Contains(c.ContactId))
                .Select(c => new VendorRef { ContactId = c.ContactId, Code = c.ContactCode, Name = c.DisplayName, IsMsme = c.IsMsme })
                .ToListAsync(ct);

        return Compose(request, page, matches, pageLines, billed, items, units, warehouses, batches, serials, orders, vendors, totals, baseCurrency);
    }

    // --- builders: LINQ over the read models, so a test runs them over lists -------------

    /// <summary>Whether an item or a warehouse narrows the lines. Pure.</summary>
    public static bool LinesAreFiltered(PurchaseReceiveOrderDetailsReportRequest request) =>
        request.ItemId != null || request.WarehouseId != null;

    /// <summary>The posted receipts dated in the period, narrowed by vendor and purchase order. A draft received nothing, and a voided receipt was never posted. LINQ only.</summary>
    public static IQueryable<GoodsReceiptRead> Receipts(
        IQueryable<GoodsReceiptRead> receipts, PurchaseReceiveOrderDetailsReportRequest request, DateOnly start, DateOnly end)
    {
        IQueryable<GoodsReceiptRead> matching = receipts.Where(r =>
            r.Status == DocumentStatus.Posted && r.DocumentDate >= start && r.DocumentDate <= end);

        if (request.ContactId is long contact)
        {
            matching = matching.Where(r => r.ContactId == contact);
        }

        if (request.PurchaseOrderId is long order)
        {
            matching = matching.Where(r => r.PurchaseOrderId == order);
        }

        return matching;
    }

    /// <summary>The receipt lines the report is about: every line, or only those for the item, or into the warehouse, asked for. LINQ only.</summary>
    public static IQueryable<GoodsReceiptDetailRead> Lines(IQueryable<GoodsReceiptDetailRead> details, PurchaseReceiveOrderDetailsReportRequest request)
    {
        IQueryable<GoodsReceiptDetailRead> lines = details;

        if (request.ItemId is long itemId)
        {
            lines = lines.Where(d => d.ItemId == itemId);
        }

        if (request.WarehouseId is long warehouseId)
        {
            lines = lines.Where(d => d.WarehouseId == warehouseId);
        }

        return lines;
    }

    /// <summary>
    /// Each line of the matching receipts in base currency: what the accepted goods are worth, the line's
    /// taxable value shared out over what was delivered and taken at the receipt's rate, each rounded to
    /// two places as Purchase does. The totals are summed from this in the database. LINQ only.
    /// </summary>
    public static IQueryable<ReceiptLineFigure> LineFigures(IQueryable<GoodsReceiptDetailRead> lines, IQueryable<GoodsReceiptRead> receipts) =>
        from d in lines
        join r in receipts on d.GoodsReceiptId equals r.GoodsReceiptId
        select new ReceiptLineFigure
        {
            GoodsReceiptId = r.GoodsReceiptId,
            ValueBase = d.Quantity == 0m ? 0m : Math.Round(Math.Round(d.TaxableAmount * d.AcceptedQuantity / d.Quantity, 2) * r.ExchangeRate, 2),
        };

    /// <summary>
    /// How much each of the given receipt lines has been billed for: what the posted bills' lines naming
    /// it add up to. A draft or voided bill bills nothing. LINQ only.
    /// </summary>
    public static IQueryable<ReceiptLineBilled> BilledQuantities(
        IQueryable<BillRead> bills, IQueryable<BillDetailRead> billDetails, IReadOnlyCollection<long> lineIds) =>
        from bd in billDetails
        join b in bills on bd.BillId equals b.BillId
        where b.Status == DocumentStatus.Posted && bd.GoodsReceiptDetailId != null && lineIds.Contains(bd.GoodsReceiptDetailId.Value)
        group bd by bd.GoodsReceiptDetailId!.Value into g
        select new ReceiptLineBilled { GoodsReceiptDetailId = g.Key, Quantity = g.Sum(x => x.Quantity) };

    /// <summary>
    /// The serial numbers the given receipts created: a serial whose stock-in movement is the receipt's
    /// own (source GRN, the receipt's id, the line's number). LINQ only.
    /// </summary>
    public static IQueryable<ReceivedSerial> Serials(
        IQueryable<ItemSerialRead> serials, IQueryable<StockMovementRead> movements, IReadOnlyCollection<long> receiptIds) =>
        from s in serials
        join m in movements on s.ReceivedMovementId equals (long?)m.StockMovementId
        where m.SourceType == "GRN" && m.SourceId != null && receiptIds.Contains(m.SourceId.Value)
        select new ReceivedSerial { GoodsReceiptId = m.SourceId!.Value, LineNumber = m.SourceLineId, SerialNumber = s.SerialNumber };

    /// <summary>The newest purchase orders a posted receipt was raised against, with the vendor, for a filter. LINQ only.</summary>
    public static IQueryable<PurchaseReceiveOrderDetailsOrderChoiceDto> OrderChoices(
        IQueryable<PurchaseOrderRead> orders, IQueryable<GoodsReceiptRead> receipts, IQueryable<ContactRead> contacts) =>
        (from o in orders
         join c in contacts on o.ContactId equals c.ContactId
         where o.Status == DocumentStatus.Posted
             && receipts.Any(r => r.PurchaseOrderId == o.PurchaseOrderId && r.Status == DocumentStatus.Posted)
         orderby o.DocumentDate descending, o.PurchaseOrderId descending
         select new PurchaseReceiveOrderDetailsOrderChoiceDto
         {
             PurchaseOrderId = o.PurchaseOrderId,
             DocumentNo = o.DocumentNo,
             DocumentDate = o.DocumentDate,
             ContactName = c.DisplayName,
         }).Take(MaxOrderChoices);

    // --- compose: pure, so a test needs no database --------------------------------------

    /// <summary>
    /// The page's receipts with their lines. A line takes the item's code and name, else its own
    /// description; its unit is the unit's code and its warehouse the warehouse's name; its unit cost is
    /// the taxable value over what was delivered and its value that cost times what was accepted, rounded
    /// to two places, and in base at the receipt's rate. Billed is what the bills named it add up to. A
    /// line's batch is its batch's number and expiry, and its serials are those the receipt created for
    /// it, the first <see cref="MaxSerialsPerLine"/> of them. A receipt's value is of the lines shown. Pure.
    /// </summary>
    public static PurchaseReceiveOrderDetailsReportResultDto Compose(
        PurchaseReceiveOrderDetailsReportRequest request,
        IReadOnlyList<GoodsReceiptRead> page,
        int totalReceipts,
        IReadOnlyList<GoodsReceiptDetailRead> lines,
        IReadOnlyList<ReceiptLineBilled> billed,
        IReadOnlyList<ItemRef> items,
        IReadOnlyList<UnitRef> units,
        IReadOnlyList<ReceiptWarehouseRef> warehouses,
        IReadOnlyList<ReceiptBatchRef> batches,
        IReadOnlyList<ReceivedSerial> serials,
        IReadOnlyList<ReceiptOrderRef> orders,
        IReadOnlyList<VendorRef> vendors,
        PurchaseReceiveOrderDetailsTotalsDto totals,
        string baseCurrency)
    {
        Dictionary<long, VendorRef> names = vendors.GroupBy(c => c.ContactId).ToDictionary(g => g.Key, g => g.First());
        Dictionary<long, ItemRef> itemsById = items.GroupBy(i => i.ItemId).ToDictionary(g => g.Key, g => g.First());
        Dictionary<long, string> unitCodes = units.GroupBy(u => u.UomId).ToDictionary(g => g.Key, g => g.First().Code);
        Dictionary<long, string> warehouseNames = warehouses.GroupBy(w => w.WarehouseId).ToDictionary(g => g.Key, g => g.First().Name);
        Dictionary<long, ReceiptBatchRef> batchesById = batches.GroupBy(b => b.ItemBatchId).ToDictionary(g => g.Key, g => g.First());
        Dictionary<long, string> orderNos = orders.GroupBy(o => o.PurchaseOrderId).ToDictionary(g => g.Key, g => g.First().DocumentNo);
        Dictionary<long, decimal> billedBy = billed.GroupBy(b => b.GoodsReceiptDetailId).ToDictionary(g => g.Key, g => g.Sum(b => b.Quantity));
        ILookup<long, GoodsReceiptDetailRead> byReceipt = lines.ToLookup(d => d.GoodsReceiptId);
        ILookup<(long Receipt, long Line), string> serialsByLine = serials.ToLookup(s => (s.GoodsReceiptId, s.LineNumber), s => s.SerialNumber);

        List<PurchaseReceiveOrderDetailsReceiptDto> receipts = [];
        foreach (GoodsReceiptRead receipt in page)
        {
            VendorRef? vendor = names.GetValueOrDefault(receipt.ContactId);
            List<PurchaseReceiveOrderDetailsLineDto> shown =
            [
                .. byReceipt[receipt.GoodsReceiptId]
                    .OrderBy(d => d.LineNumber)
                    .Select(d => LineOf(d, receipt, itemsById, unitCodes, warehouseNames, batchesById, billedBy, serialsByLine)),
            ];

            receipts.Add(new PurchaseReceiveOrderDetailsReceiptDto
            {
                GoodsReceiptId = receipt.GoodsReceiptId,
                DocumentNo = receipt.DocumentNo,
                DocumentDate = receipt.DocumentDate,
                ContactId = receipt.ContactId,
                ContactCode = vendor?.Code ?? string.Empty,
                ContactName = vendor?.Name ?? $"Vendor {receipt.ContactId}",
                CurrencyCode = receipt.CurrencyCode,
                PurchaseOrderId = receipt.PurchaseOrderId,
                PurchaseOrderNo = receipt.PurchaseOrderId is long orderId ? orderNos.GetValueOrDefault(orderId) : null,
                VendorDeliveryNoteNo = receipt.VendorDeliveryNoteNo,
                VendorDeliveryNoteDate = receipt.VendorDeliveryNoteDate,
                Lines = shown,
                Value = shown.Sum(l => l.Value),
                ValueBase = shown.Sum(l => l.ValueBase),
            });
        }

        return new PurchaseReceiveOrderDetailsReportResultDto
        {
            FromDate = request.FromDate!.Value,
            ToDate = request.ToDate!.Value,
            BaseCurrencyCode = baseCurrency,
            LinesFiltered = LinesAreFiltered(request),
            Receipts = receipts,
            TotalReceipts = totalReceipts,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            Totals = totals,
        };
    }

    private static PurchaseReceiveOrderDetailsLineDto LineOf(
        GoodsReceiptDetailRead d,
        GoodsReceiptRead receipt,
        IReadOnlyDictionary<long, ItemRef> items,
        IReadOnlyDictionary<long, string> unitCodes,
        IReadOnlyDictionary<long, string> warehouseNames,
        IReadOnlyDictionary<long, ReceiptBatchRef> batches,
        IReadOnlyDictionary<long, decimal> billed,
        ILookup<(long Receipt, long Line), string> serials)
    {
        ItemRef? item = d.ItemId is long itemId ? items.GetValueOrDefault(itemId) : null;
        ReceiptBatchRef? batch = d.ItemBatchId is long batchId ? batches.GetValueOrDefault(batchId) : null;
        List<string> serialNumbers = [.. serials[(d.GoodsReceiptId, d.LineNumber)].Order(StringComparer.Ordinal)];
        decimal value = d.Quantity == 0m ? 0m : Math.Round(d.TaxableAmount * d.AcceptedQuantity / d.Quantity, 2);

        return new PurchaseReceiveOrderDetailsLineDto
        {
            GoodsReceiptDetailId = d.GoodsReceiptDetailId,
            LineNumber = d.LineNumber,
            ItemId = d.ItemId,
            ItemCode = item?.Code,
            ItemName = item?.Name ?? d.Description ?? string.Empty,
            UnitCode = d.UomId is long uomId ? unitCodes.GetValueOrDefault(uomId) : null,
            WarehouseId = d.WarehouseId,
            WarehouseName = d.WarehouseId is long warehouseId ? warehouseNames.GetValueOrDefault(warehouseId) : null,
            Delivered = d.Quantity,
            Rejected = d.RejectedQuantity,
            Accepted = d.AcceptedQuantity,
            Billed = billed.GetValueOrDefault(d.GoodsReceiptDetailId),
            UnitCost = d.Quantity == 0m ? 0m : Math.Round(d.TaxableAmount / d.Quantity, 6),
            Value = value,
            ValueBase = Math.Round(value * receipt.ExchangeRate, 2),
            RejectionReason = d.RejectionReason,
            BatchNumber = batch?.BatchNumber,
            BatchExpiryDate = batch?.ExpiryDate,
            Serials = [.. serialNumbers.Take(MaxSerialsPerLine)],
            SerialCount = serialNumbers.Count,
        };
    }
}

/// <summary>A receipt line's value in base currency.</summary>
public sealed class ReceiptLineFigure
{
    public long GoodsReceiptId { get; set; }

    public decimal ValueBase { get; set; }
}

/// <summary>What posted bills have billed against a receipt line.</summary>
public sealed class ReceiptLineBilled
{
    public long GoodsReceiptDetailId { get; set; }

    public decimal Quantity { get; set; }
}

/// <summary>A serial number a receipt created, with the line it came in on.</summary>
public sealed class ReceivedSerial
{
    public long GoodsReceiptId { get; set; }

    /// <summary>The receipt line's number: Purchase sends it as the movement's source line.</summary>
    public long LineNumber { get; set; }

    public string SerialNumber { get; set; } = string.Empty;
}

/// <summary>A warehouse's name.</summary>
public sealed class ReceiptWarehouseRef
{
    public long WarehouseId { get; set; }

    public string Name { get; set; } = string.Empty;
}

/// <summary>A batch's number and expiry.</summary>
public sealed class ReceiptBatchRef
{
    public long ItemBatchId { get; set; }

    public string BatchNumber { get; set; } = string.Empty;

    public DateOnly? ExpiryDate { get; set; }
}

/// <summary>A purchase order's number.</summary>
public sealed class ReceiptOrderRef
{
    public long PurchaseOrderId { get; set; }

    public string DocumentNo { get; set; } = string.Empty;
}
