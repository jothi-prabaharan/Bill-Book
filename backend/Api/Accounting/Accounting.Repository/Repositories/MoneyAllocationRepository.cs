using Accounting.Entity.Models;
using Accounting.Entity.Enums;
using Accounting.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Audit;

namespace Accounting.Repository.Repositories;

/// <summary>
/// What a money document's lines are allowed to say about the documents they
/// settle.
///
/// <b>One payment, several documents, one line each.</b> That much T6.2 already
/// gave: the ledger source and the settled document both sit on the line, so
/// ₹50,000 across three bills is three lines and three pairs of ledger legs, each
/// traceable to the bill it cleared. What was missing is that nothing checked the
/// lines were saying anything sensible — and the two mistakes available here are
/// both silent.
///
/// <b>A line naming the wrong kind of document</b> traces a payment to something
/// it did not pay. Worse than a missing link, because a statement reconciling on
/// that link reads as answered.
///
/// <b>Two lines naming the same document</b> leave two ledger rows against one
/// balance. Splitting a payment across two lines of one bill is not partial
/// payment — partial payment is one line for less than the bill — it is a
/// keying slip that no report can undo afterwards.
///
/// <b>What is deliberately not here: how much a document actually owes.</b> That
/// is T5.1, and it needs the bill and the invoice to exist. Until then a line may
/// settle more than its document is for; this refuses the mistakes that can be
/// caught without asking anyone what the balance is.
/// </summary>
public static class MoneyAllocationRepository
{
    /// <summary>
    /// Checks every line's settled document, or returns null when they all pass.
    /// </summary>
    /// <param name="spending">
    /// Which side's source map applies. Spend and receive share this check but not
    /// their catalogue of sources.
    /// </param>
    public static MoneyDocumentResult? Check(
        IReadOnlyList<SaveMoneyLineRequest> lines, bool spending)
    {
        // Keyed on the source as well as the document, because paying a bill and
        // overpaying against it are two different claims on one bill — which is
        // the whole of T6.2's overpayment. Two *bill payment* lines against one
        // bill is the keying slip; a bill payment and its excess are not.
        var claimed = new HashSet<(int, string, long)>();

        for (int index = 0; index < lines.Count; index++)
        {
            SaveMoneyLineRequest line = lines[index];
            int lineNumber = index + 1;

            string? settleable = MoneyPostingMapRepository.Settles(line.LedgerSourceId);
            bool named = line.MappingTransactionTypeCode is { Length: > 0 };

            // Half a mapping is refused by the database, and this reads the type
            // code alone deliberately: a type with no id is caught there with the
            // constraint's own message, and repeating it here would give the same
            // mistake two different explanations.
            if (!named)
            {
                continue;
            }

            string code = line.MappingTransactionTypeCode!.ToUpperInvariant();

            if (settleable is null)
            {
                return new MoneyDocumentResult(
                    MoneyDocumentOutcome.MappingNotSettleable, 0,
                    $"Line {lineNumber} settles nothing — an advance is placed before any "
                        + $"document exists — so it cannot name {code}.");
            }

            if (!string.Equals(code, settleable, StringComparison.Ordinal))
            {
                return new MoneyDocumentResult(
                    MoneyDocumentOutcome.MappingNotSettleable, 0,
                    $"Line {lineNumber} is for something that settles a {settleable}, "
                        + $"so it cannot name a {code}.");
            }

            if (line.MappingTransactionId is long id
                && !claimed.Add((line.LedgerSourceId, code, id)))
            {
                return new MoneyDocumentResult(
                    MoneyDocumentOutcome.MappingRepeated, 0,
                    $"Line {lineNumber} settles {code} {id} the same way an earlier line does. "
                        + $"A payment claims each document once per purpose — put the whole "
                        + $"amount for {code} {id} on one line.");
            }
        }

        return null;
    }

    /// <summary>
    /// What the header settles, given its lines. One document when every line
    /// that names one names the same, and nothing at all otherwise — a payment
    /// across three bills is about three, and picking one of them for the header
    /// would be picking arbitrarily.
    ///
    /// <b>Derived rather than taken from the caller.</b> The header's mapping is a
    /// summary of the lines, not a fact beside them: a screen that sent one while
    /// the lines said another would leave a document whose header and body
    /// disagree, and the header is what a list reads first.
    /// </summary>
    public static (string? TypeCode, long? TransactionId) HeaderMapping(
        IReadOnlyList<SaveMoneyLineRequest> lines)
    {
        (string, long)[] named = [.. lines
            .Where(l => l.MappingTransactionTypeCode is { Length: > 0 }
                && l.MappingTransactionId is not null)
            .Select(l => (l.MappingTransactionTypeCode!.ToUpperInvariant(), l.MappingTransactionId!.Value))
            .Distinct()];

        return named.Length == 1 ? (named[0].Item1, named[0].Item2) : (null, null);
    }

    /// <summary>
    /// Writes the allocation each settling line makes, as a posted money document
    /// takes effect.
    ///
    /// <b>Without it a paid bill stays open.</b> The line names the bill, but the
    /// open-documents read, the Allocate dialog and every outstanding figure count
    /// <c>acc.TransactionRatios</c> and nothing else — so a payment posted against a
    /// bill left it showing the full amount still owed, ready to be paid twice.
    ///
    /// Only a line on a <c>Primary</c> source settles: an overpayment that names the
    /// bill it ran past is a balance of its own, not part of the bill's settlement.
    /// The pair runs the way the Allocate dialog writes it — a credit-side document
    /// is the source and a debit-side one the target — so a payment's bill is the
    /// source and a receipt's invoice is the target. Added to the caller's
    /// transaction; the caller saves.
    /// </summary>
    public static void RecordSettlements(
        AccountingDbContext db,
        string moneyTypeCode,
        long moneyId,
        bool spending,
        DateOnly allocationDate,
        IEnumerable<(int LedgerSourceId, string? TypeCode, long? TransactionId, decimal Amount)> lines)
    {
        var settled = lines
            .Where(l => l.TypeCode is { Length: > 0 } && l.TransactionId is not null && l.Amount > 0m)
            .Where(l => (spending
                    ? MoneyPostingMapRepository.ForSpend(l.LedgerSourceId)
                    : MoneyPostingMapRepository.ForReceive(l.LedgerSourceId))
                ?.SubAccountPurpose == MoneyPostingMapRepository.Primary)
            .GroupBy(l => (Code: l.TypeCode!.ToUpperInvariant(), Id: l.TransactionId!.Value))
            .Select(g => (g.Key.Code, g.Key.Id, Amount: g.Sum(l => l.Amount)));

        foreach ((string code, long id, decimal amount) in settled)
        {
            db.TransactionRatios.Add(new TransactionRatio
            {
                SourceTransactionTypeCode = spending ? code : moneyTypeCode,
                SourceTransactionId = spending ? id : moneyId,
                TargetTransactionTypeCode = spending ? moneyTypeCode : code,
                TargetTransactionId = spending ? moneyId : id,
                Amount = amount,
                AllocationDate = allocationDate,
                AllocatedAt = DateTime.UtcNow,
            });
        }
    }

    /// <summary>
    /// Writes the settlement rows posted payments and receipts should have had,
    /// for those posted before posting wrote them (TK-206). Idempotent: a pair
    /// that has ever had an allocation, live or voided, is left alone, so
    /// neither a claim someone made with Allocate nor one they released is
    /// written again. Runs with the branch seed. Returns how many it wrote.
    /// </summary>
    public static async Task<int> BackfillSettlementsAsync(AccountingDbContext db, CancellationToken ct)
    {
        var spent = await (
            from d in db.SpendMoneyDetails
            join m in db.SpendMoney on d.SpendMoneyId equals m.SpendMoneyId
            where m.Status == MoneyDocumentStatus.Posted && d.MappingTransactionId != null
            select new { MoneyId = m.SpendMoneyId, m.TransactionDate, d.LedgerSourceId, d.MappingTransactionTypeCode, d.MappingTransactionId, d.Amount })
            .ToListAsync(ct);

        var received = await (
            from d in db.ReceiveMoneyDetails
            join m in db.ReceiveMoney on d.ReceiveMoneyId equals m.ReceiveMoneyId
            where m.Status == MoneyDocumentStatus.Posted && d.MappingTransactionId != null
            select new { MoneyId = m.ReceiveMoneyId, m.TransactionDate, d.LedgerSourceId, d.MappingTransactionTypeCode, d.MappingTransactionId, d.Amount })
            .ToListAsync(ct);

        if (spent.Count == 0 && received.Count == 0)
        {
            return 0;
        }

        var touched = await db.TransactionRatios
            .Where(t => t.SourceTransactionTypeCode == "SPM" || t.TargetTransactionTypeCode == "SPM"
                || t.SourceTransactionTypeCode == "RCM" || t.TargetTransactionTypeCode == "RCM")
            .Select(t => new { t.SourceTransactionTypeCode, t.SourceTransactionId, t.TargetTransactionTypeCode, t.TargetTransactionId })
            .ToListAsync(ct);

        HashSet<(string, long, string, long)> seen =
            [.. touched.Select(t => (t.SourceTransactionTypeCode, t.SourceTransactionId, t.TargetTransactionTypeCode, t.TargetTransactionId))];

        int before = db.ChangeTracker.Entries<TransactionRatio>().Count(e => e.State == EntityState.Added);

        foreach (var document in spent.GroupBy(l => (l.MoneyId, l.TransactionDate)))
        {
            RecordSettlements(db, "SPM", document.Key.MoneyId, spending: true, document.Key.TransactionDate,
                document
                    .Where(l => !seen.Contains((l.MappingTransactionTypeCode!.ToUpperInvariant(), l.MappingTransactionId!.Value, "SPM", document.Key.MoneyId)))
                    .Select(l => (l.LedgerSourceId, (string?)l.MappingTransactionTypeCode, l.MappingTransactionId, l.Amount)));
        }

        foreach (var document in received.GroupBy(l => (l.MoneyId, l.TransactionDate)))
        {
            RecordSettlements(db, "RCM", document.Key.MoneyId, spending: false, document.Key.TransactionDate,
                document
                    .Where(l => !seen.Contains(("RCM", document.Key.MoneyId, l.MappingTransactionTypeCode!.ToUpperInvariant(), l.MappingTransactionId!.Value)))
                    .Select(l => (l.LedgerSourceId, (string?)l.MappingTransactionTypeCode, l.MappingTransactionId, l.Amount)));
        }

        int written = db.ChangeTracker.Entries<TransactionRatio>().Count(e => e.State == EntityState.Added) - before;
        if (written > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return written;
    }

    /// <summary>
    /// Releases every allocation a voided money document took part in, at either
    /// end — the ones its own lines made and any made to it afterwards. Voided,
    /// not deleted, as <c>AllocationRepository.RemoveAllocationsAsync</c> does.
    /// </summary>
    public static Task<int> ReleaseSettlementsAsync(
        AccountingDbContext db, string moneyTypeCode, long moneyId, string reason, CancellationToken ct)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        return db.TransactionRatios
            .Where(t => !t.IsVoided
                && ((t.SourceTransactionTypeCode == moneyTypeCode && t.SourceTransactionId == moneyId)
                    || (t.TargetTransactionTypeCode == moneyTypeCode && t.TargetTransactionId == moneyId)))
            .ExecuteUpdateLoggedAsync(db,
                s => s
                    .SetProperty(t => t.IsVoided, true)
                    .SetProperty(t => t.VoidedAt, now)
                    .SetProperty(t => t.VoidReason, reason),
                ct);
    }
}
