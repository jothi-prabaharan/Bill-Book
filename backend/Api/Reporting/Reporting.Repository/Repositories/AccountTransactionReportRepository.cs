using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Numbering;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Account Transaction report (TK-218): every posting in a period, account by
/// account, each with its contact, currency, tax rate and a running balance on the
/// account's normal side.
///
/// <b>Opening follows the General ledger and the Trial Balance</b> (TK-215,
/// TK-216): a balance-sheet account opens at everything before the period, an
/// income or expense account at what was posted since its financial year began, so
/// with no contact, tax rate or source narrowing the postings an account's closing
/// balance is its closing balance on the General ledger. A narrowing filter narrows
/// the opening too, so opening + debits − credits = closing holds on what is shown.
///
/// <b>The result is paged by posting, and the postings run in one order:</b> account
/// code, then date, then ledger id, which is total, so a running balance is the same
/// on every request. A page holds postings from consecutive accounts, so only its
/// first account can continue from the page before; that one account's earlier
/// postings are summed in one query and the rest open at their own opening balance.
/// Each account carries its whole-period balances and two flags saying whether its
/// opening and closing rows are on this page, which is what lets a page add itself
/// to the ones loaded before it.
///
/// <b>The ledger is never read per row.</b> A page's postings, then one batched query
/// each for the contacts, the other legs of its documents, the tax sub-accounts and
/// rates, the contacts' licences and the manual journals' references, joined here
/// in C#. LINQ only, over the read models, so a test runs the builders over lists.
/// </summary>
public sealed class AccountTransactionReportRepository
{
    // mst.AccountTypes ids: contractual, as ChartOfAccountsSeed has them.
    private const int Asset = 1;
    private const int Income = 4;
    private const int Expense = 5;

    /// <summary>What a posting's other side is called when it went to several accounts.</summary>
    public const string Various = "Various";

    private readonly ReportingDbContext _db;
    private readonly IFinancialYearProvider _financialYear;

    public AccountTransactionReportRepository(ReportingDbContext db, IFinancialYearProvider financialYear)
    {
        _db = db;
        _financialYear = financialYear;
    }

    /// <summary>
    /// The refusals a request needs beyond its annotations, with the field each
    /// belongs to: the period's order and the number of accounts. Pure.
    /// </summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(AccountTransactionRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from)
        {
            refusals.Add((nameof(AccountTransactionRequest.ToDate), "To date cannot be before the from date."));
        }

        if (request.AccountIds is { Count: > AccountTransactionLimits.MaxAccounts })
        {
            refusals.Add((nameof(AccountTransactionRequest.AccountIds), $"Choose no more than {AccountTransactionLimits.MaxAccounts} accounts."));
        }

        return refusals;
    }

    /// <summary>
    /// The refusals that need the branch's own books: an account, contact or tax
    /// rate that is not on this branch. The query filter decides what is the
    /// branch's, so another branch's reads as none.
    /// </summary>
    public async Task<IReadOnlyList<(string Field, string Message)>> LookupRefusalsAsync(
        AccountTransactionRequest request, CancellationToken ct)
    {
        List<(string, string)> refusals = [];

        List<long> accountIds = [.. request.AccountIds?.Distinct() ?? []];
        if (accountIds.Count > 0)
        {
            int known = await _db.Accounts.CountAsync(a => accountIds.Contains(a.AccountId), ct);
            if (known != accountIds.Count)
            {
                refusals.Add((nameof(AccountTransactionRequest.AccountIds), "One of those accounts is not on this branch."));
            }
        }

        if (request.ContactId is long contactId && !await _db.Contacts.AnyAsync(c => c.ContactId == contactId, ct))
        {
            refusals.Add((nameof(AccountTransactionRequest.ContactId), "That contact is not on this branch."));
        }

        if (request.TaxGroupId is long group
            && !await _db.TaxMasters.AnyAsync(t => t.TaxGroupId == group || (t.TaxGroupId == 0 && t.TaxMasterId == group), ct))
        {
            refusals.Add((nameof(AccountTransactionRequest.TaxGroupId), "That tax rate is not on this branch."));
        }

        return refusals;
    }

    /// <summary>A page of the period's postings for a request the controller has already validated.</summary>
    public async Task<AccountTransactionResultDto> RunAsync(AccountTransactionRequest request, CancellationToken ct)
    {
        DateOnly start = request.FromDate!.Value;
        DateOnly end = request.ToDate!.Value;
        DateOnly yearStart = PortalAccountRepository.FinancialYearStart(start, await _financialYear.GetStartMonthAsync(ct));
        List<long> chosen = [.. request.AccountIds?.Distinct() ?? []];

        IQueryable<JournalLedgerRead> scoped = Scope(_db.Ledger, _db.SubAccounts, chosen, request.ContactId, request.TaxGroupId, request.Source);
        IQueryable<JournalLedgerRead> period = InPeriod(scoped, start, end);

        int totalPostings = await period.CountAsync(ct);
        decimal totalDebit = await period.SumAsync(l => l.DebitAmountBase, ct);
        decimal totalCredit = await period.SumAsync(l => l.CreditAmountBase, ct);
        List<long> postedAccounts = await period.Select(l => l.AccountId).Distinct().ToListAsync(ct);

        List<long> idle = [.. IdleAccounts(chosen, postedAccounts, request.ContactId, request.TaxGroupId, request.Source)];

        int skip = (request.PageNumber - 1) * request.PageSize;
        List<PostingKey> keys = await Keys(period, _db.Accounts).Skip(skip).Take(request.PageSize).ToListAsync(ct);

        List<long> pageAccountIds = [.. keys.Select(k => k.AccountId).Distinct()];
        List<long> blockIds = request.PageNumber == 1 ? [.. pageAccountIds, .. idle] : pageAccountIds;

        List<AccountRead> accounts = blockIds.Count == 0
            ? []
            : await _db.Accounts.Where(a => blockIds.Contains(a.AccountId)).ToListAsync(ct);
        List<AccountSum> sums = blockIds.Count == 0
            ? []
            : await AccountSums(scoped, blockIds, start, end, yearStart).ToListAsync(ct);

        List<PostingRow> rows = [];
        PageLookups lookups = new();
        (int earlierCount, decimal earlierNet) = (0, 0m);

        if (keys.Count > 0)
        {
            List<long> ledgerIds = [.. keys.Select(k => k.LedgerId)];
            rows = await Postings(period, ledgerIds).ToListAsync(ct);
            lookups = await LookupsAsync(rows, ct);

            PostingKey first = keys[0];
            IQueryable<JournalLedgerRead> before = Before(period, first.AccountId, first.Date, first.LedgerId);
            earlierCount = await before.CountAsync(ct);
            earlierNet = earlierCount == 0 ? 0m : await before.SumAsync(l => l.DebitAmountBase - l.CreditAmountBase, ct);
        }

        return new AccountTransactionResultDto
        {
            FromDate = start,
            ToDate = end,
            FinancialYearStart = yearStart,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalPostings = totalPostings,
            TotalAccounts = postedAccounts.Count + idle.Count,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            Accounts = Compose(
                keys, rows, accounts, sums, idle, request.PageNumber == 1, earlierCount, earlierNet, lookups),
        };
    }

    private async Task<PageLookups> LookupsAsync(IReadOnlyList<PostingRow> rows, CancellationToken ct)
    {
        List<string> codes = [.. rows.Select(r => r.SourceCode).Distinct()];
        List<long> transactionIds = [.. rows.Select(r => r.TransactionId).Distinct()];
        List<long> contactIds = [.. rows.Where(r => r.ContactId is not null).Select(r => r.ContactId!.Value).Distinct()];
        List<long> subAccountIds = [.. rows.Where(r => r.SubAccountId is not null).Select(r => r.SubAccountId!.Value).Distinct()];
        List<long> journalIds = [.. rows.Select(JournalIdOf).Where(i => i is not null).Select(i => i!.Value).Distinct()];

        List<DocumentLeg> legs = await DocumentLegs(_db.Ledger, codes, transactionIds).ToListAsync(ct);
        List<long> otherAccountIds = [.. legs.Select(l => l.AccountId).Distinct()];

        List<ContactRef> contacts = contactIds.Count == 0 ? [] : await ContactRefs(_db.Contacts, contactIds).ToListAsync(ct);
        List<PermitRef> permits = contactIds.Count == 0 ? [] : await Permits(_db.ContactLicences, contactIds).ToListAsync(ct);
        List<TaxSubAccount> taxSubAccounts = subAccountIds.Count == 0 ? [] : await TaxSubAccounts(_db.SubAccounts, subAccountIds).ToListAsync(ct);
        List<long> groupIds = [.. taxSubAccounts.Select(s => s.TaxGroupId).Distinct()];
        List<TaxRevision> revisions = groupIds.Count == 0 ? [] : await TaxRevisions(_db.TaxMasters, groupIds).ToListAsync(ct);
        List<JournalReference> references = journalIds.Count == 0 ? [] : await References(_db.Journals, journalIds).ToListAsync(ct);
        List<AccountName> names = otherAccountIds.Count == 0 ? [] : await AccountNames(_db.Accounts, otherAccountIds).ToListAsync(ct);

        return new PageLookups
        {
            Legs = legs,
            AccountNames = names.ToDictionary(n => n.AccountId, n => n.Name),
            Contacts = contacts.ToDictionary(c => c.ContactId),
            Permits = permits,
            TaxSubAccounts = taxSubAccounts.ToDictionary(s => s.SubAccountId, s => s.TaxGroupId),
            TaxRevisions = revisions,
            References = references.ToDictionary(r => r.JournalId, r => r.Reference),
        };
    }

    /// <summary>
    /// The accounts asked for by name that have nothing in the period, which are
    /// listed once, on the first page, so their balances can be read. With a contact,
    /// a tax rate or a source chosen the balances are of the narrowed postings and an
    /// account with none of them has nothing to say, so none is listed. Pure.
    /// </summary>
    public static IReadOnlyList<long> IdleAccounts(
        IReadOnlyList<long> chosen, IReadOnlyCollection<long> posted, long? contactId, long? taxGroupId, string? source) =>
        contactId is not null || taxGroupId is not null || !string.IsNullOrEmpty(source)
            ? []
            : [.. chosen.Except(posted)];

    // ---- The builders: LINQ over the read models, so a test runs them over lists ----

    /// <summary>
    /// The ledger legs the non-date filters keep: the chosen accounts, one contact,
    /// one tax rate (the tax legs whose sub-account is that rate's, any revision of
    /// it) and one source. A tax sub-account's reference is its tax group id.
    /// LINQ only.
    /// </summary>
    public static IQueryable<JournalLedgerRead> Scope(
        IQueryable<JournalLedgerRead> ledger,
        IQueryable<SubAccountRead> subAccounts,
        IReadOnlyCollection<long> accountIds,
        long? contactId,
        long? taxGroupId,
        string? source)
    {
        IQueryable<JournalLedgerRead> scoped = ledger;

        if (accountIds.Count > 0)
        {
            scoped = scoped.Where(l => accountIds.Contains(l.AccountId));
        }

        if (contactId is long contact)
        {
            scoped = scoped.Where(l => l.ContactId == contact);
        }

        if (taxGroupId is long group)
        {
            scoped = scoped.Where(l => subAccounts.Any(s =>
                s.SubAccountId == l.SubAccountId && s.ReferenceType == SubAccountReferenceType.Tax && s.ReferenceId == group));
        }

        if (!string.IsNullOrEmpty(source))
        {
            scoped = scoped.Where(l => l.TransactionTypeCode == source);
        }

        return scoped;
    }

    /// <summary>The legs dated in the period, ends included. LINQ only.</summary>
    public static IQueryable<JournalLedgerRead> InPeriod(IQueryable<JournalLedgerRead> scoped, DateOnly start, DateOnly end) =>
        scoped.Where(l => l.LedgerDate >= start && l.LedgerDate <= end);

    /// <summary>The postings in report order: account code, date, ledger id. LINQ only.</summary>
    public static IQueryable<PostingKey> Keys(IQueryable<JournalLedgerRead> period, IQueryable<AccountRead> accounts) =>
        from l in period
        join a in accounts on l.AccountId equals a.AccountId
        orderby a.AccountCode, l.AccountId, l.LedgerDate, l.LedgerId
        select new PostingKey { LedgerId = l.LedgerId, AccountId = l.AccountId, AccountCode = a.AccountCode, Date = l.LedgerDate };

    /// <summary>The legs of an account in the period that come before one posting in the report's order. LINQ only.</summary>
    public static IQueryable<JournalLedgerRead> Before(IQueryable<JournalLedgerRead> period, long accountId, DateOnly date, long ledgerId) =>
        period.Where(l => l.AccountId == accountId && (l.LedgerDate < date || (l.LedgerDate == date && l.LedgerId < ledgerId)));

    /// <summary>
    /// What each account was posted, summed once: before the period, before its
    /// financial year, and the period's own debits, credits and count. One grouped
    /// pass up to the end of the period, with the typed <c>0m</c> Postgres needs to
    /// sum a conditional. LINQ only.
    /// </summary>
    public static IQueryable<AccountSum> AccountSums(
        IQueryable<JournalLedgerRead> scoped,
        IReadOnlyCollection<long> accountIds,
        DateOnly start,
        DateOnly end,
        DateOnly yearStart) =>
        scoped
            .Where(l => accountIds.Contains(l.AccountId) && l.LedgerDate <= end)
            .GroupBy(l => l.AccountId)
            .Select(g => new AccountSum
            {
                AccountId = g.Key,
                NetBefore = g.Sum(l => l.LedgerDate < start ? l.DebitAmountBase - l.CreditAmountBase : 0m),
                NetBeforeYear = g.Sum(l => l.LedgerDate < yearStart ? l.DebitAmountBase - l.CreditAmountBase : 0m),
                Debit = g.Sum(l => l.LedgerDate >= start ? l.DebitAmountBase : 0m),
                Credit = g.Sum(l => l.LedgerDate >= start ? l.CreditAmountBase : 0m),
                Count = g.Count(l => l.LedgerDate >= start),
            });

    /// <summary>The postings of a page, as the ledger holds them. LINQ only.</summary>
    public static IQueryable<PostingRow> Postings(IQueryable<JournalLedgerRead> period, IReadOnlyCollection<long> ledgerIds) =>
        period
            .Where(l => ledgerIds.Contains(l.LedgerId))
            .Select(l => new PostingRow
            {
                LedgerId = l.LedgerId,
                AccountId = l.AccountId,
                Date = l.LedgerDate,
                SourceCode = l.TransactionTypeCode,
                TransactionId = l.TransactionId,
                JournalId = l.JournalId,
                DocumentNo = l.DocumentNo,
                ContactId = l.ContactId,
                SubAccountId = l.SubAccountId,
                Description = l.TransactionDesc,
                CurrencyCode = l.CurrencyCode,
                ExchangeRate = l.ExchangeRate,
                DebitSource = l.DebitAmount,
                CreditSource = l.CreditAmount,
                Debit = l.DebitAmountBase,
                Credit = l.CreditAmountBase,
            });

    /// <summary>
    /// Every leg of the documents on a page, whatever the filters: a posting's other
    /// side is what makes it legible. The codes and ids are the page's, so a few
    /// more documents than the page may be read and are dropped when they are
    /// matched. LINQ only.
    /// </summary>
    public static IQueryable<DocumentLeg> DocumentLegs(
        IQueryable<JournalLedgerRead> ledger,
        IReadOnlyCollection<string> codes,
        IReadOnlyCollection<long> transactionIds) =>
        ledger
            .Where(l => codes.Contains(l.TransactionTypeCode) && transactionIds.Contains(l.TransactionId))
            .Select(l => new DocumentLeg
            {
                SourceCode = l.TransactionTypeCode,
                TransactionId = l.TransactionId,
                LedgerId = l.LedgerId,
                AccountId = l.AccountId,
                IsDebit = l.DebitAmountBase > 0m,
            });

    public static IQueryable<AccountName> AccountNames(IQueryable<AccountRead> accounts, IReadOnlyCollection<long> ids) =>
        accounts.Where(a => ids.Contains(a.AccountId)).Select(a => new AccountName { AccountId = a.AccountId, Name = a.AccountName });

    public static IQueryable<ContactRef> ContactRefs(IQueryable<ContactRead> contacts, IReadOnlyCollection<long> ids) =>
        contacts.Where(c => ids.Contains(c.ContactId)).Select(c => new ContactRef { ContactId = c.ContactId, Code = c.ContactCode, Name = c.DisplayName });

    /// <summary>The contacts' licences that are switched on; which one was valid on a day is decided in <see cref="PermitOn"/>. LINQ only.</summary>
    public static IQueryable<PermitRef> Permits(IQueryable<ContactLicenceRead> licences, IReadOnlyCollection<long> contactIds) =>
        licences
            .Where(c => contactIds.Contains(c.ContactId) && c.IsActive)
            .Select(c => new PermitRef { ContactLicenceId = c.ContactLicenceId, ContactId = c.ContactId, Number = c.LicenceNumber, ExpiresOn = c.ExpiresOn });

    /// <summary>The tax sub-accounts among a page's sub-accounts, with the tax group each belongs to. LINQ only.</summary>
    public static IQueryable<TaxSubAccount> TaxSubAccounts(IQueryable<SubAccountRead> subAccounts, IReadOnlyCollection<long> ids) =>
        subAccounts
            .Where(s => ids.Contains(s.SubAccountId) && s.ReferenceType == SubAccountReferenceType.Tax)
            .Select(s => new TaxSubAccount { SubAccountId = s.SubAccountId, TaxGroupId = s.ReferenceId });

    /// <summary>Every revision of the tax groups on a page; which one was in force on a day is decided in <see cref="TaxOn"/>. LINQ only.</summary>
    public static IQueryable<TaxRevision> TaxRevisions(IQueryable<TaxMasterRead> taxes, IReadOnlyCollection<long> groupIds) =>
        taxes
            .Where(t => groupIds.Contains(t.TaxGroupId) || (t.TaxGroupId == 0 && groupIds.Contains(t.TaxMasterId)))
            .Select(t => new TaxRevision
            {
                TaxGroupId = t.TaxGroupId != 0 ? t.TaxGroupId : t.TaxMasterId,
                Name = t.TaxName,
                Rate = t.TotalRate,
                EffectiveFrom = t.EffectiveFrom,
                EffectiveTo = t.EffectiveTo,
            });

    public static IQueryable<JournalReference> References(IQueryable<JournalRead> journals, IReadOnlyCollection<long> ids) =>
        journals.Where(j => ids.Contains(j.JournalId)).Select(j => new JournalReference { JournalId = j.JournalId, Reference = j.Reference });

    // ---- The pure steps ----

    /// <summary>Asset and expense accounts run debit; liability, equity and income run credit; a contra account runs the other way.</summary>
    public static bool IsDebitNormal(int accountTypeId, bool isContra) =>
        (accountTypeId is Asset or Expense) != isContra;

    /// <summary>
    /// An account's opening balance in debit terms: everything before the period
    /// for a balance-sheet account, and what was posted since the financial year
    /// began for an income or expense account. Pure.
    /// </summary>
    public static decimal OpeningNet(int accountTypeId, AccountSum? sum) =>
        sum is null ? 0m
        : accountTypeId is Income or Expense ? sum.NetBefore - sum.NetBeforeYear
        : sum.NetBefore;

    /// <summary>The manual journal a posting belongs to: its own id on the ledger, else its transaction id when a journal posted it. Pure.</summary>
    public static long? JournalIdOf(PostingRow row) =>
        row.JournalId ?? (row.SourceCode == JournalReportRepository.ManualJournal ? row.TransactionId : null);

    /// <summary>The account a posting's other side went to: its name, "Various" when several, null when none. Pure.</summary>
    public static string? RelatedOf(PostingRow row, IReadOnlyList<DocumentLeg> legs, IReadOnlyDictionary<long, string> names)
    {
        bool isDebit = row.Debit > 0m;
        List<long> others =
        [
            .. legs
                .Where(l => l.SourceCode == row.SourceCode && l.TransactionId == row.TransactionId
                    && l.LedgerId != row.LedgerId && l.IsDebit != isDebit)
                .Select(l => l.AccountId)
                .Distinct(),
        ];

        return others.Count switch
        {
            0 => null,
            1 => names.GetValueOrDefault(others[0]),
            _ => Various,
        };
    }

    /// <summary>The tax revision in force on a date, for a tax group; null when none was. Pure.</summary>
    public static TaxRevision? TaxOn(IReadOnlyList<TaxRevision> revisions, long taxGroupId, DateOnly date) =>
        revisions
            .Where(t => t.TaxGroupId == taxGroupId && t.EffectiveFrom <= date && (t.EffectiveTo is null || t.EffectiveTo >= date))
            .OrderByDescending(t => t.EffectiveFrom)
            .FirstOrDefault();

    /// <summary>The licence a contact held that was valid on a date: the earliest still switched on and not yet expired. Pure.</summary>
    public static string? PermitOn(IReadOnlyList<PermitRef> permits, long contactId, DateOnly date) =>
        permits
            .Where(p => p.ContactId == contactId && (p.ExpiresOn is null || p.ExpiresOn >= date))
            .OrderBy(p => p.ContactLicenceId)
            .Select(p => p.Number)
            .FirstOrDefault();

    /// <summary>
    /// The page's accounts from its postings. Pure.
    ///
    /// The postings arrive in report order. An account's blocks are in order of first
    /// appearance, then any account asked for by name that had nothing, in code order
    /// (first page only). Each account's balances are on its normal side: opening
    /// from <see cref="OpeningNet"/>, closing as opening plus the whole period's
    /// movement. The running balance of its first posting on the page starts from
    /// the opening plus what the page before it already covered, which only the
    /// first account on a page can have.
    /// </summary>
    public static List<AccountTransactionAccountDto> Compose(
        IReadOnlyList<PostingKey> keys,
        IReadOnlyList<PostingRow> rows,
        IReadOnlyList<AccountRead> accounts,
        IReadOnlyList<AccountSum> sums,
        IReadOnlyList<long> idle,
        bool firstPage,
        int earlierCount,
        decimal earlierNet,
        PageLookups lookups)
    {
        Dictionary<long, AccountRead> accountById = accounts.ToDictionary(a => a.AccountId);
        Dictionary<long, AccountSum> sumById = sums.ToDictionary(s => s.AccountId);
        Dictionary<long, PostingRow> rowById = rows.ToDictionary(r => r.LedgerId);
        List<AccountTransactionAccountDto> blocks = [];

        foreach (IGrouping<long, PostingKey> group in keys.GroupBy(k => k.AccountId))
        {
            AccountRead account = accountById[group.Key];
            sumById.TryGetValue(group.Key, out AccountSum? sum);

            // Only the first account on the page can have rows before it.
            bool continues = earlierCount > 0 && group.Key == keys[0].AccountId;
            int before = continues ? earlierCount : 0;
            decimal priorNet = continues ? earlierNet : 0m;

            AccountTransactionAccountDto block = Block(account, sum);
            decimal sign = block.NormalSide == LedgerSide.Debit ? 1m : -1m;
            decimal balance = block.OpeningBalance + (sign * priorNet);

            foreach (PostingKey key in group)
            {
                PostingRow row = rowById[key.LedgerId];
                balance += sign * (row.Debit - row.Credit);
                block.Rows.Add(RowOf(row, balance, lookups));
            }

            block.HasEarlierRows = before > 0;
            block.HasLaterRows = before + block.Rows.Count < block.PostingCount;
            blocks.Add(block);
        }

        if (firstPage)
        {
            // Each goes in before the first account whose code is greater, so the
            // database's own order for the others is left as it was.
            foreach (AccountRead account in idle.Select(id => accountById[id]).OrderBy(a => a.AccountCode, StringComparer.Ordinal).ThenBy(a => a.AccountId))
            {
                sumById.TryGetValue(account.AccountId, out AccountSum? sum);
                int at = blocks.FindIndex(b => string.CompareOrdinal(b.AccountCode, account.AccountCode) > 0);
                blocks.Insert(at < 0 ? blocks.Count : at, Block(account, sum));
            }
        }

        return blocks;
    }

    private static AccountTransactionAccountDto Block(AccountRead account, AccountSum? sum)
    {
        bool debitNormal = IsDebitNormal(account.AccountTypeId, account.IsContra);
        decimal sign = debitNormal ? 1m : -1m;
        decimal opening = sign * OpeningNet(account.AccountTypeId, sum);
        decimal debit = sum?.Debit ?? 0m;
        decimal credit = sum?.Credit ?? 0m;

        return new AccountTransactionAccountDto
        {
            AccountId = account.AccountId,
            AccountCode = account.AccountCode,
            AccountName = account.AccountName,
            AccountTypeId = account.AccountTypeId,
            IsContra = account.IsContra,
            NormalSide = debitNormal ? LedgerSide.Debit : LedgerSide.Credit,
            OpeningBalance = opening,
            ClosingBalance = opening + (sign * (debit - credit)),
            TotalDebit = debit,
            TotalCredit = credit,
            PostingCount = sum?.Count ?? 0,
        };
    }

    private static AccountTransactionRowDto RowOf(PostingRow row, decimal runningBalance, PageLookups lookups)
    {
        lookups.Contacts.TryGetValue(row.ContactId ?? 0, out ContactRef? contact);

        TaxRevision? tax = row.SubAccountId is long sub && lookups.TaxSubAccounts.TryGetValue(sub, out long group)
            ? TaxOn(lookups.TaxRevisions, group, row.Date)
            : null;

        long? journalId = JournalIdOf(row);

        return new AccountTransactionRowDto
        {
            LedgerId = row.LedgerId,
            Date = row.Date,
            DocumentNo = JournalReportRepository.DocumentNoOf(row.DocumentNo, row.SourceCode, row.TransactionId),
            SourceCode = row.SourceCode,
            TransactionId = row.TransactionId,
            JournalId = row.JournalId,
            RelatedAccount = RelatedOf(row, lookups.Legs, lookups.AccountNames),
            ContactId = row.ContactId,
            ContactCode = contact?.Code,
            ContactName = contact?.Name,
            Description = row.Description,
            Reference = journalId is long j ? lookups.References.GetValueOrDefault(j) : null,
            TaxRate = tax?.Rate,
            TaxName = tax?.Name,
            PermitNo = row.ContactId is long contactId ? PermitOn(lookups.Permits, contactId, row.Date) : null,
            CurrencyCode = row.CurrencyCode,
            ExchangeRate = row.ExchangeRate,
            DebitSource = row.DebitSource,
            CreditSource = row.CreditSource,
            Debit = row.Debit,
            Credit = row.Credit,
            RunningBalance = runningBalance,
        };
    }
}

/// <summary>A posting's place in the report: its account, its date and its ledger id, which together order it.</summary>
public sealed class PostingKey
{
    public long LedgerId { get; set; }

    public long AccountId { get; set; }

    public string AccountCode { get; set; } = string.Empty;

    public DateOnly Date { get; set; }
}

/// <summary>An account's postings summed, in debit terms, before they are joined to the account.</summary>
public sealed class AccountSum
{
    public long AccountId { get; set; }

    /// <summary>Debits less credits dated before the period.</summary>
    public decimal NetBefore { get; set; }

    /// <summary>Debits less credits dated before the financial year the period starts in.</summary>
    public decimal NetBeforeYear { get; set; }

    public decimal Debit { get; set; }

    public decimal Credit { get; set; }

    public int Count { get; set; }
}

/// <summary>One ledger leg as the report reads it, before its names are joined on.</summary>
public sealed class PostingRow
{
    public long LedgerId { get; set; }

    public long AccountId { get; set; }

    public DateOnly Date { get; set; }

    public string SourceCode { get; set; } = string.Empty;

    public long TransactionId { get; set; }

    public long? JournalId { get; set; }

    public string? DocumentNo { get; set; }

    public long? ContactId { get; set; }

    public long? SubAccountId { get; set; }

    public string? Description { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal ExchangeRate { get; set; }

    public decimal DebitSource { get; set; }

    public decimal CreditSource { get; set; }

    public decimal Debit { get; set; }

    public decimal Credit { get; set; }
}

/// <summary>One leg of a document on the page, as far as its other side is concerned.</summary>
public sealed class DocumentLeg
{
    public string SourceCode { get; set; } = string.Empty;

    public long TransactionId { get; set; }

    public long LedgerId { get; set; }

    public long AccountId { get; set; }

    public bool IsDebit { get; set; }
}

public sealed class AccountName
{
    public long AccountId { get; set; }

    public string Name { get; set; } = string.Empty;
}

public sealed class ContactRef
{
    public long ContactId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}

public sealed class PermitRef
{
    public long ContactLicenceId { get; set; }

    public long ContactId { get; set; }

    public string Number { get; set; } = string.Empty;

    public DateOnly? ExpiresOn { get; set; }
}

public sealed class TaxSubAccount
{
    public long SubAccountId { get; set; }

    public long TaxGroupId { get; set; }
}

public sealed class TaxRevision
{
    public long TaxGroupId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Rate { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }
}

public sealed class JournalReference
{
    public long JournalId { get; set; }

    public string? Reference { get; set; }
}

/// <summary>Everything a page's postings are named from, fetched in one batch each.</summary>
public sealed class PageLookups
{
    public IReadOnlyList<DocumentLeg> Legs { get; set; } = [];

    public IReadOnlyDictionary<long, string> AccountNames { get; set; } = new Dictionary<long, string>();

    public IReadOnlyDictionary<long, ContactRef> Contacts { get; set; } = new Dictionary<long, ContactRef>();

    public IReadOnlyList<PermitRef> Permits { get; set; } = [];

    /// <summary>A tax sub-account's id to its tax group.</summary>
    public IReadOnlyDictionary<long, long> TaxSubAccounts { get; set; } = new Dictionary<long, long>();

    public IReadOnlyList<TaxRevision> TaxRevisions { get; set; } = [];

    public IReadOnlyDictionary<long, string?> References { get; set; } = new Dictionary<long, string?>();
}
