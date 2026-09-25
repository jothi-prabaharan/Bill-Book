using Fee.Entity.Enums;
using Fee.Entity.Models;
using Fee.Repository;
using Microsoft.EntityFrameworkCore;

namespace Fee.Api.Services;

/// <summary>
/// A guardian's fees in the parent portal (S9, TK-69): the posted demands
/// addressed to them and the posted receipts they paid. Drafts and voided
/// documents are staff's business and never shown. A demand is addressed to a
/// student's primary guardian, so only that guardian sees it.
/// </summary>
public sealed class PortalFeeService
{
    private readonly FeeDbContext _db;

    public PortalFeeService(FeeDbContext db) => _db = db;

    public async Task<List<PortalDemandView>> DemandsAsync(long contactId, CancellationToken ct)
    {
        List<Entity.TableEntities.FeeDemand> demands = await _db.FeeDemands.AsNoTracking()
            .Where(d => d.ContactId == contactId && d.DocumentStatus == FeeDocumentStatus.Posted)
            .OrderByDescending(d => d.DueDate).ThenByDescending(d => d.FeeDemandId)
            .Take(500)
            .ToListAsync(ct);

        List<long> ids = [.. demands.Select(d => d.FeeDemandId)];
        var lines = (await (
                from l in _db.FeeDemandLines.AsNoTracking()
                join h in _db.FeeHeads on l.FeeHeadId equals h.FeeHeadId
                where ids.Contains(l.FeeDemandId)
                orderby h.Name
                select new { l.FeeDemandId, h.Name, l.Amount, l.ConcessionAmount })
                .ToListAsync(ct))
            .ToLookup(l => l.FeeDemandId);

        return [.. demands.Select(d => new PortalDemandView
        {
            FeeDemandId = d.FeeDemandId,
            DemandNo = d.DemandNo ?? string.Empty,
            StudentId = d.StudentId,
            PeriodKey = d.PeriodKey,
            DemandDate = d.DemandDate,
            DueDate = d.DueDate,
            TotalAmount = d.TotalAmount,
            ConcessionAmount = d.ConcessionAmount,
            NetAmount = d.NetAmount,
            PaidAmount = d.PaidAmount,
            Balance = d.NetAmount - d.PaidAmount,
            Lines = [.. lines[d.FeeDemandId].Select(l => new PortalDemandLineView { FeeHeadName = l.Name, Amount = l.Amount, ConcessionAmount = l.ConcessionAmount })],
        })];
    }

    public async Task<List<PortalReceiptView>> ReceiptsAsync(long contactId, CancellationToken ct)
    {
        List<Entity.TableEntities.FeeReceipt> receipts = await _db.FeeReceipts.AsNoTracking()
            .Where(r => r.ContactId == contactId && r.DocumentStatus == FeeDocumentStatus.Posted)
            .OrderByDescending(r => r.ReceiptDate).ThenByDescending(r => r.FeeReceiptId)
            .Take(500)
            .ToListAsync(ct);

        List<long> ids = [.. receipts.Select(r => r.FeeReceiptId)];
        var settled = (await (
                from a in _db.FeeReceiptAllocations.AsNoTracking()
                join d in _db.FeeDemands on a.FeeDemandId equals d.FeeDemandId
                where ids.Contains(a.FeeReceiptId)
                select new { a.FeeReceiptId, d.DemandNo })
                .ToListAsync(ct))
            .ToLookup(a => a.FeeReceiptId, a => a.DemandNo ?? string.Empty);

        return [.. receipts.Select(r => new PortalReceiptView
        {
            FeeReceiptId = r.FeeReceiptId,
            ReceiptNo = r.ReceiptNo,
            ReceiptDate = r.ReceiptDate,
            PaymentMode = r.PaymentMode,
            Amount = r.Amount,
            UnallocatedAmount = r.UnallocatedAmount,
            DemandNos = [.. settled[r.FeeReceiptId].Distinct().Order()],
        })];
    }
}
