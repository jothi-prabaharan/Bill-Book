using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Batch Tracking Detail Report (TK-248): every movement of each batch over a period,
/// grouped by batch with opening, chronological movements (doc, contact, in, out, running), and closing.
/// </summary>
public sealed class BatchTrackingDetailReportRepository
{
    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public BatchTrackingDetailReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>Validation refusals for date ordering.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(BatchTrackingDetailReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from)
        {
            refusals.Add((nameof(BatchTrackingDetailReportRequest.ToDate), "To date cannot be before the from date."));
        }

        return refusals;
    }

    /// <summary>Refusals for foreign keys not existing on this branch.</summary>
    public async Task<IReadOnlyList<(string Field, string Message)>> LookupRefusalsAsync(BatchTrackingDetailReportRequest request, CancellationToken ct)
    {
        List<(string, string)> refusals = [];

        if (request.BatchId is long batchId && !await _db.ItemBatches.AnyAsync(b => b.ItemBatchId == batchId, ct))
        {
            refusals.Add((nameof(BatchTrackingDetailReportRequest.BatchId), "That batch is not on this branch."));
        }

        if (request.ItemId is long itemId && !await _db.Items.AnyAsync(i => i.ItemId == itemId, ct))
        {
            refusals.Add((nameof(BatchTrackingDetailReportRequest.ItemId), "That item is not on this branch."));
        }

        if (request.WarehouseId is long warehouseId && !await _db.Warehouses.AnyAsync(w => w.WarehouseId == warehouseId, ct))
        {
            refusals.Add((nameof(BatchTrackingDetailReportRequest.WarehouseId), "That warehouse is not on this branch."));
        }

        if (request.ContactId is long contactId && !await _db.Contacts.AnyAsync(c => c.ContactId == contactId, ct))
        {
            refusals.Add((nameof(BatchTrackingDetailReportRequest.ContactId), "That contact is not on this branch."));
        }

        return refusals;
    }

    /// <summary>Available batches on this branch.</summary>
    public async Task<List<BatchTrackingBatchChoiceDto>> BatchesAsync(CancellationToken ct)
    {
        return await (from b in _db.ItemBatches
                      join i in _db.Items on b.ItemId equals i.ItemId
                      orderby b.BatchNumber
                      select new BatchTrackingBatchChoiceDto
                      {
                          ItemBatchId = b.ItemBatchId,
                          BatchNumber = b.BatchNumber,
                          ItemCode = i.ItemCode,
                          ItemName = i.ItemName
                      })
                      .Take(200)
                      .ToListAsync(ct);
    }

    /// <summary>Available items that have batch tracking enabled or existing batches.</summary>
    public async Task<List<PayableInvoiceItemChoiceDto>> ItemsAsync(CancellationToken ct)
    {
        return await (from i in _db.Items
                      join b in _db.ItemBatches on i.ItemId equals b.ItemId
                      select new PayableInvoiceItemChoiceDto
                      {
                          ItemId = i.ItemId,
                          ItemCode = i.ItemCode,
                          ItemName = i.ItemName
                      })
                      .Distinct()
                      .OrderBy(i => i.ItemName)
                      .ToListAsync(ct);
    }

    /// <summary>Available warehouses.</summary>
    public Task<List<SalesAnalysisWarehouseChoiceDto>> WarehousesAsync(CancellationToken ct) =>
        SalesAnalysisReportRepository.WarehouseChoices(_db.Warehouses).ToListAsync(ct);

    /// <summary>Available contacts.</summary>
    public async Task<List<BatchTrackingContactChoiceDto>> ContactsAsync(CancellationToken ct)
    {
        return await _db.Contacts
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayName)
            .Select(c => new BatchTrackingContactChoiceDto
            {
                ContactId = c.ContactId,
                ContactCode = c.ContactCode,
                DisplayName = c.DisplayName
            })
            .Take(200)
            .ToListAsync(ct);
    }

    public async Task<BatchTrackingDetailReportResultDto> RunAsync(BatchTrackingDetailReportRequest request, CancellationToken ct)
    {
        DateOnly start = request.FromDate!.Value;
        DateOnly end = request.ToDate!.Value;
        string currency = await _currency.GetBaseCurrencyAsync(ct) ?? "INR";

        IQueryable<StockMovementRead> baseMovements = _db.StockMovements.Where(m => m.ItemBatchId != null);
        if (request.WarehouseId is long wid)
        {
            baseMovements = baseMovements.Where(m => m.WarehouseId == wid);
        }

        // Opening movements by batch
        var openingsQuery = baseMovements
            .Where(m => m.MovementDate < start)
            .GroupBy(m => m.ItemBatchId!.Value)
            .Select(g => new
            {
                ItemBatchId = g.Key,
                OpeningQuantity = g.Sum(m => m.Direction == StockDirection.In ? m.Quantity : -m.Quantity)
            });

        // Period movements summary by batch
        var periodsQuery = baseMovements
            .Where(m => m.MovementDate >= start && m.MovementDate <= end)
            .GroupBy(m => m.ItemBatchId!.Value)
            .Select(g => new
            {
                ItemBatchId = g.Key,
                InQuantity = g.Sum(m => m.Direction == StockDirection.In ? m.Quantity : 0m),
                OutQuantity = g.Sum(m => m.Direction == StockDirection.Out ? m.Quantity : 0m),
                MovementCount = g.Count()
            });

        IQueryable<ItemBatchRead> batchesQuery = _db.ItemBatches.AsQueryable();
        if (request.BatchId is long bid)
        {
            batchesQuery = batchesQuery.Where(b => b.ItemBatchId == bid);
        }
        if (request.ItemId is long iid)
        {
            batchesQuery = batchesQuery.Where(b => b.ItemId == iid);
        }

        var combinedQuery = from b in batchesQuery
                            join i in _db.Items on b.ItemId equals i.ItemId
                            join c in _db.ItemCategories on i.ItemCategoryId equals (long?)c.ItemCategoryId into categorised
                            from c in categorised.DefaultIfEmpty()
                            join u in _db.UnitsOfMeasure on i.InventoryUomId equals u.UomId into united
                            from u in united.DefaultIfEmpty()
                            join op in openingsQuery on b.ItemBatchId equals op.ItemBatchId into opJoin
                            from op in opJoin.DefaultIfEmpty()
                            join per in periodsQuery on b.ItemBatchId equals per.ItemBatchId into perJoin
                            from per in perJoin.DefaultIfEmpty()
                            let openQty = op != null ? op.OpeningQuantity : 0m
                            let inQty = per != null ? per.InQuantity : 0m
                            let outQty = per != null ? per.OutQuantity : 0m
                            let moves = per != null ? per.MovementCount : 0
                            where request.BatchId != null || moves > 0 || openQty != 0m
                            select new
                            {
                                b.ItemBatchId,
                                b.BatchNumber,
                                b.ManufactureDate,
                                b.ExpiryDate,
                                b.Mrp,
                                i.ItemId,
                                i.ItemCode,
                                i.ItemName,
                                CategoryName = c != null ? c.CategoryName : null,
                                CostingMethod = i.CostingType.ToString(),
                                UnitCode = u != null ? u.UomCode : null,
                                OpeningQuantity = openQty,
                                TotalIn = inQty,
                                TotalOut = outQty,
                                ClosingQuantity = openQty + inQty - outQty,
                                MovementCount = moves
                            };

        var totals = await combinedQuery
            .GroupBy(_ => 1)
            .Select(g => new BatchTrackingDetailTotalsDto
            {
                TotalBatches = g.Count(),
                TotalOpeningQuantity = g.Sum(x => x.OpeningQuantity),
                TotalInQuantity = g.Sum(x => x.TotalIn),
                TotalOutQuantity = g.Sum(x => x.TotalOut),
                TotalClosingQuantity = g.Sum(x => x.ClosingQuantity),
                TotalMovements = g.Sum(x => x.MovementCount)
            })
            .FirstOrDefaultAsync(ct) ?? new BatchTrackingDetailTotalsDto();

        int totalCount = await combinedQuery.CountAsync(ct);

        var pageBatches = await combinedQuery
            .OrderBy(x => x.BatchNumber)
            .ThenBy(x => x.ItemBatchId)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        List<long> pageBatchIds = pageBatches.Select(x => x.ItemBatchId).ToList();

        // Load movements for the batches on this page
        IQueryable<StockMovementRead> pageMovQuery = _db.StockMovements
            .Where(m => m.ItemBatchId != null && pageBatchIds.Contains(m.ItemBatchId.Value) && m.MovementDate >= start && m.MovementDate <= end);

        if (request.WarehouseId is long pageWid)
        {
            pageMovQuery = pageMovQuery.Where(m => m.WarehouseId == pageWid);
        }

        List<StockMovementRead> movements = await pageMovQuery
            .OrderBy(m => m.MovementDate)
            .ThenBy(m => m.StockMovementId)
            .ToListAsync(ct);

        // Enrich document references
        List<long> invoiceIds = movements.Where(m => (m.SourceType == "INV" || m.SourceType == "POS") && m.SourceId != null).Select(m => m.SourceId!.Value).Distinct().ToList();
        List<long> billIds = movements.Where(m => m.SourceType == "BIL" && m.SourceId != null).Select(m => m.SourceId!.Value).Distinct().ToList();
        List<long> grnIds = movements.Where(m => m.SourceType == "GRN" && m.SourceId != null).Select(m => m.SourceId!.Value).Distinct().ToList();
        List<long> challanIds = movements.Where(m => (m.SourceType == "DLC" || m.SourceType == "DEL") && m.SourceId != null).Select(m => m.SourceId!.Value).Distinct().ToList();
        List<long> crnIds = movements.Where(m => m.SourceType == "CRN" && m.SourceId != null).Select(m => m.SourceId!.Value).Distinct().ToList();
        List<long> dbnIds = movements.Where(m => m.SourceType == "DBN" && m.SourceId != null).Select(m => m.SourceId!.Value).Distinct().ToList();

        Dictionary<long, (string DocNo, long ContactId)> invoices = invoiceIds.Count > 0
            ? await _db.Invoices.Where(i => invoiceIds.Contains(i.InvoiceId)).ToDictionaryAsync(i => i.InvoiceId, i => (i.DocumentNo, i.ContactId), ct)
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

        Dictionary<long, string> contacts = contactIds.Count > 0
            ? await _db.Contacts.Where(c => contactIds.Contains(c.ContactId)).ToDictionaryAsync(c => c.ContactId, c => c.DisplayName, ct)
            : [];

        List<long> warehouseIds = movements.Where(m => m.WarehouseId != null).Select(m => m.WarehouseId!.Value).Distinct().ToList();
        Dictionary<long, string> warehouses = warehouseIds.Count > 0
            ? await _db.Warehouses.Where(w => warehouseIds.Contains(w.WarehouseId)).ToDictionaryAsync(w => w.WarehouseId, w => w.WarehouseName, ct)
            : [];

        List<BatchTrackingDetailGroupDto> batchGroups = [];

        foreach (var pBatch in pageBatches)
        {
            var batchMoves = movements.Where(m => m.ItemBatchId == pBatch.ItemBatchId).ToList();

            var groupDto = new BatchTrackingDetailGroupDto
            {
                ItemBatchId = pBatch.ItemBatchId,
                BatchNumber = pBatch.BatchNumber,
                ItemId = pBatch.ItemId,
                ItemCode = pBatch.ItemCode,
                ItemName = pBatch.ItemName,
                CategoryName = pBatch.CategoryName,
                UnitCode = pBatch.UnitCode,
                ManufactureDate = pBatch.ManufactureDate,
                ExpiryDate = pBatch.ExpiryDate,
                CostingMethod = pBatch.CostingMethod,
                OpeningQuantity = pBatch.OpeningQuantity,
                TotalIn = pBatch.TotalIn,
                TotalOut = pBatch.TotalOut,
                ClosingQuantity = pBatch.ClosingQuantity,
                Movements = []
            };

            decimal running = pBatch.OpeningQuantity;

            foreach (var m in batchMoves)
            {
                string? docNo = null;
                string? contactName = null;

                if (m.SourceId is long sid)
                {
                    if ((m.SourceType == "INV" || m.SourceType == "POS") && invoices.TryGetValue(sid, out var inv))
                    {
                        docNo = inv.DocNo;
                        contacts.TryGetValue(inv.ContactId, out contactName);
                    }
                    else if (m.SourceType == "BIL" && bills.TryGetValue(sid, out var bil))
                    {
                        docNo = bil.DocNo;
                        contacts.TryGetValue(bil.ContactId, out contactName);
                    }
                    else if (m.SourceType == "GRN" && grns.TryGetValue(sid, out var grn))
                    {
                        docNo = grn.DocNo;
                        contacts.TryGetValue(grn.ContactId, out contactName);
                    }
                    else if ((m.SourceType == "DLC" || m.SourceType == "DEL") && challans.TryGetValue(sid, out var dlc))
                    {
                        docNo = dlc.DocNo;
                        contacts.TryGetValue(dlc.ContactId, out contactName);
                    }
                    else if (m.SourceType == "CRN" && crns.TryGetValue(sid, out var crn))
                    {
                        docNo = crn.DocNo;
                        contacts.TryGetValue(crn.ContactId, out contactName);
                    }
                    else if (m.SourceType == "DBN" && dbns.TryGetValue(sid, out var dbn))
                    {
                        docNo = dbn.DocNo;
                        contacts.TryGetValue(dbn.ContactId, out contactName);
                    }
                }

                if (docNo == null && m.SourceType != null && m.SourceId != null)
                {
                    docNo = $"{m.SourceType}-{m.SourceId}";
                }

                string whName = m.WarehouseId != null && warehouses.TryGetValue(m.WarehouseId.Value, out var wName)
                    ? wName
                    : "—";

                decimal qIn = m.Direction == StockDirection.In ? m.Quantity : 0m;
                decimal qOut = m.Direction == StockDirection.Out ? m.Quantity : 0m;
                running += (qIn - qOut);

                groupDto.Movements.Add(new BatchTrackingDetailMovementDto
                {
                    StockMovementId = m.StockMovementId,
                    MovementDate = m.MovementDate,
                    MovementType = m.MovementType.ToString(),
                    SourceType = m.SourceType,
                    SourceId = m.SourceId,
                    DocumentNumber = docNo,
                    ContactName = contactName,
                    WarehouseName = whName,
                    Direction = m.Direction == StockDirection.In ? "In" : "Out",
                    QuantityIn = qIn,
                    QuantityOut = qOut,
                    RunningBalance = running
                });
            }

            if (batchMoves.Count > 0)
            {
                groupDto.WarehouseName = groupDto.Movements.Last().WarehouseName;
            }

            batchGroups.Add(groupDto);
        }

        return new BatchTrackingDetailReportResultDto
        {
            FromDate = start,
            ToDate = end,
            Currency = currency,
            Batches = batchGroups,
            Totals = totals,
            TotalItems = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }
}
