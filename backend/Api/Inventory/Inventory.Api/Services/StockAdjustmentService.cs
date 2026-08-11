using Inventory.Entity.Enums;
using Inventory.Entity.Models;
using Inventory.Entity.TableEntities;
using Inventory.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Numbering;

namespace Inventory.Api.Services;

/// <summary>
/// Stock adjustment sheets — a count or a write-off posted as one document.
///
/// <b>It moves stock through <see cref="StockService"/> and posts nothing
/// itself.</b> Each line records an ordinary movement carrying this document's
/// id, and the movement-to-ledger mapping already files a sourced movement under
/// its document. So a twenty-line count produces twenty movements and one
/// adjustment in the ledger, and there is still exactly one place in the product
/// that decides what a stock movement means in the general ledger.
///
/// <b>A posted sheet is never edited, and never voided.</b> Voiding a payment
/// withdraws ledger rows and nothing else has happened; voiding this would have
/// to un-move stock that physically moved. The correction is a mirror-image
/// sheet, linked to the original from both ends — the same shape as a journal
/// reversal, for the same reason.
/// </summary>
public sealed class StockAdjustmentService
{
    /// <summary>The transaction type this document is, and the series it numbers from.</summary>
    private const string TypeCode = "STA";

    private readonly InventoryDbContext _db;
    private readonly StockService _stock;
    private readonly INumberGenerator _numbers;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public StockAdjustmentService(
        InventoryDbContext db,
        StockService stock,
        INumberGenerator numbers,
        ICurrentUser user,
        TimeProvider clock)
    {
        _db = db;
        _stock = stock;
        _numbers = numbers;
        _user = user;
        _clock = clock;
    }

    public async Task<IReadOnlyList<StockAdjustmentListItem>> ListAsync(
        string? status, CancellationToken ct)
    {
        IQueryable<StockAdjustment> query = _db.StockAdjustments;

        if (Enum.TryParse(status, ignoreCase: true, out StockAdjustmentStatus wanted))
        {
            query = query.Where(a => a.Status == wanted);
        }

        return await query
            .OrderByDescending(a => a.AdjustmentDate)
            .ThenByDescending(a => a.StockAdjustmentId)
            .Select(a => new StockAdjustmentListItem
            {
                StockAdjustmentId = a.StockAdjustmentId,
                AdjustmentNo = a.AdjustmentNo,
                AdjustmentDate = a.AdjustmentDate,
                Kind = a.Kind.ToString(),
                Reason = a.Reason.ToString(),
                Status = a.Status.ToString(),
                WarehouseId = a.WarehouseId,
                WarehouseName = _db.Warehouses
                    .Where(w => w.WarehouseId == a.WarehouseId)
                    .Select(w => w.WarehouseName)
                    .FirstOrDefault(),
                LineCount = _db.StockAdjustmentLines
                    .Count(l => l.StockAdjustmentId == a.StockAdjustmentId),
                NetValue = NetValueOf(a.StockAdjustmentId),
                Notes = a.Notes,
                ReversedByStockAdjustmentId = a.ReversedByStockAdjustmentId,
                ReversesStockAdjustmentId = a.ReversesStockAdjustmentId,
                PostedAt = a.PostedAt,
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// What the sheet is worth, from the movements it wrote. Null while any line
    /// is still uncosted — costing settles a moment behind, and a partial total
    /// reads exactly like a complete one.
    /// </summary>
    private decimal? NetValueOf(long adjustmentId) =>
        _db.StockAdjustmentLines
            .Where(l => l.StockAdjustmentId == adjustmentId && l.StockMovementId != null)
            .Join(_db.StockMovements, l => l.StockMovementId, m => m.StockMovementId,
                (l, m) => new { l.Direction, m.TotalCost })
            .Any(x => x.TotalCost == null)
            ? null
            : _db.StockAdjustmentLines
                .Where(l => l.StockAdjustmentId == adjustmentId && l.StockMovementId != null)
                .Join(_db.StockMovements, l => l.StockMovementId, m => m.StockMovementId,
                    (l, m) => l.Direction == StockDirection.In
                        ? m.TotalCost
                        : -m.TotalCost)
                .Sum();

    public async Task<StockAdjustmentDetail?> GetAsync(long id, CancellationToken ct)
    {
        // Queried by id rather than filtered out of the list: a branch with a
        // year of counts behind it would otherwise read every one of them to
        // open a single sheet.
        StockAdjustmentListItem? header = await _db.StockAdjustments
            .Where(a => a.StockAdjustmentId == id)
            .Select(a => new StockAdjustmentListItem
            {
                StockAdjustmentId = a.StockAdjustmentId,
                AdjustmentNo = a.AdjustmentNo,
                AdjustmentDate = a.AdjustmentDate,
                Kind = a.Kind.ToString(),
                Reason = a.Reason.ToString(),
                Status = a.Status.ToString(),
                WarehouseId = a.WarehouseId,
                WarehouseName = _db.Warehouses
                    .Where(w => w.WarehouseId == a.WarehouseId)
                    .Select(w => w.WarehouseName)
                    .FirstOrDefault(),
                LineCount = _db.StockAdjustmentLines
                    .Count(l => l.StockAdjustmentId == a.StockAdjustmentId),
                NetValue = NetValueOf(a.StockAdjustmentId),
                Notes = a.Notes,
                ReversedByStockAdjustmentId = a.ReversedByStockAdjustmentId,
                ReversesStockAdjustmentId = a.ReversesStockAdjustmentId,
                PostedAt = a.PostedAt,
            })
            .FirstOrDefaultAsync(ct);

        if (header is null)
        {
            return null;
        }

        List<StockAdjustmentLineDetail> lines = await _db.StockAdjustmentLines
            .Where(l => l.StockAdjustmentId == id)
            .OrderBy(l => l.LineNumber)
            .Join(_db.Items, l => l.ItemId, i => i.ItemId, (l, i) => new { l, i })
            .Select(x => new StockAdjustmentLineDetail
            {
                StockAdjustmentLineId = x.l.StockAdjustmentLineId,
                LineNumber = x.l.LineNumber,
                ItemId = x.l.ItemId,
                ItemCode = x.i.ItemCode,
                ItemName = x.i.ItemName,
                UomId = x.l.UomId,
                UomCode = _db.UnitOfMeasures
                    .Where(u => u.UomId == x.l.UomId).Select(u => u.UomCode).FirstOrDefault(),
                InventoryUomCode = _db.UnitOfMeasures
                    .Where(u => u.UomId == x.i.InventoryUomId).Select(u => u.UomCode).First(),
                SystemQuantity = x.l.SystemQuantity,
                CountedQuantity = x.l.CountedQuantity,
                Quantity = x.l.Quantity,
                Direction = x.l.Direction.ToString(),
                UnitCost = x.l.UnitCost,
                BatchNumber = x.l.BatchNumber,
                BatchExpiryDate = x.l.BatchExpiryDate,
                StockMovementId = x.l.StockMovementId,
                MovementTotalCost = _db.StockMovements
                    .Where(m => m.StockMovementId == x.l.StockMovementId)
                    .Select(m => m.TotalCost)
                    .FirstOrDefault(),
                Notes = x.l.Notes,
            })
            .ToListAsync(ct);

        return new StockAdjustmentDetail
        {
            StockAdjustmentId = header.StockAdjustmentId,
            AdjustmentNo = header.AdjustmentNo,
            AdjustmentDate = header.AdjustmentDate,
            Kind = header.Kind,
            Reason = header.Reason,
            Status = header.Status,
            WarehouseId = header.WarehouseId,
            WarehouseName = header.WarehouseName,
            LineCount = header.LineCount,
            NetValue = header.NetValue,
            Notes = header.Notes,
            ReversedByStockAdjustmentId = header.ReversedByStockAdjustmentId,
            ReversesStockAdjustmentId = header.ReversesStockAdjustmentId,
            PostedAt = header.PostedAt,
            Lines = lines,
        };
    }

    /// <summary>
    /// Creates or replaces a draft. The lines are replaced wholesale rather than
    /// merged: a sheet is edited as a sheet, and matching up rows the client did
    /// not identify is guesswork that silently loses one.
    /// </summary>
    public async Task<StockAdjustmentResult> SaveAsync(
        long? id, SaveStockAdjustmentRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse(request.Kind, ignoreCase: true, out StockAdjustmentKind kind)
            || !Enum.TryParse(request.Reason, ignoreCase: true, out StockAdjustmentReason reason))
        {
            return new StockAdjustmentResult(
                StockAdjustmentOutcome.InvalidValue, id,
                "The adjustment kind or reason was not recognised.");
        }

        StockAdjustment? document;

        if (id is long existingId)
        {
            document = await _db.StockAdjustments
                .FirstOrDefaultAsync(a => a.StockAdjustmentId == existingId, ct);

            if (document is null)
            {
                return new StockAdjustmentResult(StockAdjustmentOutcome.NotFound);
            }

            if (document.Status != StockAdjustmentStatus.Draft)
            {
                return new StockAdjustmentResult(
                    StockAdjustmentOutcome.NotDraft, existingId,
                    "The sheet has posted, so its stock has already moved. Reverse it instead.");
            }

            await _db.StockAdjustmentLines
                .Where(l => l.StockAdjustmentId == existingId)
                .ExecuteDeleteAsync(ct);
        }
        else
        {
            document = new StockAdjustment();
            _db.StockAdjustments.Add(document);
        }

        document.AdjustmentDate = request.AdjustmentDate
            ?? DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        document.Kind = kind;
        document.Reason = reason;
        document.WarehouseId = request.WarehouseId;
        document.Notes = request.Notes;

        await _db.SaveChangesAsync(ct);

        int lineNumber = 0;

        foreach (SaveStockAdjustmentLineRequest line in request.Lines)
        {
            (decimal quantity, StockDirection direction, decimal? systemQuantity) =
                await ResolveLineAsync(kind, line, ct);

            // A counted line that agrees with the books moves nothing. Dropped
            // rather than refused: on a sheet of twenty, most lines agreeing is
            // the ordinary case and the good news.
            if (quantity <= 0)
            {
                continue;
            }

            _db.StockAdjustmentLines.Add(new StockAdjustmentLine
            {
                StockAdjustmentId = document.StockAdjustmentId,
                LineNumber = ++lineNumber,
                ItemId = line.ItemId,
                UomId = line.UomId,
                SystemQuantity = systemQuantity,
                CountedQuantity = kind == StockAdjustmentKind.PhysicalCount
                    ? line.CountedQuantity
                    : null,
                Quantity = quantity,
                Direction = direction,
                // Cost belongs to stock coming in; the database refuses it on
                // the way out, so it is dropped here rather than sent to fail.
                UnitCost = direction == StockDirection.In ? line.UnitCost : null,
                BatchNumber = line.BatchNumber,
                BatchExpiryDate = line.BatchExpiryDate,
                Notes = line.Notes,
            });
        }

        await _db.SaveChangesAsync(ct);
        return new StockAdjustmentResult(StockAdjustmentOutcome.Ok, document.StockAdjustmentId);
    }

    /// <summary>
    /// What actually moves, and which way.
    ///
    /// A physical count keys a total and the difference is derived; an
    /// adjustment keys the difference directly. The on-hand figure is read
    /// <b>here, at save</b>, and stored — the sheet is a claim about one moment,
    /// and re-deriving it at post would compare the count against a quantity
    /// that has moved since.
    /// </summary>
    private async Task<(decimal Quantity, StockDirection Direction, decimal? SystemQuantity)>
        ResolveLineAsync(
            StockAdjustmentKind kind, SaveStockAdjustmentLineRequest line, CancellationToken ct)
    {
        if (kind != StockAdjustmentKind.PhysicalCount)
        {
            StockDirection direction =
                Enum.TryParse(line.Direction, ignoreCase: true, out StockDirection parsed)
                    ? parsed
                    : StockDirection.Out;

            return (line.Quantity ?? 0m, direction, null);
        }

        decimal onHand = await _db.ItemStock
            .Where(s => s.ItemId == line.ItemId)
            .Select(s => s.QuantityOnHand)
            .FirstOrDefaultAsync(ct);

        decimal counted = line.CountedQuantity ?? 0m;
        decimal difference = counted - onHand;

        return (Math.Abs(difference),
            difference >= 0 ? StockDirection.In : StockDirection.Out,
            onHand);
    }

    /// <summary>A draft can be thrown away. Nothing has happened yet.</summary>
    public async Task<StockAdjustmentResult> DeleteAsync(long id, CancellationToken ct)
    {
        StockAdjustment? document = await _db.StockAdjustments
            .FirstOrDefaultAsync(a => a.StockAdjustmentId == id, ct);

        if (document is null)
        {
            return new StockAdjustmentResult(StockAdjustmentOutcome.NotFound);
        }

        if (document.Status != StockAdjustmentStatus.Draft)
        {
            return new StockAdjustmentResult(
                StockAdjustmentOutcome.NotDraft, id,
                "The sheet has posted. A posted document is never deleted — reverse it.");
        }

        _db.StockAdjustments.Remove(document);
        await _db.SaveChangesAsync(ct);
        return new StockAdjustmentResult(StockAdjustmentOutcome.Ok, id);
    }

    /// <summary>
    /// Moves the stock and takes the number.
    ///
    /// <b>All of it or none of it.</b> The whole sheet runs inside one
    /// transaction, so a count that fails on line seventeen leaves the first
    /// sixteen unmoved. A half-posted count is worse than one that did not post,
    /// because only the second is obvious.
    /// </summary>
    public async Task<StockAdjustmentResult> PostAsync(long id, CancellationToken ct)
    {
        StockAdjustment? document = await _db.StockAdjustments
            .FirstOrDefaultAsync(a => a.StockAdjustmentId == id, ct);

        if (document is null)
        {
            return new StockAdjustmentResult(StockAdjustmentOutcome.NotFound);
        }

        if (document.Status != StockAdjustmentStatus.Draft)
        {
            return new StockAdjustmentResult(
                StockAdjustmentOutcome.NotDraft, id, "The sheet has already posted.");
        }

        List<StockAdjustmentLine> lines = await _db.StockAdjustmentLines
            .Where(l => l.StockAdjustmentId == id)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        if (lines.Count == 0)
        {
            return new StockAdjustmentResult(
                StockAdjustmentOutcome.NoLines, id,
                "The sheet has no lines that move anything.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        foreach (StockAdjustmentLine line in lines)
        {
            RecordStockMovementResult moved = await _stock.RecordAsync(
                new RecordStockMovementRequest
                {
                    ItemId = line.ItemId,
                    MovementType = nameof(StockMovementType.Adjustment),
                    Direction = line.Direction.ToString(),
                    MovementDate = document.AdjustmentDate,
                    Quantity = line.Quantity,
                    UomId = line.UomId,
                    WarehouseId = document.WarehouseId,
                    UnitCost = line.UnitCost,
                    // What files the movement under this document rather than
                    // under itself, which is the whole point of the sheet.
                    SourceType = TypeCode,
                    SourceId = document.StockAdjustmentId,
                    SourceLineId = line.LineNumber,
                    BatchNumber = line.BatchNumber,
                    BatchExpiryDate = line.BatchExpiryDate,
                    Notes = line.Notes,
                },
                ct);

            if (moved.Outcome != StockOutcome.Ok)
            {
                await tx.RollbackAsync(ct);

                return new StockAdjustmentResult(
                    StockAdjustmentOutcome.LineRefused, id,
                    $"Line {line.LineNumber} was refused: {moved.Outcome}. "
                        + "Nothing on the sheet was posted.");
            }

            line.StockMovementId = moved.StockMovementId;
        }

        // The number is taken last, and inside the same transaction: a rolled
        // back post must give its number back, or the series grows a hole that
        // an auditor will ask about.
        NumberAllocation allocation;

        try
        {
            allocation = await _numbers.NextAsync(TypeCode, document.AdjustmentDate, ct);
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync(ct);

            return new StockAdjustmentResult(
                StockAdjustmentOutcome.SeriesMissing, id,
                "No STA numbering series exists for this branch, so the sheet could not be "
                    + $"numbered and nothing was posted. Re-run the branch seed. ({ex.Message})");
        }

        document.AdjustmentNo = allocation.Code;
        document.Status = StockAdjustmentStatus.Posted;
        document.PostedAt = _clock.GetUtcNow();
        document.PostedBy = _user.UserId;

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new StockAdjustmentResult(StockAdjustmentOutcome.Ok, id);
    }

    /// <summary>
    /// Undoes a posted sheet with a mirror of it — every line the other way
    /// round, posted as its own document and linked to the original from both
    /// ends.
    ///
    /// Not a void, because the stock physically moved: the movements are
    /// append-only and a correction is an opposite movement, never a deletion.
    /// The two documents together are the audit trail, which is more than a
    /// voided one would have left.
    /// </summary>
    public async Task<StockAdjustmentResult> ReverseAsync(
        long id, ReverseStockAdjustmentRequest request, CancellationToken ct)
    {
        StockAdjustment? original = await _db.StockAdjustments
            .FirstOrDefaultAsync(a => a.StockAdjustmentId == id, ct);

        if (original is null)
        {
            return new StockAdjustmentResult(StockAdjustmentOutcome.NotFound);
        }

        if (original.Status == StockAdjustmentStatus.Draft)
        {
            return new StockAdjustmentResult(
                StockAdjustmentOutcome.NotPosted, id,
                "The sheet has not posted, so there is nothing to reverse. Delete it instead.");
        }

        if (original.ReversedByStockAdjustmentId is not null)
        {
            return new StockAdjustmentResult(
                StockAdjustmentOutcome.AlreadyReversed, id,
                "The sheet has already been reversed.");
        }

        List<StockAdjustmentLine> lines = await _db.StockAdjustmentLines
            .Where(l => l.StockAdjustmentId == id)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        var mirror = new StockAdjustment
        {
            // Dated today, not on the original's date: the correction happens
            // when it is made. Back-dating it into a period that may be closed
            // would rewrite what the ledger already said about that month.
            AdjustmentDate = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime),
            Kind = original.Kind,
            Reason = StockAdjustmentReason.Reversal,
            WarehouseId = original.WarehouseId,
            Notes = request.Notes
                ?? $"Reverses {original.AdjustmentNo ?? original.StockAdjustmentId.ToString()}.",
            ReversesStockAdjustmentId = original.StockAdjustmentId,
        };

        _db.StockAdjustments.Add(mirror);
        await _db.SaveChangesAsync(ct);

        int lineNumber = 0;

        foreach (StockAdjustmentLine line in lines)
        {
            _db.StockAdjustmentLines.Add(new StockAdjustmentLine
            {
                StockAdjustmentId = mirror.StockAdjustmentId,
                LineNumber = ++lineNumber,
                ItemId = line.ItemId,
                UomId = line.UomId,
                Quantity = line.Quantity,
                Direction = line.Direction == StockDirection.In
                    ? StockDirection.Out
                    : StockDirection.In,
                // A reversal puts stock back at what it left at, so it names no
                // cost of its own. Stock coming back in is valued at the running
                // average, which is where the original took it from.
                UnitCost = null,
                BatchNumber = line.BatchNumber,
                BatchExpiryDate = line.BatchExpiryDate,
                Notes = line.Notes,
            });
        }

        await _db.SaveChangesAsync(ct);

        StockAdjustmentResult posted = await PostAsync(mirror.StockAdjustmentId, ct);

        if (posted.Outcome != StockAdjustmentOutcome.Ok)
        {
            // The mirror stays as a draft rather than being deleted: it says
            // what was attempted, and the usual cause is stock that has since
            // been sold, which a person has to decide about.
            return posted;
        }

        original.Status = StockAdjustmentStatus.Reversed;
        original.ReversedByStockAdjustmentId = mirror.StockAdjustmentId;
        await _db.SaveChangesAsync(ct);

        return new StockAdjustmentResult(StockAdjustmentOutcome.Ok, mirror.StockAdjustmentId);
    }
}
