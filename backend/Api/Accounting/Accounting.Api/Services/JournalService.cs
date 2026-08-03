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
/// The manual journal: the document a person writes when no other document
/// fits, and the only screen in the product that names two accounts directly.
///
/// <b>Draft, Posted, Reversed, and no way back.</b> A draft is free — it may be
/// unbalanced, it holds no number and it touches no ledger. Posting is the one
/// irreversible act: it takes a number, writes the ledger rows and freezes the
/// entry. From there the only correction is a reversing entry that stands beside
/// the original, because a general ledger whose history can be edited is not
/// evidence of anything.
///
/// <b>Everything the post does is one transaction.</b> The number allocation,
/// the status flip and the ledger rows all succeed together or none of them
/// does — otherwise a failed posting leaves a number consumed in a series that
/// has to be gapless, or worse, a journal marked posted with no rows behind it.
/// </summary>
public sealed class JournalService
{
    /// <summary>
    /// <c>mst.LedgerSources</c> 12 — Journal. What marks these rows as
    /// hand-written when a report asks where a balance came from.
    /// </summary>
    private const int JournalLedgerSource = 12;

    /// <summary>
    /// <c>mst.LedgerTypes</c> 3 — CONTROL. A manual journal line has no item, no
    /// tax rate, no cost layer and no rounding behind it; it is a movement on the
    /// account itself, which is what the control leg means. Every line shares it,
    /// so the whole entry replaces as one key set on a retried post.
    /// </summary>
    private const int ControlLedgerType = 3;

    /// <summary><c>mst.TransactionTypes</c> — the manual journal's own code.</summary>
    private const string JournalTypeCode = "JRN";

    private readonly AccountingDbContext _db;
    private readonly LedgerPostingService _postings;
    private readonly INumberGenerator _numbers;
    private readonly IBaseCurrencyProvider _baseCurrency;
    private readonly ICurrentUser _user;
    private readonly ITenantContext _tenant;
    private readonly TimeProvider _clock;

    public JournalService(
        AccountingDbContext db,
        LedgerPostingService postings,
        INumberGenerator numbers,
        IBaseCurrencyProvider baseCurrency,
        ICurrentUser user,
        ITenantContext tenant,
        TimeProvider clock)
    {
        _db = db;
        _postings = postings;
        _numbers = numbers;
        _baseCurrency = baseCurrency;
        _user = user;
        _tenant = tenant;
        _clock = clock;
    }

    public async Task<IReadOnlyList<JournalListItem>> ListAsync(
        string? status, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        IQueryable<Journal> query = _db.Journals;

        if (Enum.TryParse(status, ignoreCase: true, out JournalStatus wanted))
        {
            query = query.Where(j => j.Status == wanted);
        }

        if (from is DateOnly start)
        {
            query = query.Where(j => j.JournalDate >= start);
        }

        if (to is DateOnly end)
        {
            query = query.Where(j => j.JournalDate <= end);
        }

        // Newest first, which is where the work is.
        return await Summarize(query
            .OrderByDescending(j => j.JournalDate)
            .ThenByDescending(j => j.JournalId))
            .ToListAsync(ct);
    }

    public async Task<JournalDetailView?> GetAsync(long journalId, CancellationToken ct)
    {
        JournalListItem? header = await Summarize(
            _db.Journals.Where(j => j.JournalId == journalId))
            .FirstOrDefaultAsync(ct);

        if (header is null)
        {
            return null;
        }

        // A LINQ join rather than a navigation: the account and sub-account names
        // are display columns on the line, and loading two entity graphs to read
        // one string each is how a twenty-line entry becomes forty queries.
        List<JournalLineView> lines = await (
            from d in _db.JournalDetails
            join a in _db.Accounts on d.AccountId equals a.AccountId
            join s in _db.SubAccounts on d.SubAccountId equals s.SubAccountId into subs
            from s in subs.DefaultIfEmpty()
            where d.JournalId == journalId
            orderby d.LineNumber
            select new JournalLineView
            {
                JournalDetailId = d.JournalDetailId,
                LineNumber = d.LineNumber,
                AccountId = d.AccountId,
                AccountCode = a.AccountCode,
                AccountName = a.AccountName,
                SubAccountId = d.SubAccountId,
                SubAccountName = s == null ? null : s.SubAccountName,
                DebitAmount = d.DebitAmount,
                CreditAmount = d.CreditAmount,
                DebitAmountBase = d.DebitAmountBase,
                CreditAmountBase = d.CreditAmountBase,
                LineMemo = d.LineMemo,
                ReversesJournalDetailId = d.ReversesJournalDetailId,
                ReversedByJournalDetailId = d.ReversedByJournalDetailId,
            }).ToListAsync(ct);

        return new JournalDetailView
        {
            JournalId = header.JournalId,
            JournalNo = header.JournalNo,
            JournalDate = header.JournalDate,
            CurrencyCode = header.CurrencyCode,
            ExchangeRate = header.ExchangeRate,
            Reference = header.Reference,
            Memo = header.Memo,
            Status = header.Status,
            PostedAt = header.PostedAt,
            LineCount = header.LineCount,
            TotalDebit = header.TotalDebit,
            TotalCredit = header.TotalCredit,
            ReversesJournalId = header.ReversesJournalId,
            ReversesJournalNo = header.ReversesJournalNo,
            ReversedByJournalId = header.ReversedByJournalId,
            ReversedByJournalNo = header.ReversedByJournalNo,
            Lines = lines,
        };
    }

    public async Task<SaveJournalResult> CreateAsync(
        SaveJournalRequest request, CancellationToken ct)
    {
        (string? currency, decimal rate) = await ResolveCurrencyAsync(request, ct);

        if (currency is null)
        {
            return new SaveJournalResult(SaveJournalOutcome.BaseCurrencyUnavailable);
        }

        SaveJournalResult? invalid = await ValidateLinesAsync(request.Lines, ct);
        if (invalid is not null)
        {
            return invalid;
        }

        var journal = new Journal
        {
            JournalNo = null,
            JournalDate = request.JournalDate,
            CurrencyCode = currency,
            ExchangeRate = rate,
            Reference = request.Reference,
            Memo = request.Memo,
            TransactionTypeCode = JournalTypeCode,
            Status = JournalStatus.Draft,
        };

        _db.Journals.Add(journal);
        await _db.SaveChangesAsync(ct);

        _db.JournalDetails.AddRange(BuildLines(journal, request.Lines));
        await _db.SaveChangesAsync(ct);

        return new SaveJournalResult(SaveJournalOutcome.Ok, journal.JournalId);
    }

    /// <summary>
    /// Replaces a draft's header and lines wholesale. Lines are rewritten rather
    /// than matched up: a journal is edited by moving amounts between lines as
    /// often as by changing one, and reconciling that against line ids would be
    /// guesswork about which line the user meant.
    /// </summary>
    public async Task<SaveJournalResult> UpdateAsync(
        long journalId, SaveJournalRequest request, CancellationToken ct)
    {
        Journal? journal = await _db.Journals
            .FirstOrDefaultAsync(j => j.JournalId == journalId, ct);

        if (journal is null)
        {
            return new SaveJournalResult(SaveJournalOutcome.NotFound);
        }

        if (journal.Status != JournalStatus.Draft)
        {
            return new SaveJournalResult(SaveJournalOutcome.NotDraft);
        }

        (string? currency, decimal rate) = await ResolveCurrencyAsync(request, ct);

        if (currency is null)
        {
            return new SaveJournalResult(SaveJournalOutcome.BaseCurrencyUnavailable);
        }

        SaveJournalResult? invalid = await ValidateLinesAsync(request.Lines, ct);
        if (invalid is not null)
        {
            return invalid;
        }

        journal.JournalDate = request.JournalDate;
        journal.CurrencyCode = currency;
        journal.ExchangeRate = rate;
        journal.Reference = request.Reference;
        journal.Memo = request.Memo;

        await _db.JournalDetails
            .Where(d => d.JournalId == journalId)
            .ExecuteDeleteAsync(ct);

        // ExecuteDelete goes straight to the database and the change tracker
        // never hears about it. Any line this context had already seen is now a
        // tracked row with nothing behind it, and the next SaveChanges — here or
        // in a later call on the same context — would try to delete it a second
        // time and fail on a row count of zero.
        Forget(journalId);

        _db.JournalDetails.AddRange(BuildLines(journal, request.Lines));
        await _db.SaveChangesAsync(ct);

        return new SaveJournalResult(SaveJournalOutcome.Ok, journalId);
    }

    /// <summary>Deletes a draft. A posted entry is reversed, never deleted.</summary>
    public async Task<SaveJournalResult> DeleteAsync(long journalId, CancellationToken ct)
    {
        Journal? journal = await _db.Journals
            .FirstOrDefaultAsync(j => j.JournalId == journalId, ct);

        if (journal is null)
        {
            return new SaveJournalResult(SaveJournalOutcome.NotFound);
        }

        if (journal.Status != JournalStatus.Draft)
        {
            return new SaveJournalResult(SaveJournalOutcome.NotDraft);
        }

        _db.Journals.Remove(journal);
        await _db.SaveChangesAsync(ct);

        return new SaveJournalResult(SaveJournalOutcome.Ok, journalId);
    }

    /// <summary>
    /// Posts a draft: takes its number, writes its ledger rows and freezes it.
    ///
    /// All of it in one transaction, and the order inside matters less than the
    /// fact that there is only one. <c>NumberGenerator</c> allocates with a
    /// guarded update that joins whatever transaction is open, so a post that
    /// fails anywhere gives the number back rather than leaving a hole in a
    /// series an auditor will ask about.
    /// </summary>
    public async Task<SaveJournalResult> PostAsync(long journalId, CancellationToken ct)
    {
        Journal? journal = await _db.Journals
            .FirstOrDefaultAsync(j => j.JournalId == journalId, ct);

        if (journal is null)
        {
            return new SaveJournalResult(SaveJournalOutcome.NotFound);
        }

        if (journal.Status != JournalStatus.Draft)
        {
            return new SaveJournalResult(SaveJournalOutcome.NotDraft);
        }

        List<JournalDetail> lines = await _db.JournalDetails
            .Where(d => d.JournalId == journalId)
            .OrderBy(d => d.LineNumber)
            .ToListAsync(ct);

        if (lines.Count == 0)
        {
            return new SaveJournalResult(SaveJournalOutcome.NoLines, journalId);
        }

        // The domain guard. It is here rather than only in the database so the
        // user gets the difference back and can fix it, rather than a constraint
        // violation naming a trigger.
        //
        // Two checks stand behind this one: the ledger door checks the legs it
        // is handed, and a deferred trigger checks both the journal and the
        // ledger at commit. CLAUDE.md also describes a SaveChangesInterceptor
        // doing the same — that one is not built, for either table.
        decimal debits = lines.Sum(l => l.DebitAmountBase);
        decimal credits = lines.Sum(l => l.CreditAmountBase);

        if (debits != credits)
        {
            return new SaveJournalResult(
                SaveJournalOutcome.Unbalanced, journalId,
                $"Debits total {debits:0.00} and credits total {credits:0.00}; "
                    + $"the entry is out by {Math.Abs(debits - credits):0.00}.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        NumberAllocation allocation = await _numbers.NextAsync(
            JournalTypeCode, journal.JournalDate, ct);

        journal.JournalNo = allocation.Code;
        journal.Status = JournalStatus.Posted;
        journal.PostedAt = _clock.GetUtcNow();
        journal.PostedBy = _user.UserId;

        await _db.SaveChangesAsync(ct);

        PostLedgerResult posted = await _postings.PostAsync(
            BuildPosting(journal, lines), ct);

        if (posted.Outcome != PostLedgerOutcome.Ok)
        {
            // Nothing is half-done: the number goes back, the status stays Draft
            // and the ledger is untouched, because none of it was ever committed.
            await tx.RollbackAsync(ct);

            return new SaveJournalResult(
                SaveJournalOutcome.PostingRefused, journalId,
                posted.Detail ?? $"The ledger refused the posting: {posted.Outcome}.");
        }

        await tx.CommitAsync(ct);

        return new SaveJournalResult(SaveJournalOutcome.Ok, journalId);
    }

    /// <summary>
    /// Reverses a posted entry with an offsetting one: same accounts, same
    /// amounts, debits and credits swapped.
    ///
    /// <b>The pairing is per line as well as per header.</b> Both journals point
    /// at each other, and so does every line at the line it offsets. Without the
    /// line pairs a reversal that missed a line would still balance and still
    /// look complete — nothing else in the system would catch it.
    /// </summary>
    public async Task<SaveJournalResult> ReverseAsync(
        long journalId, ReverseJournalRequest request, CancellationToken ct)
    {
        Journal? original = await _db.Journals
            .FirstOrDefaultAsync(j => j.JournalId == journalId, ct);

        if (original is null)
        {
            return new SaveJournalResult(SaveJournalOutcome.NotFound);
        }

        if (original.Status == JournalStatus.Draft)
        {
            return new SaveJournalResult(SaveJournalOutcome.NotPosted);
        }

        if (original.Status == JournalStatus.Reversed || original.ReversedByJournalId is not null)
        {
            return new SaveJournalResult(SaveJournalOutcome.AlreadyReversed, journalId);
        }

        List<JournalDetail> lines = await _db.JournalDetails
            .Where(d => d.JournalId == journalId)
            .OrderBy(d => d.LineNumber)
            .ToListAsync(ct);

        if (lines.Count == 0)
        {
            return new SaveJournalResult(SaveJournalOutcome.NoLines, journalId);
        }

        DateOnly reversalDate = request.ReversalDate ?? original.JournalDate;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        NumberAllocation allocation = await _numbers.NextAsync(
            JournalTypeCode, reversalDate, ct);

        // The rate travels with the reversal rather than being re-read. An
        // offsetting entry at today's rate would not offset anything — it would
        // leave a residue in base currency that no account owns.
        var reversal = new Journal
        {
            JournalNo = allocation.Code,
            JournalDate = reversalDate,
            CurrencyCode = original.CurrencyCode,
            ExchangeRate = original.ExchangeRate,
            Reference = request.Reference ?? original.Reference,
            Memo = request.Memo ?? $"Reversal of {original.JournalNo}",
            TransactionTypeCode = JournalTypeCode,
            Status = JournalStatus.Posted,
            PostedAt = _clock.GetUtcNow(),
            PostedBy = _user.UserId,
            ReversesJournalId = original.JournalId,
        };

        _db.Journals.Add(reversal);
        await _db.SaveChangesAsync(ct);

        // Sides swapped, amounts identical. Written as an offsetting entry and
        // never as a negative one — a negative debit would make every column in
        // the ledger unsummable without knowing who wrote it.
        var reversalLines = lines.Select(l => new JournalDetail
        {
            JournalId = reversal.JournalId,
            LineNumber = l.LineNumber,
            AccountId = l.AccountId,
            SubAccountId = l.SubAccountId,
            DebitAmount = l.CreditAmount,
            CreditAmount = l.DebitAmount,
            DebitAmountBase = l.CreditAmountBase,
            CreditAmountBase = l.DebitAmountBase,
            LineMemo = l.LineMemo,
            ReversesJournalDetailId = l.JournalDetailId,
        }).ToList();

        _db.JournalDetails.AddRange(reversalLines);
        await _db.SaveChangesAsync(ct);

        // The back-pointers, now that both sides have ids.
        original.Status = JournalStatus.Reversed;
        original.ReversedByJournalId = reversal.JournalId;

        foreach (JournalDetail line in lines)
        {
            line.ReversedByJournalDetailId = reversalLines
                .First(r => r.ReversesJournalDetailId == line.JournalDetailId)
                .JournalDetailId;
        }

        await _db.SaveChangesAsync(ct);

        PostLedgerResult posted = await _postings.PostAsync(
            BuildPosting(reversal, reversalLines), ct);

        if (posted.Outcome != PostLedgerOutcome.Ok)
        {
            await tx.RollbackAsync(ct);

            return new SaveJournalResult(
                SaveJournalOutcome.PostingRefused, journalId,
                posted.Detail ?? $"The ledger refused the reversal: {posted.Outcome}.");
        }

        await tx.CommitAsync(ct);

        return new SaveJournalResult(SaveJournalOutcome.Ok, reversal.JournalId);
    }

    /// <summary>
    /// The journal as a ledger posting: one leg per line, all under the entry's
    /// own document key, with the journal id carried through so a ledger row
    /// drills back to the entry rather than only to a type and a number.
    /// </summary>
    private PostLedgerRequest BuildPosting(Journal journal, List<JournalDetail> lines) =>
        new()
        {
            CustomerId = _tenant.CustomerId ?? Guid.Empty,
            OrgId = _tenant.OrgId ?? Guid.Empty,
            TransactionTypeCode = JournalTypeCode,
            TransactionId = journal.JournalId,
            LedgerDate = journal.JournalDate,
            CurrencyCode = journal.CurrencyCode,
            ExchangeRate = journal.ExchangeRate,
            SourceDocumentId = journal.JournalId,
            JournalId = journal.JournalId,
            Legs = [.. lines.Select(l => new LedgerLegRequest
            {
                LedgerTypeId = ControlLedgerType,

                // Every line of a hand-written entry is a manual journal. A
                // document that can be two things at once — an overpayment, say —
                // varies this per leg; this one cannot.
                LedgerSourceId = JournalLedgerSource,

                // The line number, not the detail id — a reader looking at the
                // ledger sees the line they typed rather than a surrogate key.
                TransactionDetailId = l.LineNumber,
                AccountId = l.AccountId,
                SubAccountId = l.SubAccountId,
                DebitAmount = l.DebitAmount,
                CreditAmount = l.CreditAmount,
                TransactionDesc = l.LineMemo ?? journal.Reference ?? journal.Memo,
            })],
        };

    /// <summary>
    /// Drops a journal's lines from the change tracker, after a statement that
    /// removed them without the tracker's knowledge.
    /// </summary>
    private void Forget(long journalId)
    {
        foreach (var entry in _db.ChangeTracker.Entries<JournalDetail>()
            .Where(e => e.Entity.JournalId == journalId)
            .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    /// <summary>
    /// Turns request lines into rows, numbering them from one and converting each
    /// to base currency. Line numbers are assigned here rather than accepted from
    /// the caller: they are unique per journal, and a screen that reorders rows
    /// should not have to renumber them.
    /// </summary>
    private static IEnumerable<JournalDetail> BuildLines(
        Journal journal, List<SaveJournalLineRequest> lines) =>
        lines.Select((line, index) => new JournalDetail
        {
            JournalId = journal.JournalId,
            LineNumber = index + 1,
            AccountId = line.AccountId,
            SubAccountId = line.SubAccountId,
            DebitAmount = line.DebitAmount,
            CreditAmount = line.CreditAmount,
            DebitAmountBase = Base(line.DebitAmount, journal.ExchangeRate),
            CreditAmountBase = Base(line.CreditAmount, journal.ExchangeRate),
            LineMemo = line.LineMemo,
        });

    /// <summary>
    /// Two decimals, away from zero — the same rule the ledger uses, because the
    /// two have to agree to the paisa or the trigger refuses the posting.
    /// </summary>
    private static decimal Base(decimal amount, decimal rate) =>
        rate == 1m ? amount : Math.Round(amount / rate, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The currency to record against, and the rate to convert at. A supplied
    /// currency is taken with its supplied rate; otherwise the branch's own
    /// currency at 1. Never a guessed default — a posting stamped with the wrong
    /// currency is wrong in a total nobody re-reads.
    /// </summary>
    private async Task<(string? Currency, decimal Rate)> ResolveCurrencyAsync(
        SaveJournalRequest request, CancellationToken ct)
    {
        if (request.CurrencyCode is { Length: 3 } supplied)
        {
            decimal rate = request.ExchangeRate is decimal given && given > 0 ? given : 1m;
            return (supplied.ToUpperInvariant(), rate);
        }

        return (await _baseCurrency.GetBaseCurrencyAsync(ct), 1m);
    }

    /// <summary>
    /// What a line has to satisfy before it is saved, even as a draft. Balance is
    /// not among them — a twelve-line accrual is out of balance for eleven of
    /// them, and a draft that refused to save until it balanced would force the
    /// whole entry to be typed in one sitting.
    ///
    /// Returns null when every line is acceptable.
    /// </summary>
    private async Task<SaveJournalResult?> ValidateLinesAsync(
        List<SaveJournalLineRequest> lines, CancellationToken ct)
    {
        foreach (SaveJournalLineRequest line in lines)
        {
            if ((line.DebitAmount == 0) == (line.CreditAmount == 0))
            {
                return new SaveJournalResult(
                    SaveJournalOutcome.LineNotExclusive, 0,
                    "Every line is a debit or a credit — never both, and never neither.");
            }

            if (line.DebitAmount < 0 || line.CreditAmount < 0)
            {
                return new SaveJournalResult(
                    SaveJournalOutcome.LineNotExclusive, 0,
                    "An amount is negative. A reversal is an offsetting entry, not a negative one.");
            }
        }

        List<long> accountIds = [.. lines.Select(l => l.AccountId).Distinct()];

        List<Account> accounts = await _db.Accounts
            .Where(a => accountIds.Contains(a.AccountId))
            .ToListAsync(ct);

        foreach (long accountId in accountIds)
        {
            Account? account = accounts.FirstOrDefault(a => a.AccountId == accountId);

            if (account is null)
            {
                return new SaveJournalResult(
                    SaveJournalOutcome.AccountMissing, 0,
                    $"Account {accountId} is not in this branch's chart of accounts.");
            }

            if (!account.IsActive || account.IsLock)
            {
                return new SaveJournalResult(
                    SaveJournalOutcome.AccountNotPostable, 0,
                    $"'{account.AccountName}' is frozen for posting.");
            }

            // A seeded control account is driven by its subledger — receivables
            // by invoices, inventory by stock movements — so a hand entry against
            // one puts the control account and its subledger out of step with
            // nothing to reconcile them. IsJE is the deliberate exception, set on
            // the seeded accounts that are meant to be journalled and openable on
            // the others by someone who knows what it costs.
            if (account.IsSystemDefault && !account.IsJE)
            {
                return new SaveJournalResult(
                    SaveJournalOutcome.AccountNotPostable, 0,
                    $"'{account.AccountName}' is a control account maintained by its own "
                        + "subledger, so it is not open to hand entries.");
            }
        }

        List<long> subAccountIds = [.. lines
            .Where(l => l.SubAccountId is not null)
            .Select(l => l.SubAccountId!.Value)
            .Distinct()];

        if (subAccountIds.Count == 0)
        {
            return null;
        }

        List<SubAccount> subAccounts = await _db.SubAccounts
            .Where(s => subAccountIds.Contains(s.SubAccountId))
            .ToListAsync(ct);

        foreach (SaveJournalLineRequest line in lines.Where(l => l.SubAccountId is not null))
        {
            SubAccount? sub = subAccounts.FirstOrDefault(s => s.SubAccountId == line.SubAccountId);

            // The sub-account has to hang from the line's own account. A contact's
            // receivable sub-account under a payable line would post a balance
            // that reconciles against neither.
            if (sub is null || sub.AccountId != line.AccountId)
            {
                return new SaveJournalResult(
                    SaveJournalOutcome.SubAccountMismatch, 0,
                    "A line's sub-account has to sit under that line's own account.");
            }
        }

        return null;
    }

    /// <summary>
    /// The header projection, written once and used by both the list and the
    /// detail read.
    ///
    /// The totals are correlated subqueries rather than a load-and-sum: a list of
    /// journals showing its own debit and credit totals is the whole point of the
    /// screen, and fetching every line of every entry to add them up in C# is the
    /// version of this that gets slow at exactly the moment there is enough data
    /// for anyone to care.
    /// </summary>
    private IQueryable<JournalListItem> Summarize(IQueryable<Journal> journals) =>
        journals.Select(j => new JournalListItem
        {
            JournalId = j.JournalId,
            JournalNo = j.JournalNo,
            JournalDate = j.JournalDate,
            CurrencyCode = j.CurrencyCode,
            ExchangeRate = j.ExchangeRate,
            Reference = j.Reference,
            Memo = j.Memo,
            Status = j.Status.ToString(),
            PostedAt = j.PostedAt,
            LineCount = _db.JournalDetails.Count(d => d.JournalId == j.JournalId),
            TotalDebit = _db.JournalDetails
                .Where(d => d.JournalId == j.JournalId)
                .Sum(d => d.DebitAmount),
            TotalCredit = _db.JournalDetails
                .Where(d => d.JournalId == j.JournalId)
                .Sum(d => d.CreditAmount),
            ReversesJournalId = j.ReversesJournalId,
            ReversesJournalNo = _db.Journals
                .Where(o => o.JournalId == j.ReversesJournalId)
                .Select(o => o.JournalNo)
                .FirstOrDefault(),
            ReversedByJournalId = j.ReversedByJournalId,
            ReversedByJournalNo = _db.Journals
                .Where(o => o.JournalId == j.ReversedByJournalId)
                .Select(o => o.JournalNo)
                .FirstOrDefault(),
        });
}
