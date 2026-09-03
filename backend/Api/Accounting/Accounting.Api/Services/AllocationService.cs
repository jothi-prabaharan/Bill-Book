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
            // Voided rows are history and hold nothing: a released claim that
            // went on occupying the balance would be money nobody could spend.
            decimal claimed = await _db.TransactionRatios
                .Where(t => t.TargetTransactionTypeCode == targetCode
                    && t.TargetTransactionId == request.TargetTransactionId
                    && !t.IsVoided
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
            // Only the live row is replaced — a voided row is the record of a
            // claim that was released, and re-allocating the same pair must not
            // erase that history.
            await _db.TransactionRatios
                .Where(t => t.SourceTransactionTypeCode == sourceCode
                    && t.SourceTransactionId == request.SourceTransactionId
                    && t.TargetTransactionTypeCode == targetCode
                    && t.TargetTransactionId == request.TargetTransactionId
                    && !t.IsVoided)
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
                AllocationDate = request.AllocationDate
                    ?? DateOnly.FromDateTime(DateTime.UtcNow),
                Notes = request.Notes,
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
    /// Releases every allocation a source document made — the void half.
    ///
    /// <b>Voided, not deleted.</b> The claim has to stop occupying the target's
    /// balance, which the guard sees to by ignoring voided rows; keeping the row
    /// keeps the answer to "what was this invoice settled against before the
    /// credit note was withdrawn". An offsetting row would do neither — it would
    /// leave the claim summing to its old total.
    /// </summary>
    public async Task RemoveAllocationsAsync(
        string sourceTransactionTypeCode, long sourceTransactionId, CancellationToken ct,
        string reason = "The source document was voided.")
    {
        string sourceCode = sourceTransactionTypeCode.ToUpperInvariant();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await _db.TransactionRatios
            .Where(t => t.SourceTransactionTypeCode == sourceCode
                && t.SourceTransactionId == sourceTransactionId
                && !t.IsVoided)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(t => t.IsVoided, true)
                    .SetProperty(t => t.VoidedAt, now)
                    .SetProperty(t => t.VoidReason, reason),
                ct);
    }

    /// <summary>
    /// Releases one allocation by its id. Returns false when no live row with
    /// that id belongs to the caller's branch — the caller decides whether that
    /// is a 404 or a 403, since only it can tell the two apart.
    /// </summary>
    public async Task<bool> VoidAsync(long transactionRatioId, string reason, CancellationToken ct)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        int affected = await _db.TransactionRatios
            .Where(t => t.TransactionRatioId == transactionRatioId && !t.IsVoided)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(t => t.IsVoided, true)
                    .SetProperty(t => t.VoidedAt, now)
                    .SetProperty(t => t.VoidReason, reason),
                ct);

        return affected > 0;
    }

    /// <summary>
    /// A page of allocations, newest first. <paramref name="contactId"/> narrows
    /// to the documents that contact's ledger rows name — the only way this
    /// service knows who a document belongs to, since the documents themselves
    /// live in Sales and Purchase.
    /// </summary>
    public async Task<AllocationPageDto> ListAsync(
        int page, int pageSize, long? contactId, bool includeVoided, CancellationToken ct)
    {
        // Clamped rather than trusted: an unbounded page size is a way to ask a
        // tenant database for everything it has.
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        IQueryable<TransactionRatio> query = _db.TransactionRatios;

        if (!includeVoided)
        {
            query = query.Where(t => !t.IsVoided);
        }

        if (contactId is long id)
        {
            // The documents this contact's CONTROL legs name, either end.
            var theirs = _db.JournalLedger
                .Where(l => l.ContactId == id && l.LedgerTypeId == ControlLedgerType)
                .Select(l => new { l.TransactionTypeCode, l.TransactionId });

            query = query.Where(t =>
                theirs.Any(d => d.TransactionTypeCode == t.SourceTransactionTypeCode
                        && d.TransactionId == t.SourceTransactionId)
                || theirs.Any(d => d.TransactionTypeCode == t.TargetTransactionTypeCode
                        && d.TransactionId == t.TargetTransactionId));
        }

        // Counted before the page is taken, so the total says what matched
        // rather than what fitted.
        int total = await query.CountAsync(ct);

        List<AllocationListItemDto> items = await query
            .OrderByDescending(t => t.TransactionRatioId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new AllocationListItemDto
            {
                TransactionRatioId = t.TransactionRatioId,
                SourceTransactionTypeCode = t.SourceTransactionTypeCode,
                SourceTransactionId = t.SourceTransactionId,
                TargetTransactionTypeCode = t.TargetTransactionTypeCode,
                TargetTransactionId = t.TargetTransactionId,
                Amount = t.Amount,
                AllocationDate = t.AllocationDate,
                IsVoided = t.IsVoided,
                Notes = t.Notes,
            })
            .ToListAsync(ct);

        return new AllocationPageDto
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    /// <summary>
    /// One allocation, with the target's live balances beside it. Null when no
    /// row with that id is visible to the caller's branch.
    /// </summary>
    public async Task<AllocationDetailDto?> GetAsync(long transactionRatioId, CancellationToken ct)
    {
        TransactionRatio? row = await _db.TransactionRatios
            .FirstOrDefaultAsync(t => t.TransactionRatioId == transactionRatioId, ct);

        if (row is null)
        {
            return null;
        }

        decimal posted = await PostedControlNetAsync(
            row.TargetTransactionTypeCode, row.TargetTransactionId, ct);

        decimal allocated = await _db.TransactionRatios
            .Where(t => t.TargetTransactionTypeCode == row.TargetTransactionTypeCode
                && t.TargetTransactionId == row.TargetTransactionId
                && !t.IsVoided)
            .SumAsync(t => t.Amount, ct);

        return new AllocationDetailDto
        {
            TransactionRatioId = row.TransactionRatioId,
            SourceTransactionTypeCode = row.SourceTransactionTypeCode,
            SourceTransactionId = row.SourceTransactionId,
            TargetTransactionTypeCode = row.TargetTransactionTypeCode,
            TargetTransactionId = row.TargetTransactionId,
            Amount = row.Amount,
            AllocationDate = row.AllocationDate,
            IsVoided = row.IsVoided,
            Notes = row.Notes,
            AllocatedAt = row.AllocatedAt,
            VoidedAt = row.VoidedAt,
            VoidReason = row.VoidReason,
            TargetPostedAmount = posted,
            TargetAllocatedAmount = allocated,
            TargetAvailableAmount = posted - allocated,
        };
    }

    /// <summary>
    /// Everything a contact has open, split by which way its balance runs.
    ///
    /// <b>Direction decides the side, not the document type.</b> A CONTROL net
    /// that runs debit is something owed to the books — an invoice, a bill's
    /// debit note — and belongs on the target side; one that runs credit is
    /// money held — an advance, an overpayment, a credit note — and belongs on
    /// the source side. Keying off the sign rather than a list of type codes is
    /// what lets one endpoint serve a customer and a vendor, and what stops a
    /// document type nobody thought of from falling off the screen.
    /// </summary>
    public async Task<OpenDocumentsDto> GetOpenDocumentsAsync(long contactId, CancellationToken ct)
    {
        var posted = await _db.JournalLedger
            .Where(l => l.ContactId == contactId && l.LedgerTypeId == ControlLedgerType)
            .GroupBy(l => new { l.TransactionTypeCode, l.TransactionId })
            .Select(g => new
            {
                g.Key.TransactionTypeCode,
                g.Key.TransactionId,
                Net = g.Sum(x => x.DebitAmountBase) - g.Sum(x => x.CreditAmountBase),
                DocumentDate = g.Min(x => x.LedgerDate),
            })
            .ToListAsync(ct);

        // Every live claim touching one of this contact's documents, in one
        // query rather than one per document — a contact with a hundred open
        // bills would otherwise be a hundred round trips. Narrowed to the ids
        // just read, so an org's whole allocation history never comes back.
        List<string> codes = [.. posted.Select(d => d.TransactionTypeCode).Distinct()];
        List<long> ids = [.. posted.Select(d => d.TransactionId).Distinct()];

        var rows = await _db.TransactionRatios
            .Where(t => !t.IsVoided
                && ((codes.Contains(t.TargetTransactionTypeCode) && ids.Contains(t.TargetTransactionId))
                    || (codes.Contains(t.SourceTransactionTypeCode) && ids.Contains(t.SourceTransactionId))))
            .Select(t => new
            {
                t.TargetTransactionTypeCode,
                t.TargetTransactionId,
                t.SourceTransactionTypeCode,
                t.SourceTransactionId,
                t.Amount,
            })
            .ToListAsync(ct);

        // A row claims against both ends: it consumes the target's balance and
        // spends the source's credit, so both sides count it.
        Dictionary<(string, long), decimal> claimed = rows
            .SelectMany(t => new[]
            {
                (Key: (t.TargetTransactionTypeCode, t.TargetTransactionId), t.Amount),
                (Key: (t.SourceTransactionTypeCode, t.SourceTransactionId), t.Amount),
            })
            .GroupBy(c => c.Key)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Amount));

        var sources = new List<OpenDocumentDto>();
        var targets = new List<OpenDocumentDto>();

        foreach (var document in posted.OrderBy(d => d.DocumentDate))
        {
            decimal total = Math.Abs(document.Net);

            if (total == 0m)
            {
                continue;
            }

            claimed.TryGetValue((document.TransactionTypeCode, document.TransactionId), out decimal used);

            decimal free = total - used;

            if (free <= 0m)
            {
                // Fully settled. It stays out of the workspace rather than
                // showing as a row nothing can be done with.
                continue;
            }

            var view = new OpenDocumentDto
            {
                TransactionTypeCode = document.TransactionTypeCode,
                TransactionId = document.TransactionId,
                DocumentNo = $"{document.TransactionTypeCode}-{document.TransactionId}",
                DocumentDate = document.DocumentDate,
                TotalAmount = total,
                AllocatedAmount = used,
                UnallocatedAmount = free,
                SettlementStatus = used == 0m
                    ? SettlementStatus.Unallocated
                    : SettlementStatus.PartiallyPaid,
            };

            if (document.Net > 0m)
            {
                targets.Add(view);
            }
            else
            {
                sources.Add(view);
            }
        }

        return new OpenDocumentsDto
        {
            ContactId = contactId,
            Sources = sources,
            Targets = targets,
            TotalOutstanding = targets.Sum(t => t.UnallocatedAmount),
            TotalAvailableCredit = sources.Sum(s => s.UnallocatedAmount),
        };
    }

    /// <summary>
    /// Whether an allocation with this id exists in some other branch's books.
    ///
    /// <b>The only place the query filter is deliberately stepped past.</b> It
    /// answers one bit — does this id belong to somebody else — so a caller
    /// reaching for another branch's allocation is told it is forbidden rather
    /// than that it does not exist. Returning "not found" instead would make the
    /// id space a probe: absent and forbidden would be indistinguishable, so
    /// guessing ids would map out what other branches hold. Nothing about the
    /// row itself is read or returned.
    /// </summary>
    public Task<bool> ExistsInAnotherOrgAsync(long transactionRatioId, CancellationToken ct)
    {
        (Guid customerId, Guid orgId) = _tenant.Require();

        return _db.TransactionRatios
            .IgnoreQueryFilters()
            .AnyAsync(
                t => t.TransactionRatioId == transactionRatioId
                    && (t.CustomerId != customerId || t.OrgId != orgId),
                ct);
    }

    /// <summary>
    /// What a document was posted for, from its CONTROL legs netted in base
    /// currency — the same convention the balance trigger, the trial balance and
    /// the outstanding report all use. Unsigned: the caller knows which way it runs.
    /// </summary>
    private async Task<decimal> PostedControlNetAsync(
        string transactionTypeCode, long transactionId, CancellationToken ct)
    {
        decimal net = await _db.JournalLedger
            .Where(l => l.TransactionTypeCode == transactionTypeCode
                && l.TransactionId == transactionId
                && l.LedgerTypeId == ControlLedgerType)
            .GroupBy(l => 1)
            .Select(g => g.Sum(l => l.DebitAmountBase) - g.Sum(l => l.CreditAmountBase))
            .FirstOrDefaultAsync(ct);

        return Math.Abs(net);
    }
}