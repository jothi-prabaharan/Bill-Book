using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;

namespace Accounting.Api.Services;

/// <summary>
/// Where a branch's books begin. `CLAUDE.md` calls this the highest-risk screen
/// in the system, and the reason is that it is the only document that touches
/// every subledger at once — get it wrong and nothing afterwards is measured
/// from the right place, quietly, forever.
///
/// <b>Opening Balance Equity is the plug, and it netting to zero is the
/// validation.</b> Every line posts as a self-balancing pair against OBE:
/// <c>Dr Bank / Cr OBE</c>, <c>Dr OBE / Cr Accounts Payable</c>. So the ledger
/// balances after every line and OBE accumulates precisely what has not been
/// accounted for yet. When the last piece goes in — capital, retained earnings —
/// it reaches zero. Finalizing with OBE non-zero would invent equity out of a
/// figure somebody forgot to key, and it would balance perfectly while doing it.
///
/// <b>Receivables and payables come across per document, not per contact and
/// certainly not as a lump.</b> The plan says per contact and gives aging as the
/// reason; per contact is only half of it. A contact owing ₹300,000 across
/// invoices of thirty, ninety and a hundred and eighty days ages nothing like one
/// ₹300,000 balance, and an aging report is the first thing anybody opens after a
/// migration. So a line carries the original document's number and date, and a
/// contact has as many lines as they have unpaid documents.
///
/// <b>Stock is Inventory's to record.</b> Accounting sends quantity and unit cost
/// and Inventory seeds the weighted average and posts the value itself. A value
/// Accounting posted would be a second figure against the Inventory account, free
/// to disagree with the stock the items actually carry.
///
/// <b>Finalize is one way.</b> There is no void and no reversal: reversing an
/// opening balance does not restate a transaction, it deletes the starting
/// position every balance since has been measured from. An error found afterwards
/// is corrected the way any error in a closed period is — with a journal entry
/// that leaves a trail.
/// </summary>
public sealed class OpeningBalanceService
{
    private const string TypeCode = "OPB";

    /// <summary><c>mst.LedgerSources</c> 13 — an opening balance.</summary>
    private const int OpeningBalanceSource = 13;

    /// <summary><c>mst.LedgerTypes</c> 3 — the AP/AR/bank control leg.</summary>
    private const int ControlLedgerType = 3;

    /// <summary>The tolerance every balance check here uses: half a paisa.</summary>
    private const decimal Tolerance = 0.005m;

    private readonly AccountingDbContext _db;
    private readonly LedgerPostingService _postings;
    private readonly IInventoryOpeningStock _inventory;
    private readonly INumberGenerator _numbers;
    private readonly IBaseCurrencyProvider _baseCurrency;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public OpeningBalanceService(
        AccountingDbContext db,
        LedgerPostingService postings,
        IInventoryOpeningStock inventory,
        INumberGenerator numbers,
        IBaseCurrencyProvider baseCurrency,
        ITenantContext tenant,
        ICurrentUser user,
        TimeProvider clock)
    {
        _db = db;
        _postings = postings;
        _inventory = inventory;
        _numbers = numbers;
        _baseCurrency = baseCurrency;
        _tenant = tenant;
        _user = user;
        _clock = clock;
    }

    /// <summary>
    /// The branch's opening balance, or null when it has none yet. There is at
    /// most one, so this takes no id.
    /// </summary>
    public async Task<OpeningBalanceView?> GetAsync(CancellationToken ct)
    {
        OpeningBalance? document = await _db.OpeningBalances.FirstOrDefaultAsync(ct);

        if (document is null)
        {
            return null;
        }

        List<OpeningBalanceLineView> lines = await (
            from line in _db.OpeningBalanceLines
            where line.OpeningBalanceId == document.OpeningBalanceId
            join account in _db.Accounts on line.AccountId equals account.AccountId into accounts
            from account in accounts.DefaultIfEmpty()
            orderby line.LineNumber
            select new OpeningBalanceLineView
            {
                OpeningBalanceLineId = line.OpeningBalanceLineId,
                LineNumber = line.LineNumber,
                LineType = line.LineType.ToString(),
                AccountId = line.AccountId,
                AccountCode = account.AccountCode,
                AccountName = account.AccountName,
                ContactId = line.ContactId,
                ItemId = line.ItemId,
                Quantity = line.Quantity,
                UnitCost = line.UnitCost,
                DebitAmount = line.DebitAmount,
                CreditAmount = line.CreditAmount,
                DocumentReference = line.DocumentReference,
                DocumentDate = line.DocumentDate,
                DueDate = line.DueDate,
                LineMemo = line.LineMemo,
            }).ToListAsync(ct);

        return new OpeningBalanceView
        {
            OpeningBalanceId = document.OpeningBalanceId,
            TransactionNo = document.TransactionNo,
            AsOfDate = document.AsOfDate,
            CurrencyCode = document.CurrencyCode,
            Memo = document.Memo,
            Status = document.Status.ToString(),
            FinalizedAt = document.FinalizedAt,
            Lines = lines,
        };
    }

    /// <summary>
    /// Creates the branch's one opening balance, or replaces the draft wholesale
    /// if it already has one.
    ///
    /// <b>Save and re-save is the normal way this document is used.</b> A
    /// migration is keyed over weeks, and it is edited by moving figures between
    /// lines and deleting the ones that turned out to be the same invoice twice —
    /// so lines are rewritten rather than matched up by id, which would be
    /// guesswork against a document nothing points at yet.
    /// </summary>
    public async Task<OpeningBalanceResult> SaveAsync(
        SaveOpeningBalanceRequest request, CancellationToken ct)
    {
        OpeningBalance? document = await _db.OpeningBalances.FirstOrDefaultAsync(ct);

        if (document is { Status: OpeningBalanceStatus.Finalized })
        {
            return new OpeningBalanceResult(
                OpeningBalanceOutcome.AlreadyFinalized, document.OpeningBalanceId,
                "The books have already opened. Correct an opening figure with a journal entry, "
                    + "which leaves a trail; changing this one would move the ground every "
                    + "balance since has been measured from.");
        }

        OpeningBalanceResult? invalid = await ValidateAsync(request, ct);
        if (invalid is not null)
        {
            return invalid;
        }

        if (document is null)
        {
            if (await _baseCurrency.GetBaseCurrencyAsync(ct) is not { } currency)
            {
                return new OpeningBalanceResult(
                    OpeningBalanceOutcome.BaseCurrencyUnavailable, 0,
                    "The branch's base currency could not be read. Nothing was saved.");
            }

            document = new OpeningBalance
            {
                AsOfDate = request.AsOfDate,
                CurrencyCode = currency,
                Memo = request.Memo,
                Status = OpeningBalanceStatus.Draft,
            };

            _db.OpeningBalances.Add(document);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            document.AsOfDate = request.AsOfDate;
            document.Memo = request.Memo;

            await _db.OpeningBalanceLines
                .Where(l => l.OpeningBalanceId == document.OpeningBalanceId)
                .ExecuteDeleteAsync(ct);

            // ExecuteDelete goes straight to the database and the change tracker
            // never hears about it, so a line this context had already read is now
            // tracked with nothing behind it.
            foreach (var stale in _db.ChangeTracker.Entries<OpeningBalanceLine>().ToList())
            {
                stale.State = EntityState.Detached;
            }
        }

        _db.OpeningBalanceLines.AddRange(BuildLines(document.OpeningBalanceId, request.Lines));
        await _db.SaveChangesAsync(ct);

        return new OpeningBalanceResult(OpeningBalanceOutcome.Ok, document.OpeningBalanceId);
    }

    /// <summary>
    /// Deletes the draft. Allowed while nothing has posted, because a migration
    /// abandoned halfway is better thrown away than corrected line by line.
    /// </summary>
    public async Task<OpeningBalanceResult> DeleteAsync(CancellationToken ct)
    {
        OpeningBalance? document = await _db.OpeningBalances.FirstOrDefaultAsync(ct);

        if (document is null)
        {
            return new OpeningBalanceResult(OpeningBalanceOutcome.NotFound);
        }

        if (document.Status != OpeningBalanceStatus.Draft)
        {
            return new OpeningBalanceResult(
                OpeningBalanceOutcome.AlreadyFinalized, document.OpeningBalanceId,
                "The books have already opened, so this can never be deleted.");
        }

        _db.OpeningBalances.Remove(document);
        await _db.SaveChangesAsync(ct);

        return new OpeningBalanceResult(OpeningBalanceOutcome.Ok, document.OpeningBalanceId);
    }

    /// <summary>
    /// Whether the document may be finalized, and if not, what is stopping it.
    ///
    /// <b>Read continuously by the screen, not only when Finalize is pressed.</b>
    /// A migration is keyed over weeks; discovering at the end that the whole
    /// thing is out by ₹4,300 means auditing weeks of work rather than the line
    /// that was just typed.
    /// </summary>
    public async Task<OpeningBalanceReadinessView?> ReadinessAsync(CancellationToken ct)
    {
        OpeningBalance? document = await _db.OpeningBalances.FirstOrDefaultAsync(ct);

        if (document is null)
        {
            return null;
        }

        List<OpeningBalanceLine> lines = await _db.OpeningBalanceLines
            .Where(l => l.OpeningBalanceId == document.OpeningBalanceId)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        var readiness = new OpeningBalanceReadinessView
        {
            TotalDebit = lines.Sum(l => l.DebitAmount),
            TotalCredit = lines.Sum(l => l.CreditAmount),
            InventoryValue = lines
                .Where(l => l.LineType == OpeningBalanceLineType.Item)
                .Sum(l => (l.Quantity ?? 0m) * (l.UnitCost ?? 0m)),
            ContactReceivableLines =
                lines.Count(l => l.LineType == OpeningBalanceLineType.ContactReceivable),
            ContactPayableLines =
                lines.Count(l => l.LineType == OpeningBalanceLineType.ContactPayable),
            ItemLines = lines.Count(l => l.LineType == OpeningBalanceLineType.Item),
        };

        // What OBE would be left holding. Stock counts too: Inventory posts
        // Dr Inventory / Cr OBE, so an unrecorded stock value is a credit OBE has
        // not seen yet — which is why it enters with the opposite sign to a debit
        // line.
        readiness.OpeningBalanceEquity =
            readiness.TotalDebit - readiness.TotalCredit + readiness.InventoryValue;

        if (document.Status == OpeningBalanceStatus.Finalized)
        {
            readiness.Blockers.Add("The books have already opened.");
            return readiness;
        }

        if (lines.Count == 0)
        {
            readiness.Blockers.Add("There is nothing to open the books with.");
        }

        if (Math.Abs(readiness.OpeningBalanceEquity) >= Tolerance)
        {
            readiness.Blockers.Add(
                $"Opening Balance Equity would be left holding "
                    + $"{readiness.OpeningBalanceEquity:0.00}. It has to net to zero — whatever "
                    + "is left is a part of the branch's position that has not been keyed yet, "
                    + "usually capital or retained earnings.");
        }

        // Every control account this document will touch has to exist before it
        // can post, and a missing one is worth naming now rather than at finalize.
        foreach (string missing in await MissingControlAccountsAsync(lines, ct))
        {
            readiness.Blockers.Add(
                $"The chart of accounts has no '{missing}' account, so those lines have "
                    + "nowhere to post.");
        }

        foreach (string missing in await MissingSubAccountsAsync(lines, ct))
        {
            readiness.Blockers.Add(missing);
        }

        return readiness;
    }

    /// <summary>
    /// Go-live. Posts every line, records the opening stock, takes the number and
    /// freezes the document.
    ///
    /// <b>Inventory goes first, then the ledger, then the number and the
    /// status.</b> Inventory is idempotent on the document and line, so a retried
    /// finalize finds the stock already recorded rather than doubling it; the
    /// ledger replaces a posting by key rather than appending one. What must never
    /// happen is the reverse — a document marked open with no ledger rows behind
    /// it, because nothing afterwards would go looking.
    /// </summary>
    public async Task<OpeningBalanceResult> FinalizeAsync(CancellationToken ct)
    {
        OpeningBalance? document = await _db.OpeningBalances.FirstOrDefaultAsync(ct);

        if (document is null)
        {
            return new OpeningBalanceResult(OpeningBalanceOutcome.NotFound);
        }

        if (document.Status != OpeningBalanceStatus.Draft)
        {
            return new OpeningBalanceResult(
                OpeningBalanceOutcome.AlreadyFinalized, document.OpeningBalanceId,
                "The books have already opened.");
        }

        OpeningBalanceReadinessView? readiness = await ReadinessAsync(ct);

        if (readiness is null || !readiness.CanFinalize)
        {
            return new OpeningBalanceResult(
                OpeningBalanceOutcome.NotReady, document.OpeningBalanceId,
                string.Join(" ", readiness?.Blockers ?? ["The opening balance could not be read."]));
        }

        List<OpeningBalanceLine> lines = await _db.OpeningBalanceLines
            .Where(l => l.OpeningBalanceId == document.OpeningBalanceId)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        OpeningStockOutcome stock = await _inventory.RecordAsync(
            document.OpeningBalanceId,
            document.AsOfDate,
            [.. lines
                .Where(l => l.LineType == OpeningBalanceLineType.Item)
                .Select(l => new OpeningStockRequestLine(
                    l.LineNumber, l.ItemId!.Value, l.Quantity!.Value, l.UnitCost!.Value,
                    WarehouseId: null))],
            ct);

        if (!stock.Recorded)
        {
            return new OpeningBalanceResult(
                OpeningBalanceOutcome.InventoryUnavailable, document.OpeningBalanceId,
                stock.Detail ?? "The opening stock could not be recorded, so the books "
                    + "have not opened. Nothing was posted and this can be retried.");
        }

        PostLedgerResult posted = await _postings.PostAsync(
            BuildPosting(document, lines), ct);

        if (posted.Outcome != PostLedgerOutcome.Ok)
        {
            return new OpeningBalanceResult(
                OpeningBalanceOutcome.PostingRefused, document.OpeningBalanceId,
                posted.Detail ?? "The ledger did not accept the opening balance.");
        }

        // The stock Inventory actually recorded against the value this document
        // says it has. They can only disagree if a line was refused and reported
        // as recorded, which is the failure this check exists to catch — a
        // migration that ties everywhere except the one place nobody looks.
        if (Math.Abs(stock.Value - readiness.InventoryValue) >= Tolerance)
        {
            return new OpeningBalanceResult(
                OpeningBalanceOutcome.NotReady, document.OpeningBalanceId,
                $"Inventory recorded stock worth {stock.Value:0.00} but this document says "
                    + $"{readiness.InventoryValue:0.00}. The books have not opened.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        NumberAllocation allocation = await _numbers.NextAsync(TypeCode, document.AsOfDate, ct);

        document.TransactionNo = allocation.Code;
        document.Status = OpeningBalanceStatus.Finalized;
        document.FinalizedAt = _clock.GetUtcNow();
        document.FinalizedBy = _user.UserId;

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new OpeningBalanceResult(OpeningBalanceOutcome.Ok, document.OpeningBalanceId);
    }

    /// <summary>
    /// Every line as a self-balancing pair against Opening Balance Equity. Item
    /// lines are absent — Inventory posts those, at the cost it seeded the
    /// weighted average with.
    /// </summary>
    private PostLedgerRequest BuildPosting(
        OpeningBalance document, IReadOnlyList<OpeningBalanceLine> lines)
    {
        (Guid customerId, Guid orgId) = _tenant.Require();

        var legs = new List<LedgerLegRequest>(lines.Count * 2);

        foreach (OpeningBalanceLine line in lines)
        {
            if (line.LineType == OpeningBalanceLineType.Item)
            {
                continue;
            }

            bool receivable = line.LineType == OpeningBalanceLineType.ContactReceivable;
            bool payable = line.LineType == OpeningBalanceLineType.ContactPayable;

            legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = ControlLedgerType,
                LedgerSourceId = OpeningBalanceSource,
                TransactionDetailId = line.LineNumber,
                AccountId = line.AccountId,
                AccountSystemName = receivable
                    ? SystemAccountNames.Of(SystemAccount.AccountsReceivable)
                    : payable ? SystemAccountNames.Of(SystemAccount.AccountsPayable) : null,
                SubAccountReferenceType = receivable || payable
                    ? SubAccountReferenceType.Contact
                    : null,
                SubAccountReferenceId = line.ContactId,
                DebitAmount = line.DebitAmount,
                CreditAmount = line.CreditAmount,
                TransactionDesc = Describe(line),
            });

            // The other half. Every line is its own balanced pair rather than one
            // OBE leg for the document, so a ledger row on the equity side can be
            // traced to the line that produced it — which is the only way anyone
            // finds the wrong figure afterwards.
            legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = ControlLedgerType,
                LedgerSourceId = OpeningBalanceSource,
                TransactionDetailId = line.LineNumber,
                AccountSystemName = SystemAccountNames.Of(SystemAccount.OpeningBalanceEquity),
                DebitAmount = line.CreditAmount,
                CreditAmount = line.DebitAmount,
                TransactionDesc = Describe(line),
            });
        }

        return new PostLedgerRequest
        {
            CustomerId = customerId,
            OrgId = orgId,
            TransactionTypeCode = TypeCode,
            TransactionId = document.OpeningBalanceId,
            LedgerDate = document.AsOfDate,
            SourceDocumentId = document.OpeningBalanceId,
            Legs = legs,
        };
    }

    /// <summary>
    /// What a ledger row says it came from. The old document's reference when
    /// there is one, because that is what somebody matches against when the
    /// contact queries the balance.
    /// </summary>
    private static string? Describe(OpeningBalanceLine line) =>
        line.DocumentReference is { Length: > 0 } reference
            ? $"Opening balance — {reference}"
            : line.LineMemo is { Length: > 0 } memo ? memo : "Opening balance";

    private async Task<OpeningBalanceResult?> ValidateAsync(
        SaveOpeningBalanceRequest request, CancellationToken ct)
    {
        for (int index = 0; index < request.Lines.Count; index++)
        {
            SaveOpeningBalanceLineRequest line = request.Lines[index];
            int lineNumber = index + 1;

            // The database enforces the shape too — these are the same rules with
            // a reason attached, because a constraint violation names a check
            // constraint and a user does not know what one is.
            string? wrong = line.LineType switch
            {
                OpeningBalanceLineType.GlAccount when line.AccountId is null =>
                    "has to name an account",
                OpeningBalanceLineType.GlAccount when line.ContactId is not null
                    || line.ItemId is not null =>
                    "is an account balance, so it cannot also name a contact or an item",

                OpeningBalanceLineType.ContactReceivable or OpeningBalanceLineType.ContactPayable
                    when line.ContactId is null => "has to name the contact it is owed by or to",
                OpeningBalanceLineType.ContactReceivable or OpeningBalanceLineType.ContactPayable
                    when line.AccountId is not null =>
                    "posts to receivables or payables, so it cannot choose its own account",

                OpeningBalanceLineType.Item when line.ItemId is null => "has to name the item",
                OpeningBalanceLineType.Item when line.Quantity is null or <= 0
                    || line.UnitCost is null =>
                    "has to say how much stock there is and what it cost — the cost is what "
                        + "seeds the weighted average",
                OpeningBalanceLineType.Item when line.DebitAmount != 0 || line.CreditAmount != 0 =>
                    "is stock, so its value is quantity times cost rather than an amount keyed "
                        + "beside them",

                _ when line.LineType != OpeningBalanceLineType.Item
                    && (line.DebitAmount > 0) == (line.CreditAmount > 0) =>
                    "is a debit or a credit, never both or neither",

                _ => null,
            };

            if (wrong is not null)
            {
                return new OpeningBalanceResult(
                    OpeningBalanceOutcome.LineShapeInvalid, 0, $"Line {lineNumber} {wrong}.");
            }
        }

        long[] accountIds = [.. request.Lines
            .Where(l => l.AccountId is not null)
            .Select(l => l.AccountId!.Value)
            .Distinct()];

        if (accountIds.Length > 0)
        {
            List<Account> accounts = await _db.Accounts
                .Where(a => accountIds.Contains(a.AccountId))
                .ToListAsync(ct);

            if (accounts.Find(a => a.IsLock || !a.IsActive) is { } frozen)
            {
                return new OpeningBalanceResult(
                    OpeningBalanceOutcome.AccountUnusable, 0,
                    $"'{frozen.AccountName}' is frozen for posting, so an opening balance "
                        + "cannot be brought in against it.");
            }

            if (accounts.Count != accountIds.Length)
            {
                return new OpeningBalanceResult(
                    OpeningBalanceOutcome.AccountUnusable, 0,
                    "One of the lines names an account this branch does not have.");
            }
        }

        return null;
    }

    /// <summary>Control accounts this document needs and the chart does not have.</summary>
    private async Task<List<string>> MissingControlAccountsAsync(
        IReadOnlyList<OpeningBalanceLine> lines, CancellationToken ct)
    {
        var wanted = new List<string>();

        if (lines.Count > 0)
        {
            // Every line posts its other half against equity, so this one is
            // needed even by a document of nothing but bank balances.
            wanted.Add(SystemAccountNames.Of(SystemAccount.OpeningBalanceEquity));
        }

        if (lines.Any(l => l.LineType == OpeningBalanceLineType.ContactReceivable))
        {
            wanted.Add(SystemAccountNames.Of(SystemAccount.AccountsReceivable));
        }

        if (lines.Any(l => l.LineType == OpeningBalanceLineType.ContactPayable))
        {
            wanted.Add(SystemAccountNames.Of(SystemAccount.AccountsPayable));
        }

        List<string> present = await _db.Accounts
            .Where(a => a.AccountSystemName != null && wanted.Contains(a.AccountSystemName)
                && a.IsActive && !a.IsLock)
            .Select(a => a.AccountSystemName!)
            .ToListAsync(ct);

        return [.. wanted.Except(present)];
    }

    /// <summary>
    /// Contacts named on a receivable or payable line that have no sub-account
    /// under the control account it posts to.
    ///
    /// <b>This is the subledger tie, checked before the posting rather than
    /// after.</b> A line that reached the control account with no sub-account
    /// beneath it balances perfectly and leaves the contact owing nothing — so
    /// the statement, the aging report and the first receipt against it are all
    /// wrong, and the trial balance says everything is fine.
    /// </summary>
    private async Task<List<string>> MissingSubAccountsAsync(
        IReadOnlyList<OpeningBalanceLine> lines, CancellationToken ct)
    {
        var blockers = new List<string>();

        foreach ((OpeningBalanceLineType type, SystemAccount control) in new[]
        {
            (OpeningBalanceLineType.ContactReceivable, SystemAccount.AccountsReceivable),
            (OpeningBalanceLineType.ContactPayable, SystemAccount.AccountsPayable),
        })
        {
            long[] contactIds = [.. lines
                .Where(l => l.LineType == type && l.ContactId is not null)
                .Select(l => l.ContactId!.Value)
                .Distinct()];

            if (contactIds.Length == 0)
            {
                continue;
            }

            string systemName = SystemAccountNames.Of(control);

            List<long> provisioned = await (
                from sub in _db.SubAccounts
                join account in _db.Accounts on sub.AccountId equals account.AccountId
                where account.AccountSystemName == systemName
                    && sub.ReferenceType == SubAccountReferenceType.Contact
                    && sub.Purpose == SubAccountPurpose.Primary
                    && sub.IsActive
                    && contactIds.Contains(sub.ReferenceId)
                select sub.ReferenceId).ToListAsync(ct);

            long[] missing = [.. contactIds.Except(provisioned)];

            if (missing.Length > 0)
            {
                blockers.Add(
                    $"{missing.Length} contact(s) have no '{systemName}' balance to open — "
                        + $"ids {string.Join(", ", missing.Take(5))}"
                        + (missing.Length > 5 ? ", …" : string.Empty)
                        + ". Open each contact and provision its accounts, then come back.");
            }
        }

        return blockers;
    }

    private static IEnumerable<OpeningBalanceLine> BuildLines(
        long openingBalanceId, List<SaveOpeningBalanceLineRequest> lines) =>
        lines.Select((line, index) => new OpeningBalanceLine
        {
            OpeningBalanceId = openingBalanceId,
            LineNumber = index + 1,
            LineType = line.LineType,
            AccountId = line.AccountId,
            ContactId = line.ContactId,
            ItemId = line.ItemId,
            Quantity = line.Quantity,
            UnitCost = line.UnitCost,
            DebitAmount = line.DebitAmount,
            CreditAmount = line.CreditAmount,
            DocumentReference = line.DocumentReference,
            DocumentDate = line.DocumentDate,
            DueDate = line.DueDate,
            LineMemo = line.LineMemo,
        });
}
