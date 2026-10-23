using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Journal Report (TK-217): every journal posted in a period with its lines,
/// who created and posted it and its status.
///
/// <b>A journal is a document, and the ledger is where every one of them is.</b>
/// A manual journal, and the posting a sales invoice, a bill, a payment or a stock
/// adjustment made, all reach <c>acc.JournalLedger</c> under one document key, the
/// type and the id. The report groups the ledger by that key, so the period's
/// totals are the ledger's and equal the Trial Balance's movement for the same
/// dates, and takes the header of a manual journal (its number, reference, memo,
/// status, who created and posted it, what it reverses or was reversed by) from
/// <c>acc.Journals</c>, which is the one kind of document that has one. A
/// document's own posting is Posted, and its creator is who posted it.
///
/// <b>Drafts are the exception</b> and are read from <c>acc.Journals</c> and
/// <c>acc.JournalDetails</c>, because a draft has no ledger rows. They are listed
/// only when Draft is asked for, and they are not part of any ledger total.
///
/// <b>The result comes a page of journals at a time.</b> The journals in the
/// period are counted and added up in the database, a page of their keys is read
/// in date order, and only those journals' lines are fetched, so the report reads
/// a page however long the period is. The users who created and posted are
/// resolved from Master in one batched call, never per row. LINQ only, over the
/// read models, so a test runs the builders over lists.
/// </summary>
public sealed class JournalReportRepository
{
    /// <summary>The document type of a manual journal, and of the journals the system posts to the ledger (<c>mst.TransactionTypes</c> JRN).</summary>
    public const string ManualJournal = "JRN";

    private readonly ReportingDbContext _db;
    private readonly BatchedNameResolverRepository _master;

    public JournalReportRepository(ReportingDbContext db, BatchedNameResolverRepository master)
    {
        _db = db;
        _master = master;
    }

    /// <summary>The refusal a period needs beyond both dates being given, with the field it belongs to. Pure.</summary>
    public static IReadOnlyList<(string Field, string Message)> DateRefusals(JournalReportRequest request) =>
        request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from
            ? [(nameof(JournalReportRequest.ToDate), "To date cannot be before the from date.")]
            : [];

    /// <summary>Whether an account is in this branch's chart; the query filter decides, so another branch's reads as none.</summary>
    public Task<bool> AccountExistsAsync(long accountId, CancellationToken ct) =>
        _db.Accounts.AnyAsync(a => a.AccountId == accountId, ct);

    /// <summary>A page of the period's journals for a request the controller has already validated.</summary>
    public async Task<JournalReportResultDto> RunAsync(JournalReportRequest request, CancellationToken ct)
    {
        DateOnly from = request.FromDate!.Value;
        DateOnly to = request.ToDate!.Value;
        int skip = (request.PageNumber - 1) * request.PageSize;

        if (request.Status == JournalStatus.Draft)
        {
            return await DraftsAsync(request, from, to, skip, ct);
        }

        IQueryable<JournalLedgerRead> legs = Legs(
            _db.Ledger, _db.Journals, from, to, request.Source, request.Status, request.AccountId);

        int totalJournals = await Keys(legs).CountAsync(ct);
        decimal totalDebit = await legs.SumAsync(l => l.DebitAmountBase, ct);
        decimal totalCredit = await legs.SumAsync(l => l.CreditAmountBase, ct);

        List<JournalKey> keys = await Keys(legs)
            .OrderBy(k => k.Date).ThenBy(k => k.FirstLedgerId)
            .Skip(skip).Take(request.PageSize)
            .ToListAsync(ct);

        List<string> codes = [.. keys.Select(k => k.SourceCode).Distinct()];
        List<long> ids = [.. keys.Select(k => k.TransactionId).Distinct()];

        List<JournalLineRow> lines = codes.Count == 0
            ? []
            : await Lines(legs, _db.Accounts, _db.Contacts, codes, ids).ToListAsync(ct);

        List<long> journalIds = [.. keys.Where(k => k.SourceCode == ManualJournal).Select(k => k.TransactionId)];
        List<JournalHeader> headers = journalIds.Count == 0 ? [] : await Headers(_db.Journals, journalIds).ToListAsync(ct);

        return await ShapeAsync(
            request, from, to, keys, lines, headers, totalJournals, totalDebit, totalCredit, ct);
    }

    private async Task<JournalReportResultDto> DraftsAsync(
        JournalReportRequest request, DateOnly from, DateOnly to, int skip, CancellationToken ct)
    {
        IQueryable<JournalRead> drafts = DraftJournals(_db.Journals, _db.JournalDetails, from, to, request.Source, request.AccountId);

        int totalJournals = await drafts.CountAsync(ct);
        decimal totalDebit = await DraftAmounts(drafts, _db.JournalDetails).SumAsync(d => d.DebitAmountBase, ct);
        decimal totalCredit = await DraftAmounts(drafts, _db.JournalDetails).SumAsync(d => d.CreditAmountBase, ct);

        List<JournalRead> page = await drafts
            .OrderBy(j => j.JournalDate).ThenBy(j => j.JournalId)
            .Skip(skip).Take(request.PageSize)
            .ToListAsync(ct);

        List<long> ids = [.. page.Select(j => j.JournalId)];
        List<JournalLineRow> lines = ids.Count == 0
            ? []
            : await DraftLines(_db.Journals, _db.JournalDetails, _db.Accounts, ids).ToListAsync(ct);

        List<JournalKey> keys =
        [
            .. page.Select(j => new JournalKey
            {
                SourceCode = ManualJournal, TransactionId = j.JournalId, Date = j.JournalDate, FirstLedgerId = j.JournalId,
            }),
        ];
        List<JournalHeader> headers = [.. page.Select(HeaderOf)];

        return await ShapeAsync(
            request, from, to, keys, lines, headers, totalJournals, totalDebit, totalCredit, ct);
    }

    private async Task<JournalReportResultDto> ShapeAsync(
        JournalReportRequest request,
        DateOnly from,
        DateOnly to,
        List<JournalKey> keys,
        List<JournalLineRow> lines,
        List<JournalHeader> headers,
        int totalJournals,
        decimal totalDebit,
        decimal totalCredit,
        CancellationToken ct)
    {
        // The numbers of the journals these reverse or were reversed by, and the
        // users who made them: each one call, however many journals are on the page.
        List<long> related =
        [
            .. headers.SelectMany(h => new[] { h.ReversesJournalId, h.ReversedByJournalId })
                .Where(id => id is not null).Select(id => id!.Value).Distinct(),
        ];

        Dictionary<long, string?> numbers = related.Count == 0
            ? []
            : (await _db.Journals.Where(j => related.Contains(j.JournalId)).Select(j => new { j.JournalId, j.JournalNo }).ToListAsync(ct))
                .ToDictionary(j => j.JournalId, j => j.JournalNo);

        Dictionary<Guid, string> names = await UserNamesAsync(keys, lines, headers, ct);

        return new JournalReportResultDto
        {
            FromDate = from,
            ToDate = to,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalJournals = totalJournals,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            IsBalanced = totalDebit == totalCredit,
            Journals = Compose(keys, lines, headers, numbers, names),
        };
    }

    /// <summary>
    /// The names of the users behind a page, in one batched call. A name that cannot
    /// be read is left blank rather than failing the report: it is drawn on a screen
    /// and is not part of any figure.
    /// </summary>
    private async Task<Dictionary<Guid, string>> UserNamesAsync(
        IReadOnlyList<JournalKey> keys,
        IReadOnlyList<JournalLineRow> lines,
        IReadOnlyList<JournalHeader> headers,
        CancellationToken ct)
    {
        IEnumerable<Guid?> users = headers.SelectMany(h => new[] { h.CreatedBy, h.PostedBy })
            .Concat(lines.Select(l => l.CreatedBy));

        try
        {
            return keys.Count == 0 ? [] : await _master.GetUserNamesAsync(users, ct);
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    // ---- The builders: LINQ over the read models, so a test runs them over lists ----

    /// <summary>
    /// The ledger legs of the journals the filters match: those in the period, of
    /// one source when asked, and of one status when asked (Reversed is a manual
    /// journal that has been reversed, Posted is every journal that has not), and
    /// when an account is asked for, every leg of each journal that has a leg on
    /// it, so the journal still balances. LINQ only.
    /// </summary>
    public static IQueryable<JournalLedgerRead> Legs(
        IQueryable<JournalLedgerRead> ledger,
        IQueryable<JournalRead> journals,
        DateOnly start,
        DateOnly end,
        string? source,
        JournalStatus? status,
        long? accountId)
    {
        IQueryable<JournalLedgerRead> inPeriod = ledger.Where(l =>
            l.LedgerDate >= start && l.LedgerDate <= end && (source == null || l.TransactionTypeCode == source));

        if (status == JournalStatus.Reversed)
        {
            inPeriod = inPeriod.Where(l => l.TransactionTypeCode == ManualJournal
                && journals.Any(j => j.JournalId == l.TransactionId && j.Status == JournalStatus.Reversed));
        }
        else if (status == JournalStatus.Posted)
        {
            inPeriod = inPeriod.Where(l => !(l.TransactionTypeCode == ManualJournal
                && journals.Any(j => j.JournalId == l.TransactionId && j.Status == JournalStatus.Reversed)));
        }

        if (accountId is null)
        {
            return inPeriod;
        }

        IQueryable<JournalLedgerRead> touching = inPeriod.Where(l => l.AccountId == accountId);
        return inPeriod.Where(l => touching.Any(t => t.TransactionTypeCode == l.TransactionTypeCode && t.TransactionId == l.TransactionId));
    }

    /// <summary>One row for each journal in the legs: its key, its date and its first ledger row, which orders it. LINQ only.</summary>
    public static IQueryable<JournalKey> Keys(IQueryable<JournalLedgerRead> legs) =>
        legs
            .GroupBy(l => new { l.TransactionTypeCode, l.TransactionId })
            .Select(g => new JournalKey
            {
                SourceCode = g.Key.TransactionTypeCode,
                TransactionId = g.Key.TransactionId,
                Date = g.Min(l => l.LedgerDate),
                FirstLedgerId = g.Min(l => l.LedgerId),
            });

    /// <summary>
    /// The lines of the journals on a page, each with its account and contact. Left
    /// join to the contact, because most legs name none. The codes and ids are the
    /// page's, so a few more journals than the page may be read and are dropped when
    /// the entries are composed. LINQ only.
    /// </summary>
    public static IQueryable<JournalLineRow> Lines(
        IQueryable<JournalLedgerRead> legs,
        IQueryable<AccountRead> accounts,
        IQueryable<ContactRead> contacts,
        IReadOnlyCollection<string> codes,
        IReadOnlyCollection<long> ids) =>
        from l in legs
        where codes.Contains(l.TransactionTypeCode) && ids.Contains(l.TransactionId)
        join a in accounts on l.AccountId equals a.AccountId
        join c in contacts on l.ContactId equals (long?)c.ContactId into cs
        from c in cs.DefaultIfEmpty()
        orderby l.TransactionId, l.TransactionDetailId, l.LedgerId
        select new JournalLineRow
        {
            SourceCode = l.TransactionTypeCode,
            TransactionId = l.TransactionId,
            RowId = l.LedgerId,
            LineNumber = (int)l.TransactionDetailId,
            AccountId = a.AccountId,
            AccountCode = a.AccountCode,
            AccountName = a.AccountName,
            ContactId = l.ContactId,
            ContactName = c == null ? null : c.DisplayName,
            Description = l.TransactionDesc,
            DocumentNo = l.DocumentNo,
            CurrencyCode = l.CurrencyCode,
            ExchangeRate = l.ExchangeRate,
            DebitSource = l.DebitAmount,
            CreditSource = l.CreditAmount,
            Debit = l.DebitAmountBase,
            Credit = l.CreditAmountBase,
            CreatedBy = l.CreatedBy,
            CreatedAt = l.CreatedAt,
        };

    /// <summary>The headers of manual journals by id. LINQ only.</summary>
    public static IQueryable<JournalHeader> Headers(IQueryable<JournalRead> journals, IReadOnlyCollection<long> ids) =>
        journals
            .Where(j => ids.Contains(j.JournalId))
            .Select(j => new JournalHeader
            {
                JournalId = j.JournalId,
                JournalNo = j.JournalNo,
                Reference = j.Reference,
                Memo = j.Memo,
                Status = j.Status,
                CreatedBy = j.CreatedBy,
                CreatedAt = j.CreatedAt,
                PostedBy = j.PostedBy,
                PostedAt = j.PostedAt,
                ReversesJournalId = j.ReversesJournalId,
                ReversedByJournalId = j.ReversedByJournalId,
            });

    /// <summary>A journal read as a header; the draft path's, which already has the journal in hand. Pure.</summary>
    public static JournalHeader HeaderOf(JournalRead j) =>
        new()
        {
            JournalId = j.JournalId,
            JournalNo = j.JournalNo,
            Reference = j.Reference,
            Memo = j.Memo,
            Status = j.Status,
            CreatedBy = j.CreatedBy,
            CreatedAt = j.CreatedAt,
            PostedBy = j.PostedBy,
            PostedAt = j.PostedAt,
            ReversesJournalId = j.ReversesJournalId,
            ReversedByJournalId = j.ReversedByJournalId,
        };

    /// <summary>
    /// Draft journals in the period, which have no ledger rows. Only a manual
    /// journal can be a draft, so a source other than JRN matches none; with an
    /// account, the drafts that have a line on it. LINQ only.
    /// </summary>
    public static IQueryable<JournalRead> DraftJournals(
        IQueryable<JournalRead> journals,
        IQueryable<JournalDetailRead> details,
        DateOnly start,
        DateOnly end,
        string? source,
        long? accountId) =>
        journals.Where(j =>
            j.Status == JournalStatus.Draft
            && j.JournalDate >= start && j.JournalDate <= end
            && (source == null || source == ManualJournal)
            && (accountId == null || details.Any(d => d.JournalId == j.JournalId && d.AccountId == accountId)));

    /// <summary>The lines of those drafts, to be added up. LINQ only.</summary>
    public static IQueryable<JournalDetailRead> DraftAmounts(IQueryable<JournalRead> drafts, IQueryable<JournalDetailRead> details) =>
        details.Where(d => drafts.Any(j => j.JournalId == d.JournalId));

    /// <summary>The lines of the drafts on a page, with their accounts and the currency and rate of their journal. LINQ only.</summary>
    public static IQueryable<JournalLineRow> DraftLines(
        IQueryable<JournalRead> journals,
        IQueryable<JournalDetailRead> details,
        IQueryable<AccountRead> accounts,
        IReadOnlyCollection<long> ids) =>
        from d in details
        where ids.Contains(d.JournalId)
        join j in journals on d.JournalId equals j.JournalId
        join a in accounts on d.AccountId equals a.AccountId
        orderby d.JournalId, d.LineNumber
        select new JournalLineRow
        {
            SourceCode = ManualJournal,
            TransactionId = d.JournalId,
            RowId = d.JournalDetailId,
            LineNumber = d.LineNumber,
            AccountId = a.AccountId,
            AccountCode = a.AccountCode,
            AccountName = a.AccountName,
            ContactId = null,
            ContactName = null,
            Description = d.LineMemo,
            DocumentNo = j.JournalNo,
            CurrencyCode = j.CurrencyCode,
            ExchangeRate = j.ExchangeRate,
            DebitSource = d.DebitAmount,
            CreditSource = d.CreditAmount,
            Debit = d.DebitAmountBase,
            Credit = d.CreditAmountBase,
            CreatedBy = null,
            CreatedAt = null,
        };

    /// <summary>
    /// The page's journals from their keys, lines and headers. Pure.
    ///
    /// A manual journal takes its number, reference, narration, status and its
    /// creator and poster from its header, and shows the number of the journal it
    /// reverses or was reversed by. A document's own posting is Posted, takes its
    /// number from the first line that has one, and its creator and poster are the
    /// user who posted it. Each journal's lines are added up in base currency, and
    /// <c>IsBalanced</c> says whether the two totals agree.
    /// </summary>
    public static List<JournalReportEntryDto> Compose(
        IReadOnlyList<JournalKey> keys,
        IReadOnlyList<JournalLineRow> lines,
        IReadOnlyList<JournalHeader> headers,
        IReadOnlyDictionary<long, string?> numbers,
        IReadOnlyDictionary<Guid, string> names)
    {
        Dictionary<long, JournalHeader> headerById = headers.ToDictionary(h => h.JournalId);
        ILookup<(string, long), JournalLineRow> linesByKey = lines.ToLookup(l => (l.SourceCode, l.TransactionId));

        string? NameOf(Guid? user) => user is Guid id && names.TryGetValue(id, out string? name) ? name : null;
        string NumberOf(long id) => numbers.GetValueOrDefault(id) ?? $"{ManualJournal}-{id}";

        List<JournalReportEntryDto> entries = [];

        foreach (JournalKey key in keys)
        {
            List<JournalLineRow> own = [.. linesByKey[(key.SourceCode, key.TransactionId)].OrderBy(l => l.LineNumber).ThenBy(l => l.RowId)];
            headerById.TryGetValue(key.TransactionId, out JournalHeader? header);
            bool isJournal = key.SourceCode == ManualJournal && header is not null;
            JournalLineRow? first = own.FirstOrDefault();

            string number = isJournal
                ? header!.JournalNo
                    ?? (header.Status == JournalStatus.Draft ? $"Draft {key.TransactionId}" : $"{ManualJournal}-{key.TransactionId}")
                : DocumentNoOf(own.Select(l => l.DocumentNo).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)), key.SourceCode, key.TransactionId);

            decimal debit = own.Sum(l => l.Debit);
            decimal credit = own.Sum(l => l.Credit);

            entries.Add(new JournalReportEntryDto
            {
                SourceCode = key.SourceCode,
                TransactionId = key.TransactionId,
                JournalId = isJournal ? key.TransactionId : null,
                Date = key.Date,
                Number = number,
                Reference = isJournal ? header!.Reference : null,
                Narration = isJournal ? header!.Memo ?? first?.Description : first?.Description,
                Status = isJournal ? header!.Status : JournalStatus.Posted,
                CreatedByName = NameOf(isJournal ? header!.CreatedBy : first?.CreatedBy),
                CreatedAt = isJournal ? header!.CreatedAt : first?.CreatedAt,
                PostedByName = isJournal ? NameOf(header!.PostedBy) : NameOf(first?.CreatedBy),
                PostedAt = isJournal ? header!.PostedAt : first?.CreatedAt,
                ReversesNumber = isJournal && header!.ReversesJournalId is long reverses ? NumberOf(reverses) : null,
                ReversedByNumber = isJournal && header!.ReversedByJournalId is long reversedBy ? NumberOf(reversedBy) : null,
                Lines = [.. own.Select(l => new JournalReportLineDto
                {
                    LineNumber = l.LineNumber,
                    AccountId = l.AccountId,
                    AccountCode = l.AccountCode,
                    AccountName = l.AccountName,
                    ContactId = l.ContactId,
                    ContactName = l.ContactName,
                    Description = l.Description,
                    CurrencyCode = l.CurrencyCode,
                    ExchangeRate = l.ExchangeRate,
                    DebitSource = l.DebitSource,
                    CreditSource = l.CreditSource,
                    Debit = l.Debit,
                    Credit = l.Credit,
                })],
                TotalDebit = debit,
                TotalCredit = credit,
                IsBalanced = debit == credit,
            });
        }

        return entries;
    }

    /// <summary>The document's number, or for a row written before the ledger carried one, its type and id. Pure.</summary>
    public static string DocumentNoOf(string? documentNo, string sourceCode, long transactionId) =>
        string.IsNullOrWhiteSpace(documentNo) ? $"{sourceCode}-{transactionId}" : documentNo;
}

/// <summary>A journal's place in the report: its document key, its date and its first ledger row.</summary>
public sealed class JournalKey
{
    public string SourceCode { get; set; } = string.Empty;

    public long TransactionId { get; set; }

    public DateOnly Date { get; set; }

    /// <summary>The lowest ledger id among its legs; for a draft, its journal id. Orders journals on one date.</summary>
    public long FirstLedgerId { get; set; }
}

/// <summary>A manual journal's header, as the report needs it.</summary>
public sealed class JournalHeader
{
    public long JournalId { get; set; }

    public string? JournalNo { get; set; }

    public string? Reference { get; set; }

    public string? Memo { get; set; }

    public JournalStatus Status { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }

    public Guid? PostedBy { get; set; }

    public DateTimeOffset? PostedAt { get; set; }

    public long? ReversesJournalId { get; set; }

    public long? ReversedByJournalId { get; set; }
}

/// <summary>One line of a journal as read, before its journal is composed.</summary>
public sealed class JournalLineRow
{
    public string SourceCode { get; set; } = string.Empty;

    public long TransactionId { get; set; }

    /// <summary>The ledger row's id, or a draft line's id; orders two lines with the same line number.</summary>
    public long RowId { get; set; }

    public int LineNumber { get; set; }

    public long AccountId { get; set; }

    public string AccountCode { get; set; } = string.Empty;

    public string AccountName { get; set; } = string.Empty;

    public long? ContactId { get; set; }

    public string? ContactName { get; set; }

    public string? Description { get; set; }

    public string? DocumentNo { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal ExchangeRate { get; set; }

    public decimal DebitSource { get; set; }

    public decimal CreditSource { get; set; }

    public decimal Debit { get; set; }

    public decimal Credit { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }
}
