using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using Npgsql;
using Shared.Kernel.Tenancy;

namespace Accounting.Api.Services;

/// <summary>
/// Allocates one document against another — a credit note against the invoice
/// it settles, a debit note against the bill it corrects — by writing an
/// <c>acc.TransactionRatio</c> row.
///
/// <b>The whole task is the guard.</b> A claim on a document must never exceed
/// what the document still represents, and the sum spans rows: the target's
/// CONTROL legs in the ledger say what it was worth, every existing
/// <c>TransactionRatio</c> row against it says what has already been claimed,
/// and only the difference is available. Both are read and the row is written
/// in one serializable transaction, so two allocations racing each other cannot
/// both pass a guard neither saw the other's row.
///
/// <b>Replace, never append.</b> The key is (source, target): re-allocating the
/// same pair replaces the earlier row. That is what makes a repost after a
/// dropped response safe, and what lets a void clear the claim by deleting the
/// row rather than by writing an offsetting one.
///
/// <b>What is deliberately not read: the money documents' own lines.</b> A
/// payment against an invoice is its own mechanism — <c>SpendMoney</c> and
/// <c>ReceiveMoney</c> lines carry the settled document and their control legs
/// carry the claim. This table is for document-to-document allocation, and the
/// guard reads the ledger's CONTROL net precisely so a payment that *does* post
/// its credit under the target's key reduces what is available.
/// </summary>
public sealed class AllocationService
{
    /// <summary><c>mst.LedgerTypes</c> 3 — the AP / AR / bank / cash control leg.</summary>
    private const int ControlLedgerType = 3;

    private readonly AccountingDbContext _db;
    private readonly TenantContext _tenant;
    private readonly ILogger<AllocationService> _log;

    public AllocationService(
        AccountingDbContext db, TenantContext tenant, ILogger<AllocationService> log)
    {
        _db = db;
        _tenant = tenant;
        _log = log;
    }

    public async Task<AllocationResult> AllocateAsync(
        AllocateTransactionRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0m)
        {
            // The check constraint would refuse the row anyway; this says why.
            return new AllocationResult(
                AllocationOutcome.Refused, "An allocation must be a positive amount.");
        }

        string targetCode = request.TargetTransactionTypeCode.ToUpperInvariant();
        string sourceCode = request.SourceTransactionTypeCode.ToUpperInvariant();

        // Read, decide and write as one act: two requests racing the same target
        // must not both pass a guard based on rows neither saw being written.
        await using IDbContextTransaction tx =
            await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        try
        {
            // What the target still represents: its CONTROL legs, netted in base
            // currency — the same convention the balance trigger, the trial
            // balance and the outstanding report all use.
            decimal net = await _db.JournalLedger
                .Where(l => l.TransactionTypeCode == targetCode
                    && l.TransactionId == request.TargetTransactionId
                    && l.LedgerTypeId == ControlLedgerType)
                .GroupBy(l => 1)
                .Select(g => g.Sum(l => l.DebitAmountBase) - g.Sum(l => l.CreditAmountBase))
                .FirstOrDefaultAsync(ct);

            if (Math.Abs(net) == 0m)
            {
                return new AllocationResult(
                    AllocationOutcome.Refused,
                    $"{targetCode} {request.TargetTransactionId} has no outstanding balance to "
                        + "allocate against. A document with nothing left owes nothing.");
            }

            // What has already been claimed against this target, excluding the
            // pair being replaced — a repost must not be judged against itself.
            decimal claimed = await _db.TransactionRatios
                .Where(t => t.TargetTransactionTypeCode == targetCode
                    && t.TargetTransactionId == request.TargetTransactionId
                    && !(t.SourceTransactionTypeCode == sourceCode
                        && t.SourceTransactionId == request.SourceTransactionId))
                .SumAsync(t => t.Amount, ct);

            decimal available = Math.Abs(net) - claimed;

            // Exact money: the ledger stores two decimals, the sums are exact,
            // and a claim a paisa past what is left is a paisa too far.
            if (request.Amount > available)
            {
                return new AllocationResult(
                    AllocationOutcome.Refused,
                    $"Allocating {request.Amount:0.00} would exceed what {targetCode} "
                        + $"{request.TargetTransactionId} still represents. "
                        + $"{Math.Abs(net):0.00} was posted against it, {claimed:0.00} has already "
                        + $"been allocated, and {available:0.00} remains.");
            }

            // Replace: a retry after a dropped response lands one row, not two.
            await _db.TransactionRatios
                .Where(t => t.SourceTransactionTypeCode == sourceCode
                    && t.SourceTransactionId == request.SourceTransactionId
                    && t.TargetTransactionTypeCode == targetCode
                    && t.TargetTransactionId == request.TargetTransactionId)
                .ExecuteDeleteAsync(ct);

            // The change tracker never saw the deleted rows (the delete went
            // straight to the database), so nothing stale can be written back.
            _db.TransactionRatios.Add(new TransactionRatio
            {
                SourceTransactionTypeCode = sourceCode,
                SourceTransactionId = request.SourceTransactionId,
                TargetTransactionTypeCode = targetCode,
                TargetTransactionId = request.TargetTransactionId,
                Amount = request.Amount,
                AllocatedAt = DateTime.UtcNow,
            });

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return new AllocationResult(AllocationOutcome.Ok);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            // The other writer won the race. Nothing was written; the caller
            // can retry against the fresh state.
            _log.LogWarning(
                "Allocation {Source}-{SourceId} against {Target}-{TargetId} raced another "
                    + "allocation and was rolled back.",
                sourceCode, request.SourceTransactionId, targetCode, request.TargetTransactionId);

            return new AllocationResult(
                AllocationOutcome.Retry,
                "Another allocation to the same document was being written at the same time, "
                    + "so this one was not applied. It can be retried.");
        }
        finally
        {
            await tx.DisposeAsync();
        }
    }

    /// <summary>
    /// Removes every allocation a source document made — the void half. Deleting
    /// the rows rather than writing an offsetting one keeps the guard honest:
    /// a claim that no longer exists must not keep occupying the target's
    /// balance, and an offsetting row would.
    /// </summary>
    public async Task RemoveAllocationsAsync(
        string sourceTransactionTypeCode, long sourceTransactionId, CancellationToken ct)
    {
        string sourceCode = sourceTransactionTypeCode.ToUpperInvariant();

        await _db.TransactionRatios
            .Where(t => t.SourceTransactionTypeCode == sourceCode
                && t.SourceTransactionId == sourceTransactionId)
            .ExecuteDeleteAsync(ct);
    }
}