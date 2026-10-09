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

public sealed record DispatchStockLineDto(
    long ItemId,
    decimal Quantity,
    decimal? UnitCost = null,
    decimal? MarginValue = null,
    decimal? SellingPrice = null);

public sealed record DispatchStockRequest(
    long SiteId,
    long SourceWarehouseId,
    DateOnly DocumentDate,
    List<DispatchStockLineDto> Lines,
    string? Remarks);

public sealed record ReturnStockLineDto(
    long ItemId,
    decimal Quantity,
    decimal? UnitCost = null);

public sealed record ReturnStockRequest(
    long SiteId,
    long DestinationWarehouseId,
    DateOnly DocumentDate,
    List<ReturnStockLineDto> Lines,
    string? Remarks);

public sealed record SiteStockBalanceDto(
    long BalanceId,
    long SiteId,
    long ItemId,
    decimal DispatchedQty,
    decimal ReturnedQty,
    decimal UsedQty,
    decimal OnSiteQty,
    decimal UnbilledUsedQty,
    decimal BilledQty);

public sealed record SiteStockDocumentDto(
    long DocumentId,
    SiteStockDocumentType DocumentType,
    string DocumentNo,
    long SiteId,
    string SiteName,
    long? SourceWarehouseId,
    DateOnly DocumentDate,
    SiteStockDocumentStatus Status,
    long? ApprovalRequestId,
    string? Remarks,
    List<SiteStockLineDto> Lines);

public sealed record SiteStockLineDto(
    long LineId,
    long ItemId,
    decimal Quantity,
    decimal? UnitCost,
    decimal? MarginValue,
    decimal? SellingPrice,
    decimal BilledQuantity);

/// <summary>
/// Site stock dispatch and return repository (TK-173).
/// Inter-warehouse stock transfers via Inventory client, guarded balance checks,
/// and live on-site balance tracking in sit.SiteStockBalances.
/// </summary>
public sealed class SiteStockRepository
{
    private readonly SiteDbContext _db;
    private readonly TenantContext _tenant;
    private readonly IInventoryWarehouseClient _warehouseClient;
    private readonly INumberGenerator _numbering;
    private readonly SiteMarginRepository _marginRepo;

    public SiteStockRepository(
        SiteDbContext db,
        TenantContext tenant,
        IInventoryWarehouseClient warehouseClient,
        INumberGenerator numbering,
        SiteMarginRepository marginRepo)
    {
        _db = db;
        _tenant = tenant;
        _warehouseClient = warehouseClient;
        _numbering = numbering;
        _marginRepo = marginRepo;
    }

    public async Task<IReadOnlyList<SiteStockBalanceDto>> ListBalancesAsync(long siteId, CancellationToken ct = default)
    {
        return await _db.SiteStockBalances
            .AsNoTracking()
            .Where(b => b.SiteId == siteId)
            .OrderBy(b => b.ItemId)
            .Select(b => new SiteStockBalanceDto(
                b.BalanceId,
                b.SiteId,
                b.ItemId,
                b.DispatchedQty,
                b.ReturnedQty,
                b.UsedQty,
                b.OnSiteQty,
                b.UnbilledUsedQty,
                b.BilledQty))
            .ToListAsync(ct);
    }

    public async Task<SiteStockBalanceDto?> GetBalanceAsync(long siteId, long itemId, CancellationToken ct = default)
    {
        var b = await _db.SiteStockBalances
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SiteId == siteId && x.ItemId == itemId, ct);

        if (b is null) return null;

        return new SiteStockBalanceDto(
            b.BalanceId,
            b.SiteId,
            b.ItemId,
            b.DispatchedQty,
            b.ReturnedQty,
            b.UsedQty,
            b.OnSiteQty,
            b.UnbilledUsedQty,
            b.BilledQty);
    }

    public async Task<IReadOnlyList<SiteStockDocumentDto>> ListDocumentsAsync(
        long? siteId = null,
        SiteStockDocumentType? type = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        var query = _db.SiteStockDocuments.AsNoTracking().AsQueryable();

        if (siteId.HasValue) query = query.Where(d => d.SiteId == siteId.Value);
        if (type.HasValue) query = query.Where(d => d.DocumentType == type.Value);
        if (fromDate.HasValue) query = query.Where(d => d.DocumentDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(d => d.DocumentDate <= toDate.Value);

        var docs = await query.OrderByDescending(d => d.DocumentDate).ThenByDescending(d => d.DocumentId).ToListAsync(ct);
        var docIds = docs.Select(d => d.DocumentId).ToList();
        var lines = await _db.SiteStockLines.AsNoTracking().Where(l => docIds.Contains(l.DocumentId)).ToListAsync(ct);
        var siteIds = docs.Select(d => d.SiteId).Distinct().ToList();
        var sites = await _db.Sites.AsNoTracking().Where(s => siteIds.Contains(s.SiteId)).ToDictionaryAsync(s => s.SiteId, s => s.SiteName, ct);

        return docs.Select(d => new SiteStockDocumentDto(
            d.DocumentId,
            d.DocumentType,
            d.DocumentNo,
            d.SiteId,
            sites.GetValueOrDefault(d.SiteId, string.Empty),
            d.SourceWarehouseId,
            d.DocumentDate,
            d.Status,
            d.ApprovalRequestId,
            d.Remarks,
            lines.Where(l => l.DocumentId == d.DocumentId).Select(l => new SiteStockLineDto(
                l.LineId,
                l.ItemId,
                l.Quantity,
                l.UnitCost,
                l.MarginValue,
                l.SellingPrice,
                l.BilledQuantity)).ToList()
        )).ToList();
    }

    public async Task<SiteStockDocumentDto?> GetDocumentAsync(long documentId, CancellationToken ct = default)
    {
        var doc = await _db.SiteStockDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.DocumentId == documentId, ct);
        if (doc is null) return null;

        var lines = await _db.SiteStockLines.AsNoTracking().Where(l => l.DocumentId == documentId).ToListAsync(ct);
        var site = await _db.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.SiteId == doc.SiteId, ct);

        return new SiteStockDocumentDto(
            doc.DocumentId,
            doc.DocumentType,
            doc.DocumentNo,
            doc.SiteId,
            site?.SiteName ?? string.Empty,
            doc.SourceWarehouseId,
            doc.DocumentDate,
            doc.Status,
            doc.ApprovalRequestId,
            doc.Remarks,
            lines.Select(l => new SiteStockLineDto(
                l.LineId,
                l.ItemId,
                l.Quantity,
                l.UnitCost,
                l.MarginValue,
                l.SellingPrice,
                l.BilledQuantity)).ToList());
    }

    public async Task<SiteStockDocumentDto> DispatchAsync(DispatchStockRequest request, CancellationToken ct = default)
    {
        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new ArgumentException("Dispatch requires at least one line item.");
        }

        var site = await _db.Sites.FirstOrDefaultAsync(s => s.SiteId == request.SiteId, ct)
            ?? throw new ArgumentException($"Site {request.SiteId} not found.");

        if (!site.WarehouseId.HasValue)
        {
            throw new InvalidOperationException($"Site {site.SiteName} does not have an assigned warehouse in Inventory.");
        }

        long siteWarehouseId = site.WarehouseId.Value;

        // Allocate document number
        string docNo;
        try
        {
            var allocation = await _numbering.NextAsync("SDP", request.DocumentDate, ct);
            docNo = allocation.Code;
        }
        catch
        {
            docNo = $"SDP-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}";
        }

        // Call inter-warehouse transfer for each line
        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0)
            {
                throw new ArgumentException($"Line quantity must be greater than zero for item {line.ItemId}.");
            }

            bool transferred = await _warehouseClient.TransferStockAsync(
                _tenant.CustomerId.GetValueOrDefault(),
                _tenant.OrgId.GetValueOrDefault(),
                request.SourceWarehouseId,
                siteWarehouseId,
                line.ItemId,
                line.Quantity,
                $"Site dispatch {docNo}",
                ct);

            if (!transferred)
            {
                throw new InvalidOperationException($"Stock transfer from warehouse {request.SourceWarehouseId} refused for item {line.ItemId}. Insufficient central stock.");
            }
        }

        var document = new SiteStockDocument
        {
            DocumentType = SiteStockDocumentType.Dispatch,
            DocumentNo = docNo,
            SiteId = request.SiteId,
            SourceWarehouseId = request.SourceWarehouseId,
            DocumentDate = request.DocumentDate,
            Status = SiteStockDocumentStatus.Posted,
            Remarks = request.Remarks,
        };

        _db.SiteStockDocuments.Add(document);
        await _db.SaveChangesAsync(ct);

        var createdLines = new List<SiteStockLineDto>();

        foreach (var line in request.Lines)
        {
            decimal unitCost = line.UnitCost ?? 0m;
            decimal marginValue = line.MarginValue ?? 0m;
            decimal sellingPrice = line.SellingPrice ?? 0m;

            if (unitCost > 0 && sellingPrice == 0)
            {
                var rule = await _marginRepo.ResolveRuleAsync(
                    assignmentId: null,
                    siteId: request.SiteId,
                    tradeId: null,
                    itemId: line.ItemId,
                    appliesTo: MarginAppliesTo.Material,
                    date: request.DocumentDate,
                    ct: ct);

                var marginResult = SiteMarginRepository.Calculate(rule, unitCost);
                marginValue = marginResult.MarginAmount;
                sellingPrice = marginResult.SellingPrice;
            }

            var stockLine = new SiteStockLine
            {
                DocumentId = document.DocumentId,
                ItemId = line.ItemId,
                Quantity = line.Quantity,
                UnitCost = unitCost,
                MarginValue = marginValue,
                SellingPrice = sellingPrice,
                BilledQuantity = 0m,
            };

            _db.SiteStockLines.Add(stockLine);

            // Guarded conditional update on SiteStockBalances
            var balance = await _db.SiteStockBalances.FirstOrDefaultAsync(
                b => b.SiteId == request.SiteId && b.ItemId == line.ItemId, ct);

            if (balance is null)
            {
                balance = new SiteStockBalance
                {
                    SiteId = request.SiteId,
                    ItemId = line.ItemId,
                    DispatchedQty = line.Quantity,
                    ReturnedQty = 0m,
                    UsedQty = 0m,
                    OnSiteQty = line.Quantity,
                    UnbilledUsedQty = 0m,
                    BilledQty = 0m,
                };
                _db.SiteStockBalances.Add(balance);
            }
            else
            {
                balance.DispatchedQty += line.Quantity;
                balance.OnSiteQty += line.Quantity;
            }

            createdLines.Add(new SiteStockLineDto(
                stockLine.LineId,
                stockLine.ItemId,
                stockLine.Quantity,
                stockLine.UnitCost,
                stockLine.MarginValue,
                stockLine.SellingPrice,
                stockLine.BilledQuantity));
        }

        await _db.SaveChangesAsync(ct);

        return new SiteStockDocumentDto(
            document.DocumentId,
            document.DocumentType,
            document.DocumentNo,
            document.SiteId,
            site.SiteName,
            document.SourceWarehouseId,
            document.DocumentDate,
            document.Status,
            document.ApprovalRequestId,
            document.Remarks,
            createdLines);
    }

    public async Task<SiteStockDocumentDto> ReturnAsync(ReturnStockRequest request, CancellationToken ct = default)
    {
        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new ArgumentException("Return requires at least one line item.");
        }

        var site = await _db.Sites.FirstOrDefaultAsync(s => s.SiteId == request.SiteId, ct)
            ?? throw new ArgumentException($"Site {request.SiteId} not found.");

        if (!site.WarehouseId.HasValue)
        {
            throw new InvalidOperationException($"Site {site.SiteName} does not have an assigned warehouse in Inventory.");
        }

        long siteWarehouseId = site.WarehouseId.Value;

        // Guarded check: OnSiteQty >= return quantity for every line
        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0)
            {
                throw new ArgumentException($"Line quantity must be greater than zero for item {line.ItemId}.");
            }

            var balance = await _db.SiteStockBalances.FirstOrDefaultAsync(
                b => b.SiteId == request.SiteId && b.ItemId == line.ItemId, ct);

            decimal currentOnSite = balance?.OnSiteQty ?? 0m;
            if (balance is null || currentOnSite < line.Quantity)
            {
                throw new InvalidOperationException(
                    $"Cannot return {line.Quantity} of item {line.ItemId}. Only {currentOnSite} is currently on site.");
            }
        }

        // Allocate document number
        string docNo;
        try
        {
            var allocation = await _numbering.NextAsync("SRT", request.DocumentDate, ct);
            docNo = allocation.Code;
        }
        catch
        {
            docNo = $"SRT-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}";
        }

        // Reverse transfer from site warehouse to destination warehouse
        foreach (var line in request.Lines)
        {
            bool transferred = await _warehouseClient.TransferStockAsync(
                _tenant.CustomerId.GetValueOrDefault(),
                _tenant.OrgId.GetValueOrDefault(),
                siteWarehouseId,
                request.DestinationWarehouseId,
                line.ItemId,
                line.Quantity,
                $"Site return {docNo}",
                ct);

            if (!transferred)
            {
                throw new InvalidOperationException($"Stock return transfer refused for item {line.ItemId}.");
            }
        }

        var document = new SiteStockDocument
        {
            DocumentType = SiteStockDocumentType.Return,
            DocumentNo = docNo,
            SiteId = request.SiteId,
            SourceWarehouseId = request.DestinationWarehouseId,
            DocumentDate = request.DocumentDate,
            Status = SiteStockDocumentStatus.Posted,
            Remarks = request.Remarks,
        };

        _db.SiteStockDocuments.Add(document);
        await _db.SaveChangesAsync(ct);

        var createdLines = new List<SiteStockLineDto>();

        foreach (var line in request.Lines)
        {
            var stockLine = new SiteStockLine
            {
                DocumentId = document.DocumentId,
                ItemId = line.ItemId,
                Quantity = line.Quantity,
                UnitCost = line.UnitCost,
                BilledQuantity = 0m,
            };

            _db.SiteStockLines.Add(stockLine);

            // Guarded conditional update on SiteStockBalances
            var balance = await _db.SiteStockBalances.FirstAsync(
                b => b.SiteId == request.SiteId && b.ItemId == line.ItemId, ct);

            balance.ReturnedQty += line.Quantity;
            balance.OnSiteQty -= line.Quantity;

            if (balance.OnSiteQty < 0m)
            {
                throw new InvalidOperationException($"Guarded check violated: OnSiteQty became negative for item {line.ItemId}.");
            }

            createdLines.Add(new SiteStockLineDto(
                stockLine.LineId,
                stockLine.ItemId,
                stockLine.Quantity,
                stockLine.UnitCost,
                stockLine.MarginValue,
                stockLine.SellingPrice,
                stockLine.BilledQuantity));
        }

        await _db.SaveChangesAsync(ct);

        return new SiteStockDocumentDto(
            document.DocumentId,
            document.DocumentType,
            document.DocumentNo,
            document.SiteId,
            site.SiteName,
            document.SourceWarehouseId,
            document.DocumentDate,
            document.Status,
            document.ApprovalRequestId,
            document.Remarks,
            createdLines);
    }
}
