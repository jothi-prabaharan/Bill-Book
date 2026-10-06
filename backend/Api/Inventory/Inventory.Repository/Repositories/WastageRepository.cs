using Inventory.Entity.Enums;
using Inventory.Entity.Models;
using Inventory.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Persistence;

namespace Inventory.Repository.Repositories;

/// <summary>
/// Wastage (TK-149): food thrown away, with why. Each entry is a stock
/// adjustment out of stock, so it posts to cost of goods sold like any
/// write-off and waits for approval where the branch approves stock
/// adjustments. No second path out of stock is built for it.
/// </summary>
public sealed class WastageRepository
{
    private readonly InventoryDbContext _db;
    private readonly StockAdjustmentRepository _adjustments;
    private readonly TimeProvider _clock;

    public WastageRepository(InventoryDbContext db, StockAdjustmentRepository adjustments, TimeProvider clock)
    {
        _db = db;
        _adjustments = adjustments;
        _clock = clock;
    }

    /// <summary>The adjustment reason a wastage reason is recorded under: expired food as expired, everything else as damage.</summary>
    public static StockAdjustmentReason AdjustmentReason(WastageReason reason) => reason switch
    {
        WastageReason.Expired => StockAdjustmentReason.Expired,
        WastageReason.Other => StockAdjustmentReason.Other,
        _ => StockAdjustmentReason.Damage,
    };

    public static string ReasonLabel(WastageReason reason) => reason switch
    {
        WastageReason.ReturnedByGuest => "Returned by a guest",
        WastageReason.Overproduced => "Made too much",
        _ => reason.ToString(),
    };

    public async Task<List<WastageView>> ListAsync(DateOnly? fromDate, DateOnly? toDate, CancellationToken ct)
    {
        var rows = await (
                from w in _db.WastageEntries
                join i in _db.Items on w.ItemId equals i.ItemId
                join u in _db.UnitOfMeasures on w.UomId equals u.UomId
                join a in _db.StockAdjustments on w.StockAdjustmentId equals a.StockAdjustmentId
                where (fromDate == null || w.EntryDate >= fromDate) && (toDate == null || w.EntryDate <= toDate)
                orderby w.EntryDate descending, w.WastageEntryId descending
                select new { Entry = w, i.ItemName, i.InventoryUomId, u.UomCode, ToBase = u.ConversionToBase, a.AdjustmentNo, a.Status })
            .Take(500)
            .ToListAsync(ct);

        List<long> itemIds = rows.Select(r => r.Entry.ItemId).Distinct().ToList();
        Dictionary<long, decimal> averages = await _db.ItemStock.AsNoTracking().Where(s => itemIds.Contains(s.ItemId)).ToDictionaryAsync(s => s.ItemId, s => s.WeightedAverageCost, ct);
        Dictionary<long, decimal> inventoryToBase = await _db.UnitOfMeasures.AsNoTracking().ToDictionaryAsync(u => u.UomId, u => u.ConversionToBase, ct);

        return rows.Select(r => new WastageView
        {
            WastageEntryId = r.Entry.WastageEntryId,
            EntryDate = r.Entry.EntryDate,
            ItemId = r.Entry.ItemId,
            ItemName = r.ItemName,
            Quantity = r.Entry.Quantity,
            UomCode = r.UomCode,
            Reason = r.Entry.Reason,
            Note = r.Entry.Note,
            StockAdjustmentId = r.Entry.StockAdjustmentId,
            AdjustmentNo = r.AdjustmentNo ?? string.Empty,
            Status = r.Status.ToString(),
            Cost = Math.Round(
                RecipeRulesRepository.Convert(r.Entry.Quantity, r.ToBase, inventoryToBase.GetValueOrDefault(r.InventoryUomId, r.ToBase)) * averages.GetValueOrDefault(r.Entry.ItemId),
                2,
                MidpointRounding.AwayFromZero),
        }).ToList();
    }

    /// <summary>
    /// Records food thrown away: a stock adjustment out, posted at once, or
    /// left waiting for approval when the branch approves adjustments.
    /// </summary>
    public async Task<MenuResult<long>> RecordAsync(RecordWastageRequest request, CancellationToken ct)
    {
        var item = await _db.Items.Where(i => i.ItemId == request.ItemId)
            .Select(i => new { i.ItemName, i.TrackInventory, i.InventoryUomId, i.UomTypeId })
            .FirstOrDefaultAsync(ct);
        if (item is null)
        {
            return MenuResult<long>.Refuse("That item does not exist.");
        }

        if (!item.TrackInventory)
        {
            return MenuResult<long>.Refuse($"{item.ItemName} is not a stocked item, so there is no stock to write off.");
        }

        long uomId = request.UomId ?? item.InventoryUomId;
        if (!await _db.UnitOfMeasures.AnyAsync(u => u.UomId == uomId && u.UomTypeId == item.UomTypeId, ct))
        {
            return MenuResult<long>.Refuse($"Choose a unit {item.ItemName} is measured in.");
        }

        DateOnly date = request.EntryDate ?? DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);
        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        StockAdjustmentResult saved = await _adjustments.SaveAsync(null, new SaveStockAdjustmentRequest
        {
            AdjustmentDate = date,
            Kind = nameof(StockAdjustmentKind.Adjustment),
            Reason = AdjustmentReason(request.Reason).ToString(),
            Notes = $"Wastage: {ReasonLabel(request.Reason)}{(string.IsNullOrWhiteSpace(request.Note) ? string.Empty : $" — {request.Note.Trim()}")}",
            Lines = [new SaveStockAdjustmentLineRequest { ItemId = request.ItemId, UomId = uomId, Quantity = request.Quantity, Direction = "Out" }],
        }, ct);
        if (saved.Outcome != StockAdjustmentOutcome.Ok || saved.StockAdjustmentId is not long adjustmentId)
        {
            return MenuResult<long>.Refuse(saved.Detail ?? $"The write-off was refused ({saved.Outcome}).");
        }

        StockAdjustmentResult posted = await _adjustments.PostAsync(adjustmentId, ct);
        if (posted.Outcome is not (StockAdjustmentOutcome.Ok or StockAdjustmentOutcome.AwaitingApproval))
        {
            return MenuResult<long>.Refuse(posted.Detail ?? $"The write-off could not be posted ({posted.Outcome}).");
        }

        WastageEntry entry = new()
        {
            EntryDate = date,
            ItemId = request.ItemId,
            Quantity = request.Quantity,
            UomId = uomId,
            Reason = request.Reason,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            StockAdjustmentId = adjustmentId,
        };
        _db.WastageEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return MenuResult<long>.Done(entry.WastageEntryId);
    }
}
