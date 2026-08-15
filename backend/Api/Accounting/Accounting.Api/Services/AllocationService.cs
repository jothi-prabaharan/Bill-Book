using Accounting.Entity;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;

namespace Accounting.Api.Services;

public sealed class AllocationService
{
    private readonly AccountingDbContext _db;
    private readonly TenantContext _tenant;

    public AllocationService(AccountingDbContext db, TenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task AllocateAsync(AllocateTransactionRequest request, CancellationToken ct)
    {
        // AR = 1, AP = 2. Depending on whether it's customer or vendor.
        var targetLedgers = await _db.JournalLedger
            .Where(x => x.OrgId == _tenant.Require().OrgId && 
                        x.TransactionTypeCode == request.TargetTransactionTypeCode &&
                        x.TransactionId == request.TargetTransactionId &&
                        (x.LedgerTypeId == 1 || x.LedgerTypeId == 2))
            .ToListAsync(ct);
            
        if (!targetLedgers.Any())
            throw new InvalidOperationException("Target transaction not found in ledger or has no AR/AP balance.");
        
        decimal targetTotal = targetLedgers.Sum(x => x.DebitAmount > 0 ? x.DebitAmount : x.CreditAmount);

        // Sum existing allocations against this target
        var existingAllocations = await _db.TransactionRatios
            .Where(x => x.OrgId == _tenant.Require().OrgId && 
                        x.TargetTransactionTypeCode == request.TargetTransactionTypeCode &&
                        x.TargetTransactionId == request.TargetTransactionId)
            .SumAsync(x => x.Amount, ct);

        if (existingAllocations + request.Amount > targetTotal)
            throw new InvalidOperationException($"Allocation exceeds target outstanding balance. Target Total: {targetTotal}, Existing: {existingAllocations}, Requested: {request.Amount}");

        var ratio = new TransactionRatio
        {
            OrgId = _tenant.Require().OrgId,
            SourceTransactionTypeCode = request.SourceTransactionTypeCode,
            SourceTransactionId = request.SourceTransactionId,
            TargetTransactionTypeCode = request.TargetTransactionTypeCode,
            TargetTransactionId = request.TargetTransactionId,
            Amount = request.Amount,
            AllocatedAt = DateTime.UtcNow
        };

        _db.TransactionRatios.Add(ratio);
        await _db.SaveChangesAsync(ct);
    }
}

public class AllocateTransactionRequest
{
    public string SourceTransactionTypeCode { get; set; } = null!;
    public long SourceTransactionId { get; set; }
    public string TargetTransactionTypeCode { get; set; } = null!;
    public long TargetTransactionId { get; set; }
    public decimal Amount { get; set; }
}
