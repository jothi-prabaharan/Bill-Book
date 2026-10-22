using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;

namespace Accounting.Repository.Repositories;

/// <summary>
/// The General ledger (TK-216): one account's postings over a period, with the
/// balance it opened at, a running balance down the page and the balance it
/// closed at; and a summary that lists every account for the same period.
///
/// <b>Every balance is on the account's normal side.</b> An asset or an expense
/// grows on the debit side and everything else on the credit side, and a contra
/// account runs opposite its type (<see cref="BudgetMathRepository.IsDebitNormal"/>).
/// "Accounts Payable: -40,000" is a number every accountant has to stop and
/// translate, so the server turns it the right way up, and opening + what grew
/// the account - what reduced it = closing holds on every account whichever side
/// it is on.
///
/// <b>Opening follows the Trial Balance (TK-215).</b> A balance-sheet account
/// opens at everything posted before the period; an income or expense account
/// opens at what was posted since its financial year began, because earlier
/// years' profit is not this year's income. So an account's closing balance here
/// is its closing balance on the Trial Balance for the same dates.
///
/// A contact or a project narrows the ledger to the postings that name it, and
/// its opening balance is the same postings' before the period, so the running
/// balance still starts from what the filtered ledger stood at.
///
/// The ledger is read in one query for the rows and one sum for the opening; the
/// arithmetic after that is pure. LINQ only.
/// </summary>
public sealed class GeneralLedgerReportRepository
{
    private const int Income = 4;
    private const int Expense = 5;

    private readonly AccountingDbContext _db;
    private readonly IFinancialYearProvider _financialYear;
    private readonly IContactNameLookup _contacts;

    public GeneralLedgerReportRepository(
        AccountingDbContext db, IFinancialYearProvider financialYear, IContactNameLookup contacts)
    {
        _db = db;
        _financialYear = financialYear;
        _contacts = contacts;
    }

    /// <summary>The refusal a period needs beyond both dates being given, with the field it belongs to. Pure.</summary>
    public static IReadOnlyList<(string Field, string Message)> DateRefusals(DateOnly? from, DateOnly? to) =>
        from is DateOnly start && to is DateOnly end && end < start
            ? [(nameof(GeneralLedgerReportRequest.To), "To date cannot be before the from date.")]
            : [];

    /// <summary>Where an account grows: its type's side, turned over for a contra account. Pure.</summary>
    public static LedgerSide NormalSideOf(int accountTypeId, bool isContra) =>
        BudgetMathRepository.IsDebitNormal(accountTypeId, isContra) ? LedgerSide.Debit : LedgerSide.Credit;

    /// <summary>An income or expense account, whose balance starts again with each financial year. Pure.</summary>
    public static bool IsProfitAndLoss(int accountTypeId) => accountTypeId is Income or Expense;

    /// <summary>A debit-terms net turned to an account's normal side. Pure.</summary>
    public static decimal OnSide(decimal debitNet, LedgerSide side) => side == LedgerSide.Debit ? debitNet : -debitNet;

    /// <summary>The document's number, or for a row written before the ledger carried one, its type and id. Pure.</summary>
    public static string DocumentNoOf(string? documentNo, string transactionTypeCode, long transactionId) =>
        string.IsNullOrWhiteSpace(documentNo) ? $"{transactionTypeCode}-{transactionId}" : documentNo;

    /// <summary>
    /// One account's ledger. Null when the account is not in this branch's chart:
    /// the query filter decides that, so a caller cannot read across branches by
    /// guessing an id, and another branch's account looks like none at all.
    /// </summary>
    public async Task<GeneralLedgerReportResultDto?> RunAsync(
        long accountId, GeneralLedgerReportRequest request, CancellationToken ct)
    {
        DateOnly from = request.From!.Value;
        DateOnly to = request.To!.Value;

        GeneralLedgerAccount? account = await _db.Accounts
            .Where(a => a.AccountId == accountId)
            .Select(a => new GeneralLedgerAccount
            {
                AccountId = a.AccountId,
                AccountTypeId = a.AccountTypeId,
                AccountCode = a.AccountCode,
                AccountName = a.AccountName,
                IsContra = a.IsContra,
                IsActive = a.IsActive,
            })
            .FirstOrDefaultAsync(ct);

        if (account is null)
        {
            return null;
        }

        int startMonth = await _financialYear.GetStartMonthAsync(ct);
        DateOnly yearStart = TrialBalanceReportRepository.FinancialYearStart(from, startMonth);
        LedgerSide side = NormalSideOf(account.AccountTypeId, account.IsContra);

        IQueryable<JournalLedger> postings = Postings(_db.JournalLedger, accountId, request.ContactId, request.ProjectId);

        decimal openingNet = await OpeningNets(
            postings, from, IsProfitAndLoss(account.AccountTypeId) ? yearStart : null).SumAsync(ct);

        List<GeneralLedgerReportRowDto> rows = await Rows(
            postings, _db.SubAccounts, _db.Journals, _db.Projects, from, to).ToListAsync(ct);

        await NameContactsAsync(rows, ct);

        return Compose(account, rows, openingNet, from, to, yearStart, request.ContactId, request.ProjectId);
    }

    /// <summary>One account's postings, narrowed to a contact and a project when asked. LINQ only.</summary>
    public static IQueryable<JournalLedger> Postings(
        IQueryable<JournalLedger> ledger, long accountId, long? contactId, long? projectId) =>
        ledger.Where(l => l.AccountId == accountId
            && (contactId == null || l.ContactId == contactId)
            && (projectId == null || l.ProjectId == projectId));

    /// <summary>
    /// Debits less credits of each posting before the period, from <paramref name="since"/>
    /// when one is given: the legs an opening balance is summed from. LINQ only.
    /// </summary>
    public static IQueryable<decimal> OpeningNets(IQueryable<JournalLedger> postings, DateOnly from, DateOnly? since) =>
        postings
            .Where(l => l.LedgerDate < from && (since == null || l.LedgerDate >= since))
            .Select(l => l.DebitAmountBase - l.CreditAmountBase);

    /// <summary>
    /// The postings in the period in date order, each with the sub-account,
    /// journal and project it names. Left joins, because a bank or equity leg has
    /// no sub-account, a document posting has no journal and most rows have no
    /// project, and an inner join would drop exactly those rows. LINQ only.
    /// </summary>
    public static IQueryable<GeneralLedgerReportRowDto> Rows(
        IQueryable<JournalLedger> postings,
        IQueryable<SubAccount> subAccounts,
        IQueryable<Journal> journals,
        IQueryable<Project> projects,
        DateOnly start,
        DateOnly end) =>
        from l in postings
        where l.LedgerDate >= start && l.LedgerDate <= end
        join s in subAccounts on l.SubAccountId equals s.SubAccountId into subs
        from s in subs.DefaultIfEmpty()
        join j in journals on l.JournalId equals j.JournalId into js
        from j in js.DefaultIfEmpty()
        join p in projects on l.ProjectId equals p.ProjectId into ps
        from p in ps.DefaultIfEmpty()
        orderby l.LedgerDate, l.LedgerId
        select new GeneralLedgerReportRowDto
        {
            LedgerId = l.LedgerId,
            LedgerDate = l.LedgerDate,
            DocumentNo = l.DocumentNo ?? string.Empty,
            TransactionTypeCode = l.TransactionTypeCode,
            TransactionId = l.TransactionId,
            TransactionDetailId = l.TransactionDetailId,
            JournalId = l.JournalId,
            JournalNo = j == null ? null : j.JournalNo,
            SubAccountName = s == null ? null : s.SubAccountName,
            ContactId = l.ContactId,
            ProjectId = l.ProjectId,
            ProjectName = p == null ? null : p.ProjectName,
            Description = l.TransactionDesc,
            DebitAmount = l.DebitAmount,
            CreditAmount = l.CreditAmount,
            DebitAmountBase = l.DebitAmountBase,
            CreditAmountBase = l.CreditAmountBase,
            CurrencyCode = l.CurrencyCode,
            ExchangeRate = l.ExchangeRate,
        };

    /// <summary>
    /// The ledger from the account, its rows and the debit-terms net before the
    /// period. Fills each row's document number and running balance. Pure.
    /// </summary>
    public static GeneralLedgerReportResultDto Compose(
        GeneralLedgerAccount account,
        List<GeneralLedgerReportRowDto> rows,
        decimal openingNet,
        DateOnly from,
        DateOnly to,
        DateOnly yearStart,
        long? contactId,
        long? projectId)
    {
        LedgerSide side = NormalSideOf(account.AccountTypeId, account.IsContra);
        decimal opening = OnSide(openingNet, side);
        decimal closing = ApplyRunningBalance(rows, opening, side);

        foreach (GeneralLedgerReportRowDto row in rows)
        {
            row.DocumentNo = DocumentNoOf(row.DocumentNo, row.TransactionTypeCode, row.TransactionId);
        }

        return new GeneralLedgerReportResultDto
        {
            AccountId = account.AccountId,
            AccountCode = account.AccountCode,
            AccountName = account.AccountName,
            AccountTypeId = account.AccountTypeId,
            IsContra = account.IsContra,
            NormalSide = side,
            From = from,
            To = to,
            FinancialYearStart = yearStart,
            ContactId = contactId,
            ProjectId = projectId,
            OpeningBalance = opening,
            ClosingBalance = closing,
            TotalDebit = rows.Sum(r => r.DebitAmountBase),
            TotalCredit = rows.Sum(r => r.CreditAmountBase),
            Rows = rows,
        };
    }

    /// <summary>
    /// Each row's balance after it, on the account's normal side, starting from
    /// the opening balance; returns the closing balance, which is the opening one
    /// when there are no rows. Pure.
    /// </summary>
    public static decimal ApplyRunningBalance(IEnumerable<GeneralLedgerReportRowDto> rows, decimal opening, LedgerSide side)
    {
        decimal running = opening;

        foreach (GeneralLedgerReportRowDto row in rows)
        {
            running += OnSide(row.DebitAmountBase - row.CreditAmountBase, side);
            row.RunningBalance = running;
        }

        return running;
    }

    /// <summary>One batched read of the contacts' names, not one per row.</summary>
    private async Task NameContactsAsync(List<GeneralLedgerReportRowDto> rows, CancellationToken ct)
    {
        List<long> ids = [.. rows.Where(r => r.ContactId is not null).Select(r => r.ContactId!.Value).Distinct()];
        if (ids.Count == 0)
        {
            return;
        }

        IReadOnlyDictionary<long, NamedRef> names = await _contacts.ResolveAsync(ids, ct);
        foreach (GeneralLedgerReportRowDto row in rows)
        {
            row.ContactName = row.ContactId is long id && names.TryGetValue(id, out NamedRef? named) ? named.Name : null;
        }
    }

    /// <summary>Every account that moved or holds a balance, for a period, on a request the controller has validated.</summary>
    public async Task<GeneralLedgerSummaryResultDto> SummaryAsync(GeneralLedgerSummaryRequest request, CancellationToken ct)
    {
        DateOnly from = request.From!.Value;
        DateOnly to = request.To!.Value;

        int startMonth = await _financialYear.GetStartMonthAsync(ct);
        DateOnly yearStart = TrialBalanceReportRepository.FinancialYearStart(from, startMonth);

        List<GeneralLedgerAccount> accounts = await _db.Accounts
            .Select(a => new GeneralLedgerAccount
            {
                AccountId = a.AccountId,
                AccountTypeId = a.AccountTypeId,
                AccountCode = a.AccountCode,
                AccountName = a.AccountName,
                IsContra = a.IsContra,
                IsActive = a.IsActive,
            })
            .ToListAsync(ct);

        // The Trial Balance's sums: one pass over the ledger, by account.
        List<AccountPeriodSums> sums = await TrialBalanceReportRepository.Sums(
            _db.JournalLedger, from, to, yearStart, null, null, null).ToListAsync(ct);

        return ComposeSummary(accounts, sums, from, to, yearStart, request.IsZeroBalanceShown);
    }

    /// <summary>
    /// The summary from the accounts and their sums. An account is listed when its
    /// opening balance, its debits or credits or its closing balance is not
    /// nothing; with zero balances asked for, an active account with none of them
    /// is listed too. An inactive account that moved is always listed. In code
    /// order. Pure.
    /// </summary>
    public static GeneralLedgerSummaryResultDto ComposeSummary(
        IReadOnlyList<GeneralLedgerAccount> accounts,
        IReadOnlyList<AccountPeriodSums> sums,
        DateOnly from,
        DateOnly to,
        DateOnly yearStart,
        bool isZeroBalanceShown)
    {
        Dictionary<long, AccountPeriodSums> sumsById = sums.ToDictionary(s => s.AccountId);
        List<GeneralLedgerSummaryLineDto> lines = [];

        foreach (GeneralLedgerAccount account in accounts)
        {
            AccountPeriodSums s = sumsById.GetValueOrDefault(account.AccountId) ?? new AccountPeriodSums();

            decimal openingNet = IsProfitAndLoss(account.AccountTypeId) ? s.NetBefore - s.NetBeforeYear : s.NetBefore;
            decimal closingNet = openingNet + s.MovementDebit - s.MovementCredit;

            bool hasFigures = openingNet != 0m || s.MovementDebit != 0m || s.MovementCredit != 0m || closingNet != 0m;
            if (!hasFigures && !(isZeroBalanceShown && account.IsActive))
            {
                continue;
            }

            LedgerSide side = NormalSideOf(account.AccountTypeId, account.IsContra);
            lines.Add(new GeneralLedgerSummaryLineDto
            {
                AccountId = account.AccountId,
                AccountCode = account.AccountCode,
                AccountName = account.AccountName,
                AccountTypeId = account.AccountTypeId,
                IsContra = account.IsContra,
                NormalSide = side,
                OpeningBalance = OnSide(openingNet, side),
                TotalDebit = s.MovementDebit,
                TotalCredit = s.MovementCredit,
                ClosingBalance = OnSide(closingNet, side),
            });
        }

        lines.Sort((a, b) => string.CompareOrdinal(a.AccountCode, b.AccountCode));

        return new GeneralLedgerSummaryResultDto
        {
            From = from,
            To = to,
            FinancialYearStart = yearStart,
            Lines = lines,
            TotalDebit = lines.Sum(l => l.TotalDebit),
            TotalCredit = lines.Sum(l => l.TotalCredit),
        };
    }
}

/// <summary>An account as the general ledger needs it.</summary>
public sealed class GeneralLedgerAccount
{
    public long AccountId { get; set; }

    public int AccountTypeId { get; set; }

    public string AccountCode { get; set; } = string.Empty;

    public string AccountName { get; set; } = string.Empty;

    public bool IsContra { get; set; }

    public bool IsActive { get; set; }
}
