using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Serial Tracking Status Report (TK-249): dedicated endpoint and page for every serial number as at a date.
/// Shows serial number, item details, status (InStock, Sold, Returned, Scrapped), warehouse, received date & document,
/// and sold date & customer.
/// </summary>
public sealed class SerialTrackingStatusReportRepository
{
    private readonly ReportingDbContext _db;

    public SerialTrackingStatusReportRepository(ReportingDbContext db) => _db = db;

    /// <summary>Validation refusals for required fields.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(SerialTrackingStatusReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.AsOfDate == null)
        {
            refusals.Add((nameof(SerialTrackingStatusReportRequest.AsOfDate), "As at date is required."));
        }

        return refusals;
    }

    /// <summary>Validation for items or warehouses not on this branch.</summary>
    public async Task<IReadOnlyList<(string Field, string Message)>> LookupRefusalsAsync(SerialTrackingStatusReportRequest request, CancellationToken ct)
    {
        List<(string, string)> refusals = [];

        if (request.ItemId is long itemId && !await _db.Items.AnyAsync(i => i.ItemId == itemId, ct))
        {
            refusals.Add((nameof(SerialTrackingStatusReportRequest.ItemId), "That item is not on this branch."));
        }

        if (request.WarehouseId is long warehouseId && !await _db.Warehouses.AnyAsync(w => w.WarehouseId == warehouseId, ct))
        {
            refusals.Add((nameof(SerialTrackingStatusReportRequest.WarehouseId), "That warehouse is not on this branch."));
        }

        return refusals;
    }

    /// <summary>Items that have serial records.</summary>
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

    /// <summary>Warehouses on this branch.</summary>
    public Task<List<SalesAnalysisWarehouseChoiceDto>> WarehousesAsync(CancellationToken ct) =>
        SalesAnalysisReportRepository.WarehouseChoices(_db.Warehouses).ToListAsync(ct);

    public async Task<SerialTrackingStatusReportResultDto> RunAsync(SerialTrackingStatusReportRequest request, CancellationToken ct)
    {
        DateOnly asOf = request.AsOfDate!.Value;

        var serials = _db.ItemSerials.AsQueryable();

        if (request.ItemId is long itemId)
        {
            serials = serials.Where(s => s.ItemId == itemId);
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
                        join b in _db.ItemBatches on s.ItemBatchId equals b.ItemBatchId into batches
                        from b in batches.DefaultIfEmpty()
                        join w in _db.Warehouses on s.WarehouseId equals w.WarehouseId into warehouses
                        from w in warehouses.DefaultIfEmpty()
                        join rm in _db.StockMovements on s.ReceivedMovementId equals (long?)rm.StockMovementId into recMoves
                        from rm in recMoves.DefaultIfEmpty()
                        join im in _db.StockMovements on s.IssuedMovementId equals (long?)im.StockMovementId into issMoves
                        from im in issMoves.DefaultIfEmpty()
                        where rm == null || rm.MovementDate <= asOf
                        let issuedByAsOf = im != null && im.MovementDate <= asOf
                        let effectiveStatus = issuedByAsOf ? s.Status : SerialStatus.InStock
                        select new
                        {
                            s.ItemSerialId,
                            s.SerialNumber,
                            s.HallmarkNumber,
                            i.ItemId,
                            i.ItemCode,
                            i.ItemName,
                            CategoryName = c != null ? c.CategoryName : null,
                            CostingMethod = i.CostingType.ToString(),
                            BatchNumber = b != null ? b.BatchNumber : null,
                            ExpiryDate = b != null ? b.ExpiryDate : null,
                            WarehouseName = w != null ? w.WarehouseName : "—",
                            EffectiveStatus = effectiveStatus,
                            IssuedByAsOf = issuedByAsOf,
                            ReceivedMovementDate = rm != null ? (DateOnly?)rm.MovementDate : null,
                            ReceivedSourceType = rm != null ? rm.SourceType : null,
                            ReceivedSourceId = rm != null ? rm.SourceId : null,
                            IssuedMovementDate = issuedByAsOf && im != null ? (DateOnly?)im.MovementDate : null,
                            IssuedSourceType = issuedByAsOf && im != null ? im.SourceType : null,
                            IssuedSourceId = issuedByAsOf && im != null ? im.SourceId : null,
                        };

        var filteredQuery = request.Status is SerialStatus reqStatus
            ? baseQuery.Where(x => x.EffectiveStatus == reqStatus)
            : baseQuery;

        var totals = await filteredQuery
            .GroupBy(_ => 1)
            .Select(g => new SerialTrackingStatusTotalsDto
            {
                TotalSerials = g.Count(),
                InStockCount = g.Count(x => x.EffectiveStatus == SerialStatus.InStock),
                SoldCount = g.Count(x => x.EffectiveStatus == SerialStatus.Sold),
                ReturnedCount = g.Count(x => x.EffectiveStatus == SerialStatus.Returned),
                ScrappedCount = g.Count(x => x.EffectiveStatus == SerialStatus.Scrapped),
            })
            .FirstOrDefaultAsync(ct) ?? new SerialTrackingStatusTotalsDto();

        int totalCount = await filteredQuery.CountAsync(ct);

        var pageItems = await filteredQuery
            .OrderBy(x => x.ItemName)
            .ThenBy(x => x.SerialNumber)
            .ThenBy(x => x.ItemSerialId)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        // Fetch documents and contacts for page items
        List<long> recGrnIds = pageItems.Where(x => x.ReceivedSourceType == "GRN" && x.ReceivedSourceId != null).Select(x => x.ReceivedSourceId!.Value).Distinct().ToList();
        List<long> recBillIds = pageItems.Where(x => x.ReceivedSourceType == "BIL" && x.ReceivedSourceId != null).Select(x => x.ReceivedSourceId!.Value).Distinct().ToList();

        List<long> issInvIds = pageItems.Where(x => (x.IssuedSourceType == "INV" || x.IssuedSourceType == "POS") && x.IssuedSourceId != null).Select(x => x.IssuedSourceId!.Value).Distinct().ToList();
        List<long> issDlcIds = pageItems.Where(x => (x.IssuedSourceType == "DLC" || x.IssuedSourceType == "DEL") && x.IssuedSourceId != null).Select(x => x.IssuedSourceId!.Value).Distinct().ToList();

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

        HashSet<long> contactIds = [
            .. recGrns.Values.Select(v => v.ContactId),
            .. recBills.Values.Select(v => v.ContactId),
            .. issInvoices.Values.Select(v => v.ContactId),
            .. issChallans.Values.Select(v => v.ContactId)
        ];

        Dictionary<long, string> contacts = contactIds.Count > 0
            ? await _db.Contacts.Where(c => contactIds.Contains(c.ContactId)).ToDictionaryAsync(c => c.ContactId, c => c.DisplayName, ct)
            : [];

        List<SerialTrackingStatusItemDto> itemsResult = pageItems.Select(x =>
        {
            string? recDocNo = null;
            string? recContact = null;
            if (x.ReceivedSourceId is long rsid)
            {
                if (x.ReceivedSourceType == "GRN" && recGrns.TryGetValue(rsid, out var grn))
                {
                    recDocNo = grn.DocNo;
                    contacts.TryGetValue(grn.ContactId, out recContact);
                }
                else if (x.ReceivedSourceType == "BIL" && recBills.TryGetValue(rsid, out var bil))
                {
                    recDocNo = bil.DocNo;
                    contacts.TryGetValue(bil.ContactId, out recContact);
                }
            }

            string? issDocNo = null;
            string? issContact = null;
            if (x.IssuedSourceId is long isid)
            {
                if ((x.IssuedSourceType == "INV" || x.IssuedSourceType == "POS") && issInvoices.TryGetValue(isid, out var inv))
                {
                    issDocNo = inv.DocNo;
                    contacts.TryGetValue(inv.ContactId, out issContact);
                }
                else if ((x.IssuedSourceType == "DLC" || x.IssuedSourceType == "DEL") && issChallans.TryGetValue(isid, out var dlc))
                {
                    issDocNo = dlc.DocNo;
                    contacts.TryGetValue(dlc.ContactId, out issContact);
                }
            }

            return new SerialTrackingStatusItemDto
            {
                ItemSerialId = x.ItemSerialId,
                SerialNumber = x.SerialNumber,
                ItemId = x.ItemId,
                ItemCode = x.ItemCode,
                ItemName = x.ItemName,
                CategoryName = x.CategoryName,
                CostingMethod = x.CostingMethod,
                HallmarkNumber = x.HallmarkNumber,
                BatchNumber = x.BatchNumber,
                ExpiryDate = x.ExpiryDate,
                Status = x.EffectiveStatus.ToString(),
                WarehouseName = x.WarehouseName,
                ReceivedDate = x.ReceivedMovementDate,
                ReceivedDocumentNumber = recDocNo ?? (x.ReceivedSourceType != null && x.ReceivedSourceId != null ? $"{x.ReceivedSourceType}-{x.ReceivedSourceId}" : null),
                ReceivedFromContactName = recContact,
                SoldDate = x.IssuedMovementDate,
                SoldDocumentNumber = issDocNo ?? (x.IssuedSourceType != null && x.IssuedSourceId != null ? $"{x.IssuedSourceType}-{x.IssuedSourceId}" : null),
                SoldToContactName = issContact,
                IsInStock = x.EffectiveStatus == SerialStatus.InStock || x.EffectiveStatus == SerialStatus.Returned
            };
        }).ToList();

        return new SerialTrackingStatusReportResultDto
        {
            AsOfDate = asOf,
            Items = itemsResult,
            Totals = totals,
            TotalItems = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }
}
