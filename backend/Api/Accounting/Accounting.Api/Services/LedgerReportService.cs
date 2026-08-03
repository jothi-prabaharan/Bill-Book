using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Api.Services;

/// <summary>
/// The two reads that make a posting checkable: one account's ledger, and the
/// trial balance.
///
/// <b>Built as LINQ projections, not as a database view.</b> SPEC sketches an
/// <c>acc.vw_LedgerDetail</c>, and it was not built. <c>CREATE VIEW</c> is not on
/// CLAUDE.md's short list of raw-SQL exceptions, and a view that forgets
/// <c>security_invoker = true</c> runs as its owner — which means it reads past
/// row-level security and hands one branch another branch's general ledger. The
/// join it would save is four lines of LINQ.
///
/// <b>The running balance is computed in C#</b>, over the ordered result. A
/// window function would need the rows ordered, scoped to one account and cut to
/// one date range — and a ledger read is always all three of those, so the
/// database buys nothing the list in memory does not already have.
/// </summary>
public sealed class LedgerReportService
{
    private readonly AccountingDbContext _db;

    public LedgerReportService(AccountingDbContext db) => _db = db;

    /// <summary>
    /// One account over a date range. Returns null when the account is not in
    /// this branch's chart — the query filter decides that, so a caller cannot
    /// read across branches by guessing an id.
    /// </summary>
    public async Task<AccountLedgerView?> GetAccountLedgerAsync(
        long accountId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        Account? account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.AccountId == accountId, ct);

        if (account is null)
        {
            return null;
        }

        // Everything before the range, netted into one number. Without it the
        // running balance down the page would start from zero and every figure
        // on it would be wrong by the same amount — the kind of wrong that looks
        // completely plausible.
        decimal opening = 0m;

        if (from is DateOnly start)
        {
            var before = await _db.JournalLedger
                .Where(l => l.AccountId == accountId && l.LedgerDate < start)
                .GroupBy(l => 1)
                .Select(g => new
                {
                    Debit = g.Sum(l => l.DebitAmountBase),
                    Credit = g.Sum(l => l.CreditAmountBase),
                })
                .FirstOrDefaultAsync(ct);

            opening = LedgerArithmetic.Net(before?.Debit ?? 0m, before?.Credit ?? 0m);
        }

        IQueryable<JournalLedger> query = _db.JournalLedger
            .Where(l => l.AccountId == accountId);

        if (from is DateOnly rangeStart)
        {
            query = query.Where(l => l.LedgerDate >= rangeStart);
        }

        if (to is DateOnly rangeEnd)
        {
            query = query.Where(l => l.LedgerDate <= rangeEnd);
        }

        // The join Accounting would otherwise ask a view for. Left joins, because
        // a bank or equity leg has no sub-account and a document posting has no
        // journal — and an inner join would silently drop exactly those rows.
        List<AccountLedgerRow> rows = await (
            from l in query
            join s in _db.SubAccounts on l.SubAccountId equals s.SubAccountId into subs
            from s in subs.DefaultIfEmpty()
            join j in _db.Journals on l.JournalId equals j.JournalId into journals
            from j in journals.DefaultIfEmpty()
            orderby l.LedgerDate, l.LedgerId
            select new AccountLedgerRow
            {
                LedgerId = l.LedgerId,
                LedgerDate = l.LedgerDate,
                TransactionTypeCode = l.TransactionTypeCode,
                TransactionId = l.TransactionId,
                TransactionDetailId = l.TransactionDetailId,
                JournalId = l.JournalId,
                JournalNo = j == null ? null : j.JournalNo,
                LedgerTypeId = l.LedgerTypeId,
                LedgerSourceId = l.LedgerSourceId,
                SubAccountId = l.SubAccountId,
                SubAccountName = s == null ? null : s.SubAccountName,
                ContactId = l.ContactId,
                TransactionDesc = l.TransactionDesc,
                DebitAmount = l.DebitAmount,
                CreditAmount = l.CreditAmount,
                DebitAmountBase = l.DebitAmountBase,
                CreditAmountBase = l.CreditAmountBase,
                CurrencyCode = l.CurrencyCode,
                ExchangeRate = l.ExchangeRate,
            }).ToListAsync(ct);

        decimal running = LedgerArithmetic.ApplyRunningBalance(rows, opening);

        return new AccountLedgerView
        {
            AccountId = account.AccountId,
            AccountCode = account.AccountCode,
            AccountName = account.AccountName,
            AccountTypeId = account.AccountTypeId,
            IsContra = account.IsContra,
            From = from,
            To = to,
            OpeningBalance = opening,
            ClosingBalance = running,
            TotalDebit = rows.Sum(r => r.DebitAmountBase),
            TotalCredit = rows.Sum(r => r.CreditAmountBase),
            Rows = rows,
        };
    }

    /// <summary>
    /// Every account with a balance, and the two columns that have to agree.
    ///
    /// Accounts with no postings are left out rather than listed at zero: a
    /// freshly seeded chart has a dozen of them and a trial balance is read for
    /// what moved, not for what exists.
    /// </summary>
    public async Task<TrialBalanceView> GetTrialBalanceAsync(
        DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        IQueryable<JournalLedger> query = _db.JournalLedger;

        if (from is DateOnly start)
        {
            query = query.Where(l => l.LedgerDate >= start);
        }

        if (to is DateOnly end)
        {
            query = query.Where(l => l.LedgerDate <= end);
        }

        // Grouped and summed in the database. The alternative — pulling every
        // ledger row back to add it up here — is the query that works on a demo
        // branch and takes a minute on a real one.
        // Joined before grouping, not after. Grouping first and joining the key
        // afterwards reads more naturally and does not translate — EF gives up
        // and the whole ledger comes back to be grouped in memory.
        var totals = await (
            from l in query
            join a in _db.Accounts on l.AccountId equals a.AccountId
            group new { l.DebitAmountBase, l.CreditAmountBase } by new
            {
                a.AccountId,
                a.AccountCode,
                a.AccountName,
                a.AccountTypeId,
                a.IsContra,
            } into g
            select new
            {
                g.Key.AccountId,
                g.Key.AccountCode,
                g.Key.AccountName,
                g.Key.AccountTypeId,
                g.Key.IsContra,
                Debit = g.Sum(x => x.DebitAmountBase),
                Credit = g.Sum(x => x.CreditAmountBase),
            }).ToListAsync(ct);

        var rows = totals
            .Select(t =>
            {
                // Each account appears in exactly one column — the side its net
                // actually falls on. Showing both totals per account would foot
                // to two numbers that always agree and prove nothing, because
                // every posting was balanced when it was written.
                (decimal debitBalance, decimal creditBalance) =
                    LedgerArithmetic.Sides(LedgerArithmetic.Net(t.Debit, t.Credit));

                return new TrialBalanceRow
                {
                    AccountId = t.AccountId,
                    AccountCode = t.AccountCode,
                    AccountName = t.AccountName,
                    AccountTypeId = t.AccountTypeId,
                    IsContra = t.IsContra,
                    TotalDebit = t.Debit,
                    TotalCredit = t.Credit,
                    DebitBalance = debitBalance,
                    CreditBalance = creditBalance,
                };
            })
            .Where(r => r.DebitBalance != 0m || r.CreditBalance != 0m)
            .OrderBy(r => r.AccountCode)
            .ToList();

        decimal debitTotal = rows.Sum(r => r.DebitBalance);
        decimal creditTotal = rows.Sum(r => r.CreditBalance);

        return new TrialBalanceView
        {
            From = from,
            To = to,
            Rows = rows,
            TotalDebit = debitTotal,
            TotalCredit = creditTotal,
            IsBalanced = debitTotal == creditTotal,
        };
    }
}
