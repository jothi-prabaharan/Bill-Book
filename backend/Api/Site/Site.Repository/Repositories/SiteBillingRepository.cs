using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Site.Entity.Enums;
using Site.Entity.TableEntities;
using Site.Repository.Clients;

namespace Site.Repository.Repositories;

public sealed record UnbilledLaborItemDto(
    long AttendanceId,
    long AssignmentId,
    long LaborerId,
    string LaborerName,
    long TradeId,
    string TradeName,
    DateOnly WorkDate,
    decimal DayFraction,
    decimal OtHoursApproved,
    decimal DaySellSnapshot,
    decimal OtSellSnapshot,
    decimal TotalSellAmount,
    decimal? DayCostSnapshot = null,
    decimal? DayMarginSnapshot = null,
    decimal? OtCostSnapshot = null,
    decimal? OtMarginSnapshot = null);

public sealed record UnbilledMaterialItemDto(
    long LineId,
    long DocumentId,
    string DocumentNo,
    long ItemId,
    decimal UsedQuantity,
    decimal BilledQuantity,
    decimal UnbilledQuantity,
    decimal SellingPrice,
    decimal TotalSellAmount,
    decimal? UnitCost = null,
    decimal? MarginValue = null);

public sealed record BillingWorksheetDto(
    long SiteId,
    string SiteName,
    long ContactId,
    long? WarehouseId,
    List<UnbilledLaborItemDto> LaborItems,
    List<UnbilledMaterialItemDto> MaterialItems);

public sealed record MaterialLineToBillDto(
    long LineId,
    decimal QuantityToBill);

public sealed record CreateSiteBillRequest(
    long SiteId,
    DateOnly BillingDate,
    DateOnly? DueDate = null,
    List<long>? AttendanceIdsToBill = null,
    List<MaterialLineToBillDto>? MaterialLinesToBill = null,
    string? Notes = null);

public sealed record SiteBillDto(
    long SiteBillingInvoiceId,
    string BillingNumber,
    long SiteId,
    DateOnly BillingDate,
    long? SalesInvoiceId,
    decimal TotalLaborAmount,
    decimal TotalMaterialAmount,
    decimal TotalBillableAmount,
    List<SiteBillLineDto> Lines);

public sealed record SiteBillLineDto(
    long SiteBillingInvoiceLineId,
    BillingLineType LineType,
    long? ReferenceId,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal TotalAmount);

/// <summary>
/// Site billing worksheet and partial invoice generation (TK-175).
/// Claims unbilled approved attendance and unbilled used materials with guarded updates,
/// and creates/posts real sales invoices via internal Sales API.
/// Privacy rule: Selling prices only cross into Sales (no cost or margin fields).
/// </summary>
public sealed class SiteBillingRepository
{
    private readonly SiteDbContext _db;
    private readonly TenantContext _tenant;
    private readonly ISalesInvoiceClient _salesClient;
    private readonly INumberGenerator _numbering;

    public SiteBillingRepository(
        SiteDbContext db,
        TenantContext tenant,
        ISalesInvoiceClient salesClient,
        INumberGenerator numbering)
    {
        _db = db;
        _tenant = tenant;
        _salesClient = salesClient;
        _numbering = numbering;
    }

    public async Task<BillingWorksheetDto> GetWorksheetAsync(
        long siteId,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        long? tradeId = null,
        bool includeCostAndMargin = false,
        CancellationToken ct = default)
    {
        var site = await _db.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.SiteId == siteId, ct)
            ?? throw new ArgumentException($"Site {siteId} not found.");

        // 1. Approved unbilled attendance
        var attendanceQuery = _db.SiteAttendances.AsNoTracking()
            .Where(a => a.SiteId == siteId
                && a.Status == AttendanceStatus.Approved
                && a.SiteBillLineId == null);

        if (fromDate.HasValue) attendanceQuery = attendanceQuery.Where(a => a.WorkDate >= fromDate.Value);
        if (toDate.HasValue) attendanceQuery = attendanceQuery.Where(a => a.WorkDate <= toDate.Value);

        var attendances = await attendanceQuery.OrderBy(a => a.WorkDate).ToListAsync(ct);

        var laborerIds = attendances.Select(a => a.LaborerId).Distinct().ToList();
        var laborers = await _db.Laborers.AsNoTracking()
            .Where(l => laborerIds.Contains(l.LaborerId))
            .ToDictionaryAsync(l => l.LaborerId, ct);

        var tradeIds = laborers.Values.Select(l => l.TradeId).Distinct().ToList();
        var trades = await _db.Trades.AsNoTracking()
            .Where(t => tradeIds.Contains(t.TradeId))
            .ToDictionaryAsync(t => t.TradeId, ct);

        var laborItems = new List<UnbilledLaborItemDto>();
        foreach (var att in attendances)
        {
            if (laborers.TryGetValue(att.LaborerId, out var laborer))
            {
                if (tradeId.HasValue && laborer.TradeId != tradeId.Value) continue;

                trades.TryGetValue(laborer.TradeId, out var trade);
                decimal totalSell = att.DaySellSnapshot + att.OtSellSnapshot;

                laborItems.Add(new UnbilledLaborItemDto(
                    att.AttendanceId,
                    att.AssignmentId,
                    att.LaborerId,
                    laborer.FullName,
                    laborer.TradeId,
                    trade?.TradeName ?? string.Empty,
                    att.WorkDate,
                    att.DayFraction,
                    att.OtHoursApproved,
                    att.DaySellSnapshot,
                    att.OtSellSnapshot,
                    totalSell,
                    includeCostAndMargin ? att.DayCostSnapshot : null,
                    includeCostAndMargin ? att.DayMarginSnapshot : null,
                    includeCostAndMargin ? att.OtCostSnapshot : null,
                    includeCostAndMargin ? att.OtMarginSnapshot : null));
            }
        }

        // 2. Approved unbilled materials from usage documents
        var usageDocs = await _db.SiteStockDocuments.AsNoTracking()
            .Where(d => d.SiteId == siteId
                && d.DocumentType == SiteStockDocumentType.Usage
                && d.Status == SiteStockDocumentStatus.Approved)
            .ToDictionaryAsync(d => d.DocumentId, ct);

        var usageDocIds = usageDocs.Keys.ToList();
        var stockLines = await _db.SiteStockLines.AsNoTracking()
            .Where(l => usageDocIds.Contains(l.DocumentId) && l.BilledQuantity < l.Quantity)
            .ToListAsync(ct);

        var materialItems = new List<UnbilledMaterialItemDto>();
        foreach (var line in stockLines)
        {
            decimal unbilledQty = line.Quantity - line.BilledQuantity;
            if (unbilledQty <= 0) continue;

            decimal sellPrice = line.SellingPrice ?? 0m;
            decimal totalSell = unbilledQty * sellPrice;
            usageDocs.TryGetValue(line.DocumentId, out var doc);

            materialItems.Add(new UnbilledMaterialItemDto(
                line.LineId,
                line.DocumentId,
                doc?.DocumentNo ?? string.Empty,
                line.ItemId,
                line.Quantity,
                line.BilledQuantity,
                unbilledQty,
                sellPrice,
                totalSell,
                includeCostAndMargin ? line.UnitCost : null,
                includeCostAndMargin ? line.MarginValue : null));
        }

        return new BillingWorksheetDto(
            site.SiteId,
            site.SiteName,
            site.ContactId,
            site.WarehouseId,
            laborItems,
            materialItems);
    }

    public async Task<SiteBillDto> CreateAndPostBillAsync(CreateSiteBillRequest request, CancellationToken ct = default)
    {
        var attendanceIds = request.AttendanceIdsToBill ?? new List<long>();
        var materialLines = request.MaterialLinesToBill ?? new List<MaterialLineToBillDto>();

        if (attendanceIds.Count == 0 && materialLines.Count == 0)
        {
            throw new ArgumentException("Site bill must contain at least one attendance row or material line.");
        }

        var site = await _db.Sites.FirstOrDefaultAsync(s => s.SiteId == request.SiteId, ct)
            ?? throw new ArgumentException($"Site {request.SiteId} not found.");

        // Guarded check on attendance: must not already be claimed
        var attendances = await _db.SiteAttendances
            .Where(a => attendanceIds.Contains(a.AttendanceId))
            .ToListAsync(ct);

        foreach (var att in attendances)
        {
            if (att.SiteBillLineId.HasValue)
            {
                throw new InvalidOperationException($"Attendance {att.AttendanceId} for date {att.WorkDate} has already been billed.");
            }
            if (att.Status != AttendanceStatus.Approved)
            {
                throw new InvalidOperationException($"Attendance {att.AttendanceId} is not approved.");
            }
        }

        // Guarded check on material lines: BilledQuantity + q <= Quantity
        var lineIds = materialLines.Select(m => m.LineId).ToList();
        var stockLines = await _db.SiteStockLines
            .Where(l => lineIds.Contains(l.LineId))
            .ToListAsync(ct);

        foreach (var mat in materialLines)
        {
            var line = stockLines.FirstOrDefault(l => l.LineId == mat.LineId)
                ?? throw new ArgumentException($"Stock line {mat.LineId} not found.");

            if (line.BilledQuantity + mat.QuantityToBill > line.Quantity)
            {
                throw new InvalidOperationException(
                    $"Cannot bill {mat.QuantityToBill} on line {mat.LineId}. Only {line.Quantity - line.BilledQuantity} remaining unbilled.");
            }
        }

        // Allocate bill number from SBL series
        string billNo;
        try
        {
            var allocation = await _numbering.NextAsync("SBL", request.BillingDate, ct);
            billNo = allocation.Code;
        }
        catch
        {
            billNo = $"SBL-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}";
        }

        // Prepare Sales invoice payload (PRIVACY RULE: SELLING PRICE ONLY, NO COST/MARGIN)
        var salesLines = new List<SiteBillingInvoiceLinePayload>();
        var laborerIds = attendances.Select(a => a.LaborerId).Distinct().ToList();
        var laborers = await _db.Laborers.AsNoTracking()
            .Where(l => laborerIds.Contains(l.LaborerId))
            .ToDictionaryAsync(l => l.LaborerId, ct);

        decimal totalLabor = 0m;
        foreach (var att in attendances)
        {
            laborers.TryGetValue(att.LaborerId, out var laborer);
            string labName = laborer?.FullName ?? $"Laborer #{att.LaborerId}";

            if (att.DayFraction > 0 && att.DaySellSnapshot > 0)
            {
                decimal unitRate = att.DayFraction > 0 ? (att.DaySellSnapshot / att.DayFraction) : att.DaySellSnapshot;
                salesLines.Add(new SiteBillingInvoiceLinePayload
                {
                    Description = $"{labName} - Site Labor ({att.WorkDate:yyyy-MM-dd})",
                    Quantity = att.DayFraction,
                    UnitPrice = Math.Round(unitRate, 2),
                });
                totalLabor += att.DaySellSnapshot;
            }

            if (att.OtHoursApproved > 0 && att.OtSellSnapshot > 0)
            {
                decimal otUnitRate = att.OtHoursApproved > 0 ? (att.OtSellSnapshot / att.OtHoursApproved) : att.OtSellSnapshot;
                salesLines.Add(new SiteBillingInvoiceLinePayload
                {
                    Description = $"{labName} - Site Overtime ({att.WorkDate:yyyy-MM-dd})",
                    Quantity = att.OtHoursApproved,
                    UnitPrice = Math.Round(otUnitRate, 2),
                });
                totalLabor += att.OtSellSnapshot;
            }
        }

        decimal totalMaterial = 0m;
        foreach (var mat in materialLines)
        {
            var line = stockLines.First(l => l.LineId == mat.LineId);
            decimal unitPrice = line.SellingPrice ?? 0m;
            decimal lineTotal = mat.QuantityToBill * unitPrice;

            salesLines.Add(new SiteBillingInvoiceLinePayload
            {
                ItemId = line.ItemId,
                Description = $"Site Material - Item #{line.ItemId}",
                WarehouseId = site.WarehouseId,
                Quantity = mat.QuantityToBill,
                UnitPrice = unitPrice,
            });
            totalMaterial += lineTotal;
        }

        // Post through Sales internal API
        var salesPayload = new CreateSiteInvoicePayload
        {
            CustomerId = _tenant.CustomerId.GetValueOrDefault(),
            OrgId = _tenant.OrgId.GetValueOrDefault(),
            ContactId = site.ContactId,
            DocumentDate = request.BillingDate,
            DueDate = request.DueDate ?? request.BillingDate,
            Notes = $"Site Billing {billNo} for site: {site.SiteName}. {request.Notes}".Trim(),
            Lines = salesLines,
        };

        var salesResult = await _salesClient.CreateAndPostInvoiceAsync(salesPayload, ct);
        if (!salesResult.Success)
        {
            throw new InvalidOperationException($"Sales invoice creation failed: {salesResult.Error}");
        }

        // Record SiteBillingInvoice in sit schema
        var bill = new SiteBillingInvoice
        {
            BillingNumber = billNo,
            SiteId = request.SiteId,
            BillingDate = request.BillingDate,
            SalesInvoiceId = salesResult.InvoiceId,
            TotalLaborAmount = totalLabor,
            TotalMaterialAmount = totalMaterial,
            TotalBillableAmount = totalLabor + totalMaterial,
        };
        _db.SiteBillingInvoices.Add(bill);
        await _db.SaveChangesAsync(ct);

        var createdBillLines = new List<SiteBillLineDto>();

        // Claim attendance rows
        var assignments = await _db.SiteAssignments
            .Where(a => attendances.Select(x => x.AssignmentId).Contains(a.SiteAssignmentId))
            .ToDictionaryAsync(a => a.SiteAssignmentId, ct);

        foreach (var att in attendances)
        {
            decimal amt = att.DaySellSnapshot + att.OtSellSnapshot;
            var billLine = new SiteBillingInvoiceLine
            {
                SiteBillingInvoiceId = bill.SiteBillingInvoiceId,
                LineType = BillingLineType.Labor,
                ReferenceId = att.AttendanceId,
                Description = $"Laborer #{att.LaborerId} - {att.WorkDate:yyyy-MM-dd}",
                Quantity = att.DayFraction,
                UnitPrice = att.DayFraction > 0 ? (amt / att.DayFraction) : amt,
                TotalAmount = amt,
            };
            _db.SiteBillingInvoiceLines.Add(billLine);
            await _db.SaveChangesAsync(ct);

            // Guarded claim
            att.SiteBillLineId = billLine.SiteBillingInvoiceLineId;

            // Increment assignment counters
            if (assignments.TryGetValue(att.AssignmentId, out var asgn))
            {
                asgn.BilledDays += att.DayFraction;
                asgn.BilledOtHours += att.OtHoursApproved;
            }

            createdBillLines.Add(new SiteBillLineDto(
                billLine.SiteBillingInvoiceLineId,
                billLine.LineType,
                billLine.ReferenceId,
                billLine.Description,
                billLine.Quantity,
                billLine.UnitPrice,
                billLine.TotalAmount));
        }

        // Claim material lines and update balances
        foreach (var mat in materialLines)
        {
            var line = stockLines.First(l => l.LineId == mat.LineId);
            decimal unitPrice = line.SellingPrice ?? 0m;
            decimal lineTotal = mat.QuantityToBill * unitPrice;

            var billLine = new SiteBillingInvoiceLine
            {
                SiteBillingInvoiceId = bill.SiteBillingInvoiceId,
                LineType = BillingLineType.Material,
                ReferenceId = line.LineId,
                Description = $"Material Item #{line.ItemId}",
                Quantity = mat.QuantityToBill,
                UnitPrice = unitPrice,
                TotalAmount = lineTotal,
            };
            _db.SiteBillingInvoiceLines.Add(billLine);
            await _db.SaveChangesAsync(ct);

            line.BilledQuantity += mat.QuantityToBill;

            var balance = await _db.SiteStockBalances.FirstOrDefaultAsync(
                b => b.SiteId == request.SiteId && b.ItemId == line.ItemId, ct);

            if (balance is not null)
            {
                balance.UnbilledUsedQty = Math.Max(0m, balance.UnbilledUsedQty - mat.QuantityToBill);
                balance.BilledQty += mat.QuantityToBill;
            }

            createdBillLines.Add(new SiteBillLineDto(
                billLine.SiteBillingInvoiceLineId,
                billLine.LineType,
                billLine.ReferenceId,
                billLine.Description,
                billLine.Quantity,
                billLine.UnitPrice,
                billLine.TotalAmount));
        }

        await _db.SaveChangesAsync(ct);

        return new SiteBillDto(
            bill.SiteBillingInvoiceId,
            bill.BillingNumber,
            bill.SiteId,
            bill.BillingDate,
            bill.SalesInvoiceId,
            bill.TotalLaborAmount,
            bill.TotalMaterialAmount,
            bill.TotalBillableAmount,
            createdBillLines);
    }
}
