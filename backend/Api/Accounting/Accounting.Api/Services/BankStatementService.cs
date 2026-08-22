using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Interfaces;

namespace Accounting.Api.Services;

/// <summary>
/// Bank statements: importing them, and reconciling them against the money
/// documents already posted.
///
/// <b>Nothing in this service writes to the general ledger, and that is the
/// design rather than an omission.</b> A statement is the bank's account of
/// movements the organization already recorded. Reconciling compares the two
/// accounts; it does not produce a third. A reconciliation that posted would
/// double every transaction it touched, and it would look like it was working
/// right up until somebody read the trial balance.
///
/// <b>Re-importing an overlapping period is the normal case, not an error.</b>
/// Nobody downloads tidy non-overlapping statements — they download the last
/// thirty days every week. So a line carries a hash of what the bank said, and an
/// import adds only what is new. "312 rows read, 40 imported" is the correct and
/// expected result of the fourth download in a month.
/// </summary>
public sealed class BankStatementService
{
    private readonly AccountingDbContext _db;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public BankStatementService(AccountingDbContext db, ICurrentUser user, TimeProvider clock)
    {
        _db = db;
        _user = user;
        _clock = clock;
    }

    public async Task<ImportProfileView?> GetProfileAsync(long bankAccountId, CancellationToken ct)
    {
        StatementImportProfile? profile = await _db.StatementImportProfiles
            .FirstOrDefaultAsync(p => p.BankAccountId == bankAccountId, ct);

        return profile is null ? null : ToView(profile);
    }

    /// <summary>
    /// Saves the mapping for one account, replacing whatever was there. A bank
    /// that changes its export format is the ordinary reason, and there is
    /// nothing to keep from the old one.
    /// </summary>
    public async Task<StatementResult> SaveProfileAsync(
        SaveImportProfileRequest request, CancellationToken ct)
    {
        if (!await _db.BankAccounts.AnyAsync(a => a.BankAccountId == request.BankAccountId, ct))
        {
            return new StatementResult(
                StatementOutcome.BankAccountUnusable, 0,
                "That account is not in this branch.");
        }

        bool twoColumns = request.WithdrawalColumn is { Length: > 0 }
            && request.DepositColumn is { Length: > 0 };

        bool oneColumn = request.AmountColumn is { Length: > 0 };

        // The database says the same thing, and says it as a constraint. Here it
        // is said with a reason, because a person choosing columns on a screen
        // needs to know which of the two shapes to complete rather than that
        // chk_import_profile_amount_shape was violated.
        if (twoColumns == oneColumn)
        {
            return new StatementResult(
                StatementOutcome.FileUnreadable, 0,
                twoColumns
                    ? "Choose either two columns (withdrawal and deposit) or one signed amount "
                        + "column, not both."
                    : "Say where the amount is: either a withdrawal column and a deposit column, "
                        + "or one signed amount column.");
        }

        StatementImportProfile? profile = await _db.StatementImportProfiles
            .FirstOrDefaultAsync(p => p.BankAccountId == request.BankAccountId, ct);

        if (profile is null)
        {
            profile = new StatementImportProfile { BankAccountId = request.BankAccountId };
            _db.StatementImportProfiles.Add(profile);
        }

        profile.SkipRows = request.SkipRows;
        profile.HasHeaderRow = request.HasHeaderRow;
        profile.DateFormat = request.DateFormat;
        profile.DateColumn = request.DateColumn;
        profile.ValueDateColumn = request.ValueDateColumn;
        profile.DescriptionColumn = request.DescriptionColumn;
        profile.ReferenceColumn = request.ReferenceColumn;
        profile.WithdrawalColumn = request.WithdrawalColumn;
        profile.DepositColumn = request.DepositColumn;
        profile.AmountColumn = request.AmountColumn;
        profile.NegativeIsDeposit = request.NegativeIsDeposit;
        profile.BalanceColumn = request.BalanceColumn;

        await _db.SaveChangesAsync(ct);

        return new StatementResult(StatementOutcome.Ok, profile.StatementImportProfileId);
    }

    /// <summary>
    /// Reads a file and stores what is new in it.
    ///
    /// <b>Everything or nothing.</b> A statement half-imported is worse than one
    /// not imported: the lines that landed look reconcilable and the ones that did
    /// not are invisible, so the account reads as almost balanced. The whole read
    /// happens before any row is written, and a bad row aborts the lot.
    /// </summary>
    public async Task<StatementResult> ImportAsync(
        long bankAccountId, string fileName, Stream content, CancellationToken ct)
    {
        if (!await _db.BankAccounts.AnyAsync(a => a.BankAccountId == bankAccountId, ct))
        {
            return new StatementResult(
                StatementOutcome.BankAccountUnusable, 0, "That account is not in this branch.");
        }

        StatementImportProfile? profile = await _db.StatementImportProfiles
            .FirstOrDefaultAsync(p => p.BankAccountId == bankAccountId, ct);

        if (profile is null)
        {
            return new StatementResult(
                StatementOutcome.ProfileMissing, 0,
                "This account has no import mapping yet. Set up which column is which, then "
                    + "import — every bank lays its export out differently.");
        }

        List<BankStatementLine> read;

        try
        {
            StatementGrid grid = IsExcel(fileName)
                ? ExcelStatementReader.Read(content, profile.SkipRows, profile.HasHeaderRow)
                : CsvStatementReader.Read(content, profile.SkipRows, profile.HasHeaderRow);

            read = StatementRowMapper.Map(grid, profile);
        }
        catch (StatementReadException ex)
        {
            return new StatementResult(StatementOutcome.FileUnreadable, 0, ex.Message);
        }

        // What this account has already seen. Compared in memory rather than per
        // row against the database: a statement is a few hundred lines and an
        // account's history is a few thousand, so one read beats N.
        HashSet<string> known = [.. await _db.BankStatementLines
            .Where(l => l.BankAccountId == bankAccountId)
            .Select(l => l.RowHash)
            .ToListAsync(ct)];

        List<BankStatementLine> fresh = [.. read.Where(l => known.Add(l.RowHash))];

        var statement = new BankStatement
        {
            BankAccountId = bankAccountId,
            FromDate = read.Min(l => l.TransactionDate),
            ToDate = read.Max(l => l.TransactionDate),
            FileName = fileName.Length > 260 ? fileName[..260] : fileName,
            ImportedAt = _clock.GetUtcNow(),
            ImportedBy = _user.UserId,
        };

        // The bank's own opening and closing figures, when the file carried a
        // running balance. Taken from the first and last line rather than from a
        // header nobody exports consistently: the balance before the first
        // movement is the first line's balance less what that line moved.
        if (read[0].RunningBalance is decimal firstBalance
            && read[^1].RunningBalance is decimal lastBalance)
        {
            statement.OpeningBalance =
                firstBalance - read[0].DepositAmount + read[0].WithdrawalAmount;
            statement.ClosingBalance = lastBalance;
        }

        _db.BankStatements.Add(statement);
        await _db.SaveChangesAsync(ct);

        foreach (BankStatementLine line in fresh)
        {
            line.BankStatementId = statement.BankStatementId;
        }

        _db.BankStatementLines.AddRange(fresh);
        await _db.SaveChangesAsync(ct);

        int autoMatched = await AutoMatchAsync(bankAccountId, fresh, ct);

        var result = new ImportStatementResult
        {
            BankStatementId = statement.BankStatementId,
            RowsRead = read.Count,
            Imported = fresh.Count,
            AlreadyPresent = read.Count - fresh.Count,
            AutoMatched = autoMatched,
            FromDate = statement.FromDate,
            ToDate = statement.ToDate,
        };

        // Does the bank's own arithmetic hold? This catches what per-line
        // matching cannot — a row that never arrived, because the file was
        // truncated or a mapping quietly skipped it.
        if (statement.OpeningBalance is decimal opening
            && statement.ClosingBalance is decimal closing)
        {
            decimal movement = read.Sum(l => l.DepositAmount - l.WithdrawalAmount);
            decimal difference = closing - opening - movement;

            result.BalanceDifference = difference;
            result.BalancesAgree = Math.Abs(difference) < 0.005m;
        }

        return new StatementResult(StatementOutcome.Ok, statement.BankStatementId, Describe(result));
    }

    /// <summary>
    /// Ties every line that has exactly one plausible document to it, and leaves
    /// the rest alone. See <see cref="StatementMatcher"/> for why "exactly one"
    /// is the bar.
    /// </summary>
    private async Task<int> AutoMatchAsync(
        long bankAccountId, IReadOnlyList<BankStatementLine> lines, CancellationToken ct)
    {
        if (lines.Count == 0)
        {
            return 0;
        }

        DateOnly from = lines.Min(l => l.TransactionDate).AddDays(-15);
        DateOnly to = lines.Max(l => l.TransactionDate).AddDays(15);

        List<MoneyDocumentCandidate> documents =
            await CandidatesAsync(bankAccountId, from, to, ct);

        // Documents already tied to some other line, so one document cannot be
        // claimed twice. Two statement lines matching one payment means one of
        // them is a different movement, and taking both would reconcile a
        // transaction that happened once against money that moved twice.
        HashSet<(string, long)> taken = [.. await _db.BankStatementLines
            .Where(l => l.BankAccountId == bankAccountId
                && l.Status == StatementLineStatus.Matched)
            .Select(l => new { l.MatchedTransactionTypeCode, l.MatchedTransactionId })
            .ToListAsync(ct)
            .ContinueWith(
                t => t.Result.Select(r => (r.MatchedTransactionTypeCode!, r.MatchedTransactionId!.Value)),
                ct)];

        int matched = 0;
        DateTimeOffset now = _clock.GetUtcNow();

        foreach (BankStatementLine line in lines)
        {
            List<MatchCandidateView> ranked = StatementMatcher.Rank(
                line,
                [.. documents.Where(d => !taken.Contains((d.TransactionTypeCode, d.TransactionId)))]);

            if (ranked is not [{ IsConfident: true } best, ..])
            {
                continue;
            }

            line.Status = StatementLineStatus.Matched;
            line.MatchedTransactionTypeCode = best.TransactionTypeCode;
            line.MatchedTransactionId = best.TransactionId;
            line.MatchedAutomatically = true;
            line.MatchedAt = now;
            line.MatchedBy = _user.UserId;

            taken.Add((best.TransactionTypeCode, best.TransactionId));
            matched++;
        }

        if (matched > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return matched;
    }

    /// <summary>
    /// Every posted money document that touched this account in the window.
    ///
    /// <b>A transfer appears twice</b> — outward from the account it leaves and
    /// inward to the one it reaches — because it genuinely shows on both
    /// statements, and a reconciliation of the receiving account that could not
    /// see it would leave a line permanently unmatched.
    /// </summary>
    private async Task<List<MoneyDocumentCandidate>> CandidatesAsync(
        long bankAccountId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        List<MoneyDocumentCandidate> spend = await _db.SpendMoney
            .Where(d => d.BankAccountId == bankAccountId
                && d.Status == MoneyDocumentStatus.Posted
                && d.TransactionDate >= from && d.TransactionDate <= to)
            .Select(d => new MoneyDocumentCandidate(
                "SPM", d.SpendMoneyId, d.TransactionNo, d.TransactionDate, d.Amount,
                true, d.ContactId, d.ReferenceNo))
            .ToListAsync(ct);

        List<MoneyDocumentCandidate> receive = await _db.ReceiveMoney
            .Where(d => d.BankAccountId == bankAccountId
                && d.Status == MoneyDocumentStatus.Posted
                && d.TransactionDate >= from && d.TransactionDate <= to)
            .Select(d => new MoneyDocumentCandidate(
                "RCM", d.ReceiveMoneyId, d.TransactionNo, d.TransactionDate, d.Amount,
                false, d.ContactId, d.ReferenceNo))
            .ToListAsync(ct);

        List<MoneyDocumentCandidate> transfersOut = await _db.TransferMoney
            .Where(d => d.FromBankAccountId == bankAccountId
                && d.Status == MoneyDocumentStatus.Posted
                && d.TransactionDate >= from && d.TransactionDate <= to)
            .Select(d => new MoneyDocumentCandidate(
                "TRM", d.TransferMoneyId, d.TransactionNo, d.TransactionDate, d.Amount,
                true, null, d.ReferenceNo))
            .ToListAsync(ct);

        List<MoneyDocumentCandidate> transfersIn = await _db.TransferMoney
            .Where(d => d.ToBankAccountId == bankAccountId
                && d.Status == MoneyDocumentStatus.Posted
                && d.TransactionDate >= from && d.TransactionDate <= to)
            .Select(d => new MoneyDocumentCandidate(
                "TRM", d.TransferMoneyId, d.TransactionNo, d.TransactionDate, d.Amount,
                false, null, d.ReferenceNo))
            .ToListAsync(ct);

        return [.. spend, .. receive, .. transfersOut, .. transfersIn];
    }

    public async Task<IReadOnlyList<BankStatementListItem>> ListAsync(
        long? bankAccountId, CancellationToken ct)
    {
        IQueryable<BankStatement> query = _db.BankStatements;

        if (bankAccountId is long account)
        {
            query = query.Where(s => s.BankAccountId == account);
        }

        return await (
            from statement in query
            join bank in _db.BankAccounts on statement.BankAccountId equals bank.BankAccountId
            orderby statement.ToDate descending, statement.BankStatementId descending
            select new BankStatementListItem
            {
                BankStatementId = statement.BankStatementId,
                BankAccountId = statement.BankAccountId,
                BankAccountName = bank.AccountName,
                StatementReference = statement.StatementReference,
                FromDate = statement.FromDate,
                ToDate = statement.ToDate,
                FileName = statement.FileName,
                ImportedAt = statement.ImportedAt,
                LineCount = _db.BankStatementLines
                    .Count(l => l.BankStatementId == statement.BankStatementId),
                MatchedCount = _db.BankStatementLines.Count(
                    l => l.BankStatementId == statement.BankStatementId
                        && l.Status == StatementLineStatus.Matched),
                IgnoredCount = _db.BankStatementLines.Count(
                    l => l.BankStatementId == statement.BankStatementId
                        && l.Status == StatementLineStatus.Ignored),
            }).ToListAsync(ct);
    }

    /// <summary>
    /// One statement's lines, each unmatched one carrying the documents it could
    /// be.
    ///
    /// <b>Candidates are computed on the read rather than stored.</b> A document
    /// posted or voided since the import changes what a line could be, and a
    /// stored candidate list would go stale silently — offering a document that
    /// no longer exists, or hiding the one somebody just keyed to fix the
    /// mismatch.
    /// </summary>
    public async Task<IReadOnlyList<StatementLineView>?> LinesAsync(
        long bankStatementId, CancellationToken ct)
    {
        BankStatement? statement = await _db.BankStatements
            .FirstOrDefaultAsync(s => s.BankStatementId == bankStatementId, ct);

        if (statement is null)
        {
            return null;
        }

        List<BankStatementLine> lines = await _db.BankStatementLines
            .Where(l => l.BankStatementId == bankStatementId)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        List<MoneyDocumentCandidate> documents = await CandidatesAsync(
            statement.BankAccountId,
            statement.FromDate.AddDays(-15),
            statement.ToDate.AddDays(15),
            ct);

        // Already claimed by another line, here or on another statement of the
        // same account. Offering one twice invites reconciling one payment
        // against two movements.
        HashSet<(string, long)> taken = [.. (await _db.BankStatementLines
            .Where(l => l.BankAccountId == statement.BankAccountId
                && l.Status == StatementLineStatus.Matched)
            .Select(l => new
            {
                l.BankStatementLineId,
                l.MatchedTransactionTypeCode,
                l.MatchedTransactionId,
            })
            .ToListAsync(ct))
            .Select(r => (r.MatchedTransactionTypeCode!, r.MatchedTransactionId!.Value))];

        Dictionary<(string, long), string?> numbers = documents
            .GroupBy(d => (d.TransactionTypeCode, d.TransactionId))
            .ToDictionary(g => g.Key, g => g.First().TransactionNo);

        var views = new List<StatementLineView>(lines.Count);

        foreach (BankStatementLine line in lines)
        {
            var view = new StatementLineView
            {
                BankStatementLineId = line.BankStatementLineId,
                LineNumber = line.LineNumber,
                TransactionDate = line.TransactionDate,
                ValueDate = line.ValueDate,
                Description = line.Description,
                ReferenceNo = line.ReferenceNo,
                WithdrawalAmount = line.WithdrawalAmount,
                DepositAmount = line.DepositAmount,
                RunningBalance = line.RunningBalance,
                Status = line.Status.ToString(),
                MatchedTransactionTypeCode = line.MatchedTransactionTypeCode,
                MatchedTransactionId = line.MatchedTransactionId,
                MatchedAutomatically = line.MatchedAutomatically,
                Note = line.Note,
            };

            if (line.Status == StatementLineStatus.Matched
                && line.MatchedTransactionTypeCode is { } code
                && line.MatchedTransactionId is long id)
            {
                view.MatchedTransactionNo = numbers.GetValueOrDefault((code, id));
            }
            else if (line.Status == StatementLineStatus.Unmatched)
            {
                view.Candidates = StatementMatcher.Rank(
                    line,
                    [.. documents.Where(
                        d => !taken.Contains((d.TransactionTypeCode, d.TransactionId)))]);
            }

            views.Add(view);
        }

        return views;
    }

    /// <summary>
    /// Ties a line to a document by hand.
    ///
    /// The document must be posted, on this account, and not already claimed by
    /// another line — the last of those being the one a person cannot see for
    /// themselves, and the one that quietly reconciles one payment twice.
    /// </summary>
    public async Task<StatementResult> MatchAsync(
        long bankStatementLineId, MatchStatementLineRequest request, CancellationToken ct)
    {
        BankStatementLine? line = await _db.BankStatementLines
            .FirstOrDefaultAsync(l => l.BankStatementLineId == bankStatementLineId, ct);

        if (line is null)
        {
            return new StatementResult(StatementOutcome.NotFound);
        }

        if (line.Status != StatementLineStatus.Unmatched)
        {
            return new StatementResult(
                StatementOutcome.AlreadyDecided, bankStatementLineId,
                $"That line is already {line.Status.ToString().ToLowerInvariant()}. "
                    + "Undo it first if it is wrong.");
        }

        string code = request.TransactionTypeCode.ToUpperInvariant();

        List<MoneyDocumentCandidate> documents = await CandidatesAsync(
            line.BankAccountId,
            line.TransactionDate.AddDays(-90),
            line.TransactionDate.AddDays(90),
            ct);

        // A wider window than the matcher uses, deliberately: a person matching
        // by hand has seen something the matcher could not, and refusing on a
        // date window would be the tool overruling them about their own books.
        if (!documents.Any(
            d => d.TransactionTypeCode == code && d.TransactionId == request.TransactionId))
        {
            return new StatementResult(
                StatementOutcome.CandidateUnusable, bankStatementLineId,
                "That document is not a posted money document on this account within three "
                    + "months of this line.");
        }

        bool claimed = await _db.BankStatementLines.AnyAsync(
            l => l.BankAccountId == line.BankAccountId
                && l.Status == StatementLineStatus.Matched
                && l.MatchedTransactionTypeCode == code
                && l.MatchedTransactionId == request.TransactionId,
            ct);

        if (claimed)
        {
            return new StatementResult(
                StatementOutcome.CandidateUnusable, bankStatementLineId,
                "Another statement line is already matched to that document. One document is "
                    + "one movement — if this line is a different one, it needs its own.");
        }

        line.Status = StatementLineStatus.Matched;
        line.MatchedTransactionTypeCode = code;
        line.MatchedTransactionId = request.TransactionId;
        line.MatchedAutomatically = false;
        line.MatchedAt = _clock.GetUtcNow();
        line.MatchedBy = _user.UserId;

        await _db.SaveChangesAsync(ct);

        return new StatementResult(StatementOutcome.Ok, bankStatementLineId);
    }

    /// <summary>Undoes a match or an ignore, putting the line back to undecided.</summary>
    public async Task<StatementResult> UnmatchAsync(long bankStatementLineId, CancellationToken ct)
    {
        BankStatementLine? line = await _db.BankStatementLines
            .FirstOrDefaultAsync(l => l.BankStatementLineId == bankStatementLineId, ct);

        if (line is null)
        {
            return new StatementResult(StatementOutcome.NotFound);
        }

        line.Status = StatementLineStatus.Unmatched;
        line.MatchedTransactionTypeCode = null;
        line.MatchedTransactionId = null;
        line.MatchedAutomatically = false;
        line.MatchedAt = null;
        line.MatchedBy = null;
        line.Note = null;

        await _db.SaveChangesAsync(ct);

        return new StatementResult(StatementOutcome.Ok, bankStatementLineId);
    }

    /// <summary>
    /// Sets a line aside. Kept rather than deleted, and it needs a reason: a line
    /// that vanishes is indistinguishable from one that was never imported.
    /// </summary>
    public async Task<StatementResult> IgnoreAsync(
        long bankStatementLineId, IgnoreStatementLineRequest request, CancellationToken ct)
    {
        BankStatementLine? line = await _db.BankStatementLines
            .FirstOrDefaultAsync(l => l.BankStatementLineId == bankStatementLineId, ct);

        if (line is null)
        {
            return new StatementResult(StatementOutcome.NotFound);
        }

        if (line.Status == StatementLineStatus.Matched)
        {
            return new StatementResult(
                StatementOutcome.AlreadyDecided, bankStatementLineId,
                "That line is matched to a document. Unmatch it first.");
        }

        line.Status = StatementLineStatus.Ignored;
        line.Note = request.Note;

        await _db.SaveChangesAsync(ct);

        return new StatementResult(StatementOutcome.Ok, bankStatementLineId);
    }

    /// <summary>
    /// A statement's lines as rows, for export. Shaped here rather than in the
    /// writers so CSV and Excel cannot drift into exporting different things.
    /// </summary>
    public async Task<StatementExport?> ExportAsync(long bankStatementId, CancellationToken ct)
    {
        IReadOnlyList<StatementLineView>? lines = await LinesAsync(bankStatementId, ct);

        if (lines is null)
        {
            return null;
        }

        BankStatementListItem header = (await ListAsync(null, ct))
            .First(s => s.BankStatementId == bankStatementId);

        string[] columns =
        [
            "Date", "Value date", "Description", "Reference",
            "Withdrawal", "Deposit", "Balance", "Status", "Matched document", "Note",
        ];

        List<string[]> rows =
        [
            .. lines.Select(l => new[]
            {
                l.TransactionDate.ToString("yyyy-MM-dd"),
                l.ValueDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                l.Description ?? string.Empty,
                l.ReferenceNo ?? string.Empty,
                l.WithdrawalAmount == 0 ? string.Empty : l.WithdrawalAmount.ToString("0.00"),
                l.DepositAmount == 0 ? string.Empty : l.DepositAmount.ToString("0.00"),
                l.RunningBalance?.ToString("0.00") ?? string.Empty,
                l.Status,
                l.MatchedTransactionNo
                    ?? (l.MatchedTransactionId is long id
                        ? $"{l.MatchedTransactionTypeCode} #{id}"
                        : string.Empty),
                l.Note ?? string.Empty,
            }),
        ];

        string name =
            $"{header.BankAccountName}-{header.FromDate:yyyyMMdd}-{header.ToDate:yyyyMMdd}";

        return new StatementExport(Sanitize(name), columns, rows);
    }

    /// <summary>
    /// Makes a name safe to put in a Content-Disposition header and on a file
    /// system. An account named "HDFC Current / Main" would otherwise produce a
    /// path, and a quote in a name would end the header's quoted string early.
    /// </summary>
    private static string Sanitize(string name)
    {
        var safe = new System.Text.StringBuilder(name.Length);

        foreach (char c in name)
        {
            safe.Append(char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-');
        }

        return safe.ToString().Trim('-');
    }

    private static bool IsExcel(string fileName) =>
        fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".xlsm", StringComparison.OrdinalIgnoreCase);

    private static string Describe(ImportStatementResult result)
    {
        string counted = result.AlreadyPresent > 0
            ? $"{result.RowsRead} row(s) read, {result.Imported} new, "
                + $"{result.AlreadyPresent} already imported"
            : $"{result.Imported} row(s) imported";

        string matched = result.AutoMatched > 0
            ? $"; {result.AutoMatched} matched automatically"
            : string.Empty;

        string balances = result.BalancesAgree switch
        {
            true => "; the bank's own balances agree",
            false => $"; the bank's balances are out by {result.BalanceDifference:0.00} — "
                + "a row may be missing from the file",
            null => string.Empty,
        };

        return counted + matched + balances + ".";
    }

    private static ImportProfileView ToView(StatementImportProfile profile) => new()
    {
        StatementImportProfileId = profile.StatementImportProfileId,
        BankAccountId = profile.BankAccountId,
        SkipRows = profile.SkipRows,
        HasHeaderRow = profile.HasHeaderRow,
        DateFormat = profile.DateFormat,
        DateColumn = profile.DateColumn,
        ValueDateColumn = profile.ValueDateColumn,
        DescriptionColumn = profile.DescriptionColumn,
        ReferenceColumn = profile.ReferenceColumn,
        WithdrawalColumn = profile.WithdrawalColumn,
        DepositColumn = profile.DepositColumn,
        AmountColumn = profile.AmountColumn,
        NegativeIsDeposit = profile.NegativeIsDeposit,
        BalanceColumn = profile.BalanceColumn,
    };
}
