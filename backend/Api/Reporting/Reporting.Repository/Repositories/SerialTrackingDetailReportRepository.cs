using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Serial Tracking Detail Report (TK-250): every movement of each serial number over a period,
/// grouped by serial with opening, chronological movements (doc, contact, in, out, running), and closing.
/// </summary>
public sealed class SerialTrackingDetailReportRepository
{
    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public SerialTrackingDetailReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>Validation refusals for required fields and date ordering.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(SerialTrackingDetailReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.FromDate == null)
        {
            refusals.Add((nameof(SerialTrackingDetailReportRequest.FromDate), "From date is required."));
        }

        if (request.ToDate == null)
        {
            refusals.Add((nameof(SerialTrackingDetailReportRequest.ToDate), "To date is required."));
        }

        if (request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from)
        {
            refusals.Add((nameof(SerialTrackingDetailReportRequest.ToDate), "To date cannot be before the from date."));
        }

        return refusals;
    }

    /// <summary>Refusals for foreign keys not existing on this branch.</summary>
    public async Task<IReadOnlyList<(string Field, string Message)>> LookupRefusalsAsync(SerialTrackingDetailReportRequest request, CancellationToken ct)
    {
        List<(string, string)> refusals = [];

        if (request.ItemSerialId is long serialId && !await _db.ItemSerials.AnyAsync(s => s.ItemSerialId == serialId, ct))
        {
            refusals.Add((nameof(SerialTrackingDetailReportRequest.ItemSerialId), "That serial number is not on this branch."));
        }

        if (request.ItemId is long itemId && !await _db.Items.AnyAsync(i => i.ItemId == itemId, ct))
        {
            refusals.Add((nameof(SerialTrackingDetailReportRequest.ItemId), "That item is not on this branch."));
        }

        if (request.WarehouseId is long warehouseId && !await _db.Warehouses.AnyAsync(w => w.WarehouseId == warehouseId, ct))
        {
            refusals.Add((nameof(SerialTrackingDetailReportRequest.WarehouseId), "That warehouse is not on this branch."));
        }

        if (request.ContactId is long contactId && !await _db.Contacts.AnyAsync(c => c.ContactId == contactId, ct))
        {
            refusals.Add((nameof(SerialTrackingDetailReportRequest.ContactId), "That contact is not on this branch."));
        }

        return refusals;
    }

    /// <summary>Available serial numbers on this branch.</summary>
    public async Task<List<SerialTrackingChoiceDto>> SerialsAsync(CancellationToken ct)
    {
        return await (from s in _db.ItemSerials
                      join i in _db.Items on s.ItemId equals i.ItemId
                      orderby s.SerialNumber
                      select new SerialTrackingChoiceDto
                      {
                          ItemSerialId = s.ItemSerialId,
                          SerialNumber = s.SerialNumber,
                          ItemCode = i.ItemCode,
                          ItemName = i.ItemName
                      })
                      .Take(200)
                      .ToListAsync(ct);
    }

    /// <summary>Available items that have serial tracking.</summary>
    public async Task<List<SerialTrackingItemChoiceDto>> ItemsAsync(CancellationToken ct)
    {
        return await (from i in _db.Items
                      join s in _db.ItemSerials on i.ItemId equals s.ItemId
                      select new SerialTrackingItemChoiceDto
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

    public async Task<SerialTrackingDetailReportResultDto> RunAsync(SerialTrackingDetailReportRequest request, CancellationToken ct)
    {
        DateOnly start = request.FromDate!.Value;
        DateOnly end = request.ToDate!.Value;
        string currency = await _currency.GetBaseCurrencyAsync(ct) ?? "INR";

        var serials = _db.ItemSerials.AsQueryable();

        if (request.ItemSerialId is long sid)
        {
            serials = serials.Where(s => s.ItemSerialId == sid);
        }

        if (request.ItemId is long iid)
        {
            serials = serials.Where(s => s.ItemId == iid);
        }

        if (request.WarehouseId is long wid)
        {
            serials = serials.Where(s => s.WarehouseId == wid);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string term = request.Search.Trim();
            serials = serials.Where(s => s.SerialNumber.Contains(term));
        }

        var baseQuery = from s in serials
                        join i in _db.Items on s.ItemId equals i.ItemId
                        join c in _db.ItemCategories on i.ItemCategoryId equals c.ItemCategoryId into categories
                        from c in categories.DefaultIfEmpty()
                        join u in _db.UnitsOfMeasure on i.InventoryUomId equals u.UomId into uoms
                        from u in uoms.DefaultIfEmpty()
                        join b in _db.ItemBatches on s.ItemBatchId equals b.ItemBatchId into batches
                        from b in batches.DefaultIfEmpty()
                        join w in _db.Warehouses on s.WarehouseId equals w.WarehouseId into warehouses
                        from w in warehouses.DefaultIfEmpty()
                        join rm in _db.StockMovements on s.ReceivedMovementId equals (long?)rm.StockMovementId into recMoves
                        from rm in recMoves.DefaultIfEmpty()
                        join im in _db.StockMovements on s.IssuedMovementId equals (long?)im.StockMovementId into issMoves
                        from im in issMoves.DefaultIfEmpty()
                        where rm == null || rm.MovementDate <= end
                        select new
                        {
                            s.ItemSerialId,
                            s.SerialNumber,
                            s.HallmarkNumber,
                            s.Status,
                            i.ItemId,
                            i.ItemCode,
                            i.ItemName,
                            CategoryName = c != null ? c.CategoryName : null,
                            UnitCode = u != null ? u.UomCode : null,
                            CostingMethod = i.CostingType.ToString(),
                            BatchNumber = b != null ? b.BatchNumber : null,
                            ExpiryDate = b != null ? b.ExpiryDate : null,
                            WarehouseName = w != null ? w.WarehouseName : "—",

                            ReceivedMovementId = rm != null ? (long?)rm.StockMovementId : null,
                            ReceivedDate = rm != null ? (DateOnly?)rm.MovementDate : null,
                            ReceivedMovementType = rm != null ? rm.MovementType.ToString() : null,
                            ReceivedSourceType = rm != null ? rm.SourceType : null,
                            ReceivedSourceId = rm != null ? rm.SourceId : null,
                            ReceivedWarehouseName = w != null ? w.WarehouseName : "—",
                            ReceivedNotes = rm != null ? rm.Notes : null,

                            IssuedMovementId = im != null ? (long?)im.StockMovementId : null,
                            IssuedDate = im != null ? (DateOnly?)im.MovementDate : null,
                            IssuedMovementType = im != null ? im.MovementType.ToString() : null,
                            IssuedSourceType = im != null ? im.SourceType : null,
                            IssuedSourceId = im != null ? im.SourceId : null,
                            IssuedNotes = im != null ? im.Notes : null,
                        };

        var allItems = await baseQuery.ToListAsync(ct);

        // Pre-fetch document lookups
        List<long> recGrnIds = allItems.Where(x => x.ReceivedSourceType == "GRN" && x.ReceivedSourceId != null).Select(x => x.ReceivedSourceId!.Value).Distinct().ToList();
        List<long> recBillIds = allItems.Where(x => x.ReceivedSourceType == "BIL" && x.ReceivedSourceId != null).Select(x => x.ReceivedSourceId!.Value).Distinct().ToList();

        List<long> issInvIds = allItems.Where(x => (x.IssuedSourceType == "INV" || x.IssuedSourceType == "POS") && x.IssuedSourceId != null).Select(x => x.IssuedSourceId!.Value).Distinct().ToList();
        List<long> issDlcIds = allItems.Where(x => (x.IssuedSourceType == "DLC" || x.IssuedSourceType == "DEL") && x.IssuedSourceId != null).Select(x => x.IssuedSourceId!.Value).Distinct().ToList();

        Dictionary<long, (string DocNo, long ContactId)> recGrns = recGrnIds.Count > 0
            ? await _db.GoodsReceipts.Where(g => recGrnIds.Contains(g.GoodsReceiptId)).ToDictionaryAsync(g => g.GoodsReceiptId, g => (g.DocumentNo, g.ContactId), ct)
            : [];

        Dictionary<long, (string DocNo, long ContactId)> recBills = recBillIds.Count > 0
            ? await _db.Bills.Where(b => recBillIds.Contains(b.BillId)).ToDictionaryAsync(b => b.BillId, b => (b.VendorBillNo ?? b.DocumentNo, b.ContactId), ct)
            : [];

        Dictionary<long, (string DocNo, long ContactId)> issInvoices = issInvIds.Count > 0
            ? await _db.Invoices.Where(i => issInvIds.Contains(i.InvoiceId)).ToDictionaryAsync(i => i.InvoiceId, i => (i.DocumentNo, i.ContactId), ct)
            : [];

        Dictionary<long, (string DocNo, long ContactId)> issChallans = issDlcIds.Count > 0
            ? await _db.DeliveryChallans.Where(d => issDlcIds.Contains(d.DeliveryChallanId)).ToDictionaryAsync(d => d.DeliveryChallanId, d => (d.DocumentNo, d.ContactId), ct)
            : [];

        // Collect all contact IDs
        HashSet<long> contactIds = [];
        foreach (var (_, cid) in recGrns.Values) contactIds.Add(cid);
        foreach (var (_, cid) in recBills.Values) contactIds.Add(cid);
        foreach (var (_, cid) in issInvoices.Values) contactIds.Add(cid);
        foreach (var (_, cid) in issChallans.Values) contactIds.Add(cid);

        Dictionary<long, string> contactNames = contactIds.Count > 0
            ? await _db.Contacts.Where(c => contactIds.Contains(c.ContactId)).ToDictionaryAsync(c => c.ContactId, c => c.DisplayName, ct)
            : [];

        // Build composite groups with opening, movements in period, and closing
        List<SerialTrackingDetailGroupDto> builtGroups = [];

        foreach (var item in allItems)
        {
            // Opening balance: received before start date, and not issued before start date
            bool receivedBeforeStart = item.ReceivedDate != null && item.ReceivedDate < start;
            bool issuedBeforeStart = item.IssuedDate != null && item.IssuedDate < start;
            decimal openingQty = (receivedBeforeStart && !issuedBeforeStart) ? 1m : 0m;

            // In period movements
            bool receivedInPeriod = item.ReceivedDate != null && item.ReceivedDate >= start && item.ReceivedDate <= end;
            bool issuedInPeriod = item.IssuedDate != null && item.IssuedDate >= start && item.IssuedDate <= end;

            decimal totalIn = receivedInPeriod ? 1m : 0m;
            decimal totalOut = issuedInPeriod ? 1m : 0m;
            decimal closingQty = openingQty + totalIn - totalOut;

            // Include if there was opening stock, or any movement in period
            if (openingQty == 0m && totalIn == 0m && totalOut == 0m)
            {
                continue;
            }

            // Resolve received doc and contact
            string? recDocNo = null;
            long? recContactId = null;
            if (item.ReceivedSourceType == "GRN" && item.ReceivedSourceId is long grid && recGrns.TryGetValue(grid, out var grn))
            {
                recDocNo = grn.DocNo;
                recContactId = grn.ContactId;
            }
            else if (item.ReceivedSourceType == "BIL" && item.ReceivedSourceId is long bid && recBills.TryGetValue(bid, out var bill))
            {
                recDocNo = bill.DocNo;
                recContactId = bill.ContactId;
            }
            else if (item.ReceivedSourceId != null)
            {
                recDocNo = $"{item.ReceivedSourceType}-{item.ReceivedSourceId}";
            }

            string? recContactName = recContactId != null && contactNames.TryGetValue(recContactId.Value, out var rcName) ? rcName : null;

            // Resolve issued doc and contact
            string? issDocNo = null;
            long? issContactId = null;
            if ((item.IssuedSourceType == "INV" || item.IssuedSourceType == "POS") && item.IssuedSourceId is long inid && issInvoices.TryGetValue(inid, out var inv))
            {
                issDocNo = inv.DocNo;
                issContactId = inv.ContactId;
            }
            else if ((item.IssuedSourceType == "DLC" || item.IssuedSourceType == "DEL") && item.IssuedSourceId is long dcid && issChallans.TryGetValue(dcid, out var dlc))
            {
                issDocNo = dlc.DocNo;
                issContactId = dlc.ContactId;
            }
            else if (item.IssuedSourceId != null)
            {
                issDocNo = $"{item.IssuedSourceType}-{item.IssuedSourceId}";
            }

            string? issContactName = issContactId != null && contactNames.TryGetValue(issContactId.Value, out var icName) ? icName : null;

            // If contact filter is supplied, check if serial involved this contact
            if (request.ContactId is long reqContactId)
            {
                bool matchesContact = (recContactId == reqContactId && receivedInPeriod) ||
                                     (issContactId == reqContactId && issuedInPeriod);
                if (!matchesContact)
                {
                    continue;
                }
            }

            // Determine status as at end date
            string status = closingQty > 0m ? "InStock" : item.Status.ToString();

            var group = new SerialTrackingDetailGroupDto
            {
                ItemSerialId = item.ItemSerialId,
                SerialNumber = item.SerialNumber,
                HallmarkNumber = item.HallmarkNumber,
                ItemId = item.ItemId,
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                CategoryName = item.CategoryName,
                UnitCode = item.UnitCode,
                CostingMethod = item.CostingMethod,
                BatchNumber = item.BatchNumber,
                ExpiryDate = item.ExpiryDate,
                WarehouseName = item.WarehouseName,
                Status = status,
                OpeningQuantity = openingQty,
                TotalIn = totalIn,
                TotalOut = totalOut,
                ClosingQuantity = closingQty,
                Movements = []
            };

            decimal running = openingQty;

            if (receivedInPeriod && item.ReceivedMovementId != null)
            {
                running += 1m;
                group.Movements.Add(new SerialTrackingDetailMovementDto
                {
                    StockMovementId = item.ReceivedMovementId.Value,
                    MovementDate = item.ReceivedDate!.Value,
                    MovementType = item.ReceivedMovementType ?? "Purchase",
                    SourceType = item.ReceivedSourceType,
                    SourceId = item.ReceivedSourceId,
                    DocumentNumber = recDocNo,
                    ContactName = recContactName,
                    WarehouseName = item.ReceivedWarehouseName,
                    Direction = "In",
                    QuantityIn = 1m,
                    QuantityOut = 0m,
                    RunningBalance = running,
                    Notes = item.ReceivedNotes
                });
            }

            if (issuedInPeriod && item.IssuedMovementId != null)
            {
                running -= 1m;
                group.Movements.Add(new SerialTrackingDetailMovementDto
                {
                    StockMovementId = item.IssuedMovementId.Value,
                    MovementDate = item.IssuedDate!.Value,
                    MovementType = item.IssuedMovementType ?? "Sale",
                    SourceType = item.IssuedSourceType,
                    SourceId = item.IssuedSourceId,
                    DocumentNumber = issDocNo,
                    ContactName = issContactName,
                    WarehouseName = item.WarehouseName,
                    Direction = "Out",
                    QuantityIn = 0m,
                    QuantityOut = 1m,
                    RunningBalance = running,
                    Notes = item.IssuedNotes
                });
            }

            group.Movements = group.Movements.OrderBy(m => m.MovementDate).ThenBy(m => m.StockMovementId).ToList();
            builtGroups.Add(group);
        }

        // Totals
        var totals = new SerialTrackingDetailTotalsDto
        {
            TotalSerials = builtGroups.Count,
            TotalOpeningQuantity = builtGroups.Sum(g => g.OpeningQuantity),
            TotalInQuantity = builtGroups.Sum(g => g.TotalIn),
            TotalOutQuantity = builtGroups.Sum(g => g.TotalOut),
            TotalClosingQuantity = builtGroups.Sum(g => g.ClosingQuantity),
            TotalMovements = builtGroups.Sum(g => g.Movements.Count)
        };

        var pagedGroups = builtGroups
            .OrderBy(g => g.ItemName)
            .ThenBy(g => g.SerialNumber)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new SerialTrackingDetailReportResultDto
        {
            FromDate = start,
            ToDate = end,
            Currency = currency,
            Serials = pagedGroups,
            Totals = totals,
            TotalItems = builtGroups.Count,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }
}
