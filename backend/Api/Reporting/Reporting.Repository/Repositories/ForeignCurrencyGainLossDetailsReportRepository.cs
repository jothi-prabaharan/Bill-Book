using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Foreign Currency Gain or Loss Details report (TK-221): the gains and losses of
/// <see cref="ForeignCurrencyGainLossReportRepository"/> document by document. <b>Its
/// currency and account totals are that report's</b>: the same builders and the same
/// <c>Compose</c> produce them from the same legs, with the document rows attached under
/// each account, so the two cannot disagree for the same dates and rates.
///
/// <b>Open documents carry the unrealized amount.</b> A balance is made of documents: an
/// invoice owed, then the receipts that settle it. A receipt's line says which document
/// it settles (<c>MappingTransactionTypeCode</c> and <c>MappingTransactionId</c> on the
/// money document's line), and it comes off the balance at the rate that document was
/// booked at, so the receipt's receivable leg is counted with the invoice it settles. What
/// is left is the document's open amount, and its difference from the booked rate at the
/// revaluation rate is its unrealized gain or loss. A receipt that settles nothing in
/// particular, an advance, is a document of its own with a negative balance. Documents
/// that net to nothing are not listed. A bank account's legs have no document to settle,
/// so each is its own.
///
/// <b>Settlements carry the realized amount</b>, each on the date it was posted: one row
/// for each exchange difference in the period, with the document it settled, the rate that
/// document was booked at and the rate the money moved at. A document's amount is rounded
/// on its own, so its rows can differ from the account's figure, which is rounded once, by
/// a paisa or two; the account and currency totals are always the summary's.
/// </summary>
public sealed class ForeignCurrencyGainLossDetailsReportRepository
{
    /// <summary>
    /// The transaction type codes of Accounting's receive money and spend money documents,
    /// written as literals because this project does not reference Accounting's enums.
    /// </summary>
    public const string ReceiveMoneyCode = "RCM";

    public const string SpendMoneyCode = "SPM";

    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public ForeignCurrencyGainLossDetailsReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>
    /// What a request needs beyond both dates being given, each with the field it belongs
    /// to: the to date not before the from date, a currency that is a three-letter code,
    /// and a rate for each currency that is above nought. The summary's rules, named for
    /// this request's fields. Pure.
    /// </summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(ForeignCurrencyGainLossDetailsRequest request)
    {
        IReadOnlyList<(string Field, string Message)> shared = ForeignCurrencyGainLossReportRepository.Refusals(
            new ForeignCurrencyGainLossReportRequest
            {
                FromDate = request.FromDate,
                AsAtDate = request.ToDate,
                CurrencyCode = request.CurrencyCode,
                Rates = request.Rates,
            });

        return
        [
            .. shared.Select(r => r.Field == nameof(ForeignCurrencyGainLossReportRequest.AsAtDate)
                ? (nameof(ForeignCurrencyGainLossDetailsRequest.ToDate), "To date cannot be before the from date.")
                : r),
        ];
    }

    /// <summary>Whether a contact is on this branch; the query filter decides, so another branch's reads as none.</summary>
    public Task<bool> ContactExistsAsync(long contactId, CancellationToken ct) =>
        _db.Contacts.AnyAsync(c => c.ContactId == contactId, ct);

    /// <summary>Whether an account is on this branch.</summary>
    public Task<bool> AccountExistsAsync(long accountId, CancellationToken ct) =>
        _db.Accounts.AnyAsync(a => a.AccountId == accountId, ct);

    /// <summary>The report for a request the controller has already validated.</summary>
    public async Task<ForeignCurrencyGainLossDetailsResultDto> RunAsync(
        ForeignCurrencyGainLossDetailsRequest request, CancellationToken ct)
    {
        DateOnly start = request.FromDate!.Value;
        DateOnly end = request.ToDate!.Value;
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;
        Dictionary<string, decimal> rates = ForeignCurrencyGainLossReportRepository.RatesOf(
            new ForeignCurrencyGainLossReportRequest { Rates = request.Rates });

        // Without a base currency nothing is foreign: there is nothing to answer, and no guess to make.
        if (baseCurrency.Length == 0)
        {
            return Compose(start, end, baseCurrency, [], [], [], [], [], new Dictionary<(string, long), string>(), new Dictionary<long, string>(), rates, null, null, null);
        }

        List<ForeignLeg> legs = await ForeignCurrencyGainLossReportRepository
            .ForeignLegs(_db.Ledger, _db.Accounts, baseCurrency)
            .Where(l => l.LedgerDate <= end)
            .ToListAsync(ct);
        List<FxRealizedLeg> realized = await RealizedLegDetails(_db.Ledger, _db.Accounts, baseCurrency, start, end).ToListAsync(ct);
        List<FxRealizedLeg> unassigned = await UnassignedRealizedLegDetails(_db.Ledger, _db.Accounts, baseCurrency, start, end).ToListAsync(ct);

        List<long> receiveIds = [.. IdsOf(ReceiveMoneyCode, legs, realized, unassigned)];
        List<long> spendIds = [.. IdsOf(SpendMoneyCode, legs, realized, unassigned)];

        List<FxSettlementLine> lines = [];
        Dictionary<(string Code, long Id), string> moneyNumbers = [];

        if (receiveIds.Count > 0)
        {
            lines.AddRange(await _db.ReceiveMoneyDetails
                .Where(d => receiveIds.Contains(d.ReceiveMoneyId) && d.MappingTransactionId != null)
                .Select(d => new FxSettlementLine
                {
                    Code = ReceiveMoneyCode,
                    Id = d.ReceiveMoneyId,
                    Line = d.LineNumber,
                    MappedCode = d.MappingTransactionTypeCode!,
                    MappedId = d.MappingTransactionId!.Value,
                })
                .ToListAsync(ct));

            foreach (var m in await _db.ReceiveMoney.Where(m => receiveIds.Contains(m.ReceiveMoneyId) && m.TransactionNo != null)
                         .Select(m => new { m.ReceiveMoneyId, m.TransactionNo }).ToListAsync(ct))
            {
                moneyNumbers[(ReceiveMoneyCode, m.ReceiveMoneyId)] = m.TransactionNo!;
            }
        }

        if (spendIds.Count > 0)
        {
            lines.AddRange(await _db.SpendMoneyDetails
                .Where(d => spendIds.Contains(d.SpendMoneyId) && d.MappingTransactionId != null)
                .Select(d => new FxSettlementLine
                {
                    Code = SpendMoneyCode,
                    Id = d.SpendMoneyId,
                    Line = d.LineNumber,
                    MappedCode = d.MappingTransactionTypeCode!,
                    MappedId = d.MappingTransactionId!.Value,
                })
                .ToListAsync(ct));

            foreach (var m in await _db.SpendMoney.Where(m => spendIds.Contains(m.SpendMoneyId) && m.TransactionNo != null)
                         .Select(m => new { m.SpendMoneyId, m.TransactionNo }).ToListAsync(ct))
            {
                moneyNumbers[(SpendMoneyCode, m.SpendMoneyId)] = m.TransactionNo!;
            }
        }

        List<long> accountIds = [.. legs.Select(l => l.AccountId).Union(realized.Select(r => r.AccountId)).Where(id => id != 0L).Distinct()];
        List<AccountRead> accounts = await _db.Accounts.Where(a => accountIds.Contains(a.AccountId)).ToListAsync(ct);

        List<long> contactIds =
        [
            .. legs.Where(l => l.ContactId != null).Select(l => l.ContactId!.Value)
                .Union(realized.Where(r => r.ContactId != null).Select(r => r.ContactId!.Value))
                .Union(unassigned.Where(r => r.ContactId != null).Select(r => r.ContactId!.Value))
                .Distinct(),
        ];

        Dictionary<long, string> contactNames = (await _db.Contacts.Where(c => contactIds.Contains(c.ContactId))
                .Select(c => new { c.ContactId, c.DisplayName }).ToListAsync(ct))
            .ToDictionary(c => c.ContactId, c => c.DisplayName);

        string? only = string.IsNullOrWhiteSpace(request.CurrencyCode) ? null : request.CurrencyCode.Trim().ToUpperInvariant();

        return Compose(start, end, baseCurrency, accounts, legs, realized, unassigned, lines, moneyNumbers, contactNames, rates, only, request.ContactId, request.AccountId);
    }

    /// <summary>
    /// What the filters offer at a date: the foreign currencies with a posting up to it and
    /// whether each still has a balance (the ones the page asks a rate for), the accounts
    /// and the contacts with a posting.
    /// </summary>
    public async Task<ForeignCurrencyDetailChoicesDto> ChoicesAsync(DateOnly end, CancellationToken ct)
    {
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

        if (baseCurrency.Length == 0)
        {
            return new ForeignCurrencyDetailChoicesDto();
        }

        List<ForeignLeg> legs = await ForeignCurrencyGainLossReportRepository
            .ForeignLegs(_db.Ledger, _db.Accounts, baseCurrency)
            .Where(l => l.LedgerDate <= end)
            .ToListAsync(ct);

        List<long> accountIds = [.. legs.Select(l => l.AccountId).Distinct()];
        List<AccountRead> accounts = await _db.Accounts.Where(a => accountIds.Contains(a.AccountId)).ToListAsync(ct);

        List<long> contactIds = [.. legs.Where(l => l.ContactId != null).Select(l => l.ContactId!.Value).Distinct()];
        List<ForeignCurrencyContactChoiceDto> contacts =
        [
            .. (await _db.Contacts.Where(c => contactIds.Contains(c.ContactId)).Select(c => new { c.ContactId, c.DisplayName }).ToListAsync(ct))
                .OrderBy(c => c.DisplayName, StringComparer.Ordinal).ThenBy(c => c.ContactId)
                .Select(c => new ForeignCurrencyContactChoiceDto { ContactId = c.ContactId, ContactName = c.DisplayName }),
        ];

        return ChoicesOf(baseCurrency, legs, accounts, contacts);
    }

    // ---- The builders: LINQ over the read models, so a test runs them over lists ----

    /// <summary>
    /// Each leg on Realized FX Gain/Loss in the period, with the currency and account its
    /// document is tied to (<see cref="ForeignCurrencyGainLossReportRepository.Documents"/>,
    /// which the summary groups the same way). A gain is positive. LINQ only.
    /// </summary>
    public static IQueryable<FxRealizedLeg> RealizedLegDetails(
        IQueryable<JournalLedgerRead> ledger, IQueryable<AccountRead> accounts, string baseCurrency, DateOnly start, DateOnly end)
    {
        // Held in a local: EF reads a captured query as a sub-query, and a method call as nothing it can translate.
        IQueryable<FxDocument> documents = ForeignCurrencyGainLossReportRepository.Documents(ledger, accounts, baseCurrency);

        return from r in ForeignCurrencyGainLossReportRepository.RealizedLegs(ledger, accounts, start, end)
               join d in documents on new { Code = r.TransactionTypeCode, Id = r.TransactionId } equals new { d.Code, d.Id }
               select new FxRealizedLeg
               {
                   LedgerId = r.LedgerId,
                   Date = r.LedgerDate,
                   Code = r.TransactionTypeCode,
                   Id = r.TransactionId,
                   DetailId = r.TransactionDetailId,
                   ContactId = r.ContactId,
                   DocumentNo = r.DocumentNo,
                   Amount = r.CreditAmountBase - r.DebitAmountBase,
                   CurrencyCode = d.Currency,
                   AccountId = d.Control ?? d.Bank ?? 0L,
               };
    }

    /// <summary>The legs on Realized FX Gain/Loss that no currency can claim, as rows with no currency or account. LINQ only.</summary>
    public static IQueryable<FxRealizedLeg> UnassignedRealizedLegDetails(
        IQueryable<JournalLedgerRead> ledger, IQueryable<AccountRead> accounts, string baseCurrency, DateOnly start, DateOnly end) =>
        from r in ForeignCurrencyGainLossReportRepository.UnassignedRealizedLegs(ledger, accounts, baseCurrency, start, end)
        select new FxRealizedLeg
        {
            LedgerId = r.LedgerId,
            Date = r.LedgerDate,
            Code = r.TransactionTypeCode,
            Id = r.TransactionId,
            DetailId = r.TransactionDetailId,
            ContactId = r.ContactId,
            DocumentNo = r.DocumentNo,
            Amount = r.CreditAmountBase - r.DebitAmountBase,
            CurrencyCode = "",
            AccountId = 0L,
        };

    private static IEnumerable<long> IdsOf(
        string code, IEnumerable<ForeignLeg> legs, IEnumerable<FxRealizedLeg> realized, IEnumerable<FxRealizedLeg> unassigned) =>
        legs.Where(l => l.Code == code).Select(l => l.Id)
            .Union(realized.Where(r => r.Code == code).Select(r => r.Id))
            .Union(unassigned.Where(r => r.Code == code).Select(r => r.Id))
            .Distinct();

    /// <summary>The balance of each account and currency from a set of legs: what <see cref="ForeignCurrencyGainLossReportRepository.Balances"/> sums in the database. Pure.</summary>
    public static List<FxBalance> BalancesOf(IEnumerable<ForeignLeg> legs) =>
    [
        .. legs.GroupBy(l => (l.AccountId, l.CurrencyCode))
            .Select(g => new FxBalance
            {
                AccountId = g.Key.AccountId,
                CurrencyCode = g.Key.CurrencyCode,
                DueSource = g.Sum(l => l.Source),
                CarryingBase = g.Sum(l => l.Base),
            }),
    ];

    /// <summary>The realized figure of each currency and account from a set of attributed legs. Pure.</summary>
    public static List<FxRealized> RealizedOf(IEnumerable<FxRealizedLeg> legs) =>
    [
        .. legs.GroupBy(r => (r.CurrencyCode, r.AccountId))
            .Select(g => new FxRealized
            {
                CurrencyCode = g.Key.CurrencyCode,
                AccountId = g.Key.AccountId,
                Amount = g.Sum(r => r.Amount),
                Count = g.Count(),
            }),
    ];

    /// <summary>The choices the filters offer, from the foreign legs up to the date. Pure.</summary>
    public static ForeignCurrencyDetailChoicesDto ChoicesOf(
        string baseCurrency,
        IReadOnlyList<ForeignLeg> legs,
        IReadOnlyList<AccountRead> accounts,
        IReadOnlyList<ForeignCurrencyContactChoiceDto> contacts)
    {
        HashSet<string> withBalance =
            [.. BalancesOf(legs).Where(b => b.DueSource != 0m || b.CarryingBase != 0m).Select(b => b.CurrencyCode)];

        return new ForeignCurrencyDetailChoicesDto
        {
            BaseCurrencyCode = baseCurrency,
            Currencies =
            [
                .. legs.GroupBy(l => l.CurrencyCode)
                    .OrderBy(g => g.Key, StringComparer.Ordinal)
                    .Select(g => new ForeignCurrencyChoiceDto
                    {
                        CurrencyCode = g.Key,
                        Accounts = g.Select(l => l.AccountId).Distinct().Count(),
                        HasBalance = withBalance.Contains(g.Key),
                    }),
            ],
            Accounts =
            [
                .. accounts.OrderBy(a => ForeignCurrencyGainLossReportRepository.KindOf(a)).ThenBy(a => a.AccountName, StringComparer.Ordinal).ThenBy(a => a.AccountId)
                    .Select(a => new ForeignCurrencyAccountChoiceDto
                    {
                        AccountId = a.AccountId,
                        AccountName = a.AccountName,
                        Kind = ForeignCurrencyGainLossReportRepository.KindOf(a),
                    }),
            ],
            Contacts = [.. contacts],
        };
    }

    /// <summary>
    /// The report. The filters are applied here, to the legs and to the realized postings
    /// (a settlement's contact is the one on its balance leg, since the exchange difference
    /// itself carries none); the currency and account figures are then the summary's, from
    /// <see cref="ForeignCurrencyGainLossReportRepository.Compose"/> over the same legs, and
    /// the document rows are attached under their accounts. Pure.
    /// </summary>
    /// <param name="legs">Every foreign-currency balance leg up to the to date.</param>
    /// <param name="realized">The realized legs of the period tied to a currency and account.</param>
    /// <param name="unassigned">The realized legs of the period no currency can claim.</param>
    /// <param name="lines">What each settlement line of a money document settles.</param>
    /// <param name="moneyNumbers">The number of each receive and spend money document, which the ledger cannot carry.</param>
    public static ForeignCurrencyGainLossDetailsResultDto Compose(
        DateOnly start,
        DateOnly end,
        string baseCurrency,
        IReadOnlyList<AccountRead> accounts,
        IReadOnlyList<ForeignLeg> legs,
        IReadOnlyList<FxRealizedLeg> realized,
        IReadOnlyList<FxRealizedLeg> unassigned,
        IReadOnlyList<FxSettlementLine> lines,
        IReadOnlyDictionary<(string Code, long Id), string> moneyNumbers,
        IReadOnlyDictionary<long, string> contactNames,
        IReadOnlyDictionary<string, decimal> rates,
        string? only,
        long? contactId,
        long? accountId)
    {
        // A settlement line's balance leg: the receivable or payable the money relieved.
        Dictionary<(string, long, long), ForeignLeg> controlOf = legs
            .Where(l => l.IsControl)
            .GroupBy(l => (l.Code, l.Id, l.DetailId))
            .ToDictionary(g => g.Key, g => g.OrderBy(l => l.AccountId).First());

        Dictionary<(string, long, long), (string Code, long Id)> settles = lines
            .GroupBy(l => (l.Code, l.Id, l.Line))
            .ToDictionary(g => ((g.Key.Code, g.Key.Id, (long)g.Key.Line)), g => (g.First().MappedCode, g.First().MappedId));

        // The number on a document's face: a money document's own, else the ledger's.
        Dictionary<(string, long), string> ledgerNumbers = legs
            .Where(l => l.DocumentNo != null)
            .GroupBy(l => (l.Code, l.Id))
            .ToDictionary(g => g.Key, g => g.Max(l => l.DocumentNo)!);

        foreach (FxRealizedLeg r in realized.Concat(unassigned).Where(r => r.DocumentNo != null))
        {
            ledgerNumbers.TryAdd((r.Code, r.Id), r.DocumentNo!);
        }

        string? NumberOf(string code, long id) =>
            moneyNumbers.TryGetValue((code, id), out string? money) ? money
            : ledgerNumbers.TryGetValue((code, id), out string? number) ? number
            : null;

        string? NameOf(long? contact) => contact is long id && contactNames.TryGetValue(id, out string? name) ? name : null;

        long? ContactOf(FxRealizedLeg r) =>
            r.ContactId ?? (controlOf.TryGetValue((r.Code, r.Id, r.DetailId), out ForeignLeg? control) ? control.ContactId : null);

        bool Keep(string currency, long account, long? contact) =>
            (only is null || currency == only)
            && (accountId is null || account == accountId)
            && (contactId is null || contact == contactId);

        List<ForeignLeg> keptLegs = [.. legs.Where(l => Keep(l.CurrencyCode, l.AccountId, l.ContactId))];
        List<FxRealizedLeg> keptRealized = [.. realized.Where(r => Keep(r.CurrencyCode, r.AccountId, ContactOf(r)))];

        // What no currency can claim has no currency, account or contact to be filtered by, so any filter leaves it out.
        List<FxRealizedLeg> keptUnassigned = only is null && accountId is null && contactId is null ? [.. unassigned] : [];

        // ---- The totals: the summary's, from the same legs ----
        HashSet<(string Currency, long AccountId)> rowsUnder = [];

        Dictionary<(string Currency, long AccountId), List<ForeignCurrencyDocumentDto>> documents = [];

        void Add(string currency, long account, ForeignCurrencyDocumentDto row)
        {
            rowsUnder.Add((currency, account));

            if (!documents.TryGetValue((currency, account), out List<ForeignCurrencyDocumentDto>? list))
            {
                documents[(currency, account)] = list = [];
            }

            list.Add(row);
        }

        // ---- Open documents ----
        (string Code, long Id) SubjectOf(ForeignLeg l) =>
            l.IsControl && settles.TryGetValue((l.Code, l.Id, l.DetailId), out (string Code, long Id) subject) ? subject : (l.Code, l.Id);

        foreach (IGrouping<(long AccountId, string Currency, string Code, long Id), ForeignLeg> group in keptLegs
                     .GroupBy(l => { (string code, long id) = SubjectOf(l); return (l.AccountId, l.CurrencyCode, code, id); }))
        {
            decimal due = group.Sum(l => l.Source);
            decimal carrying = group.Sum(l => l.Base);

            if (due == 0m && carrying == 0m)
            {
                continue;
            }

            // The document's own legs, when it has some here: they give its date, its number and its contact.
            List<ForeignLeg> own = [.. group.Where(l => l.Code == group.Key.Code && l.Id == group.Key.Id)];
            List<ForeignLeg> dated = own.Count > 0 ? own : [.. group];

            decimal? rate = rates.TryGetValue(group.Key.Currency, out decimal given) ? given : null;
            decimal? current = rate is decimal r ? Math.Round(due * r, 2, MidpointRounding.AwayFromZero) : null;
            long? contact = dated.Select(l => l.ContactId).FirstOrDefault(c => c != null) ?? group.Select(l => l.ContactId).FirstOrDefault(c => c != null);

            Add(group.Key.Currency, group.Key.AccountId, new ForeignCurrencyDocumentDto
            {
                Kind = ForeignDocumentKind.Open,
                Date = dated.Min(l => l.LedgerDate),
                TypeCode = group.Key.Code,
                DocumentNo = NumberOf(group.Key.Code, group.Key.Id) ?? $"{group.Key.Code}-{group.Key.Id}",
                ContactId = contact,
                ContactName = NameOf(contact),
                Amount = due,
                BookedRate = due == 0m ? null : carrying / due,
                CurrentRate = rate,
                CarryingBase = carrying,
                CurrentBase = current,
                Unrealized = current - carrying,
            });
        }

        // ---- Settlements ----
        foreach (FxRealizedLeg r in keptRealized)
        {
            controlOf.TryGetValue((r.Code, r.Id, r.DetailId), out ForeignLeg? control);

            string? settled = settles.TryGetValue((r.Code, r.Id, r.DetailId), out (string Code, long Id) mapped)
                ? $"Settles {NumberOf(mapped.Code, mapped.Id) ?? $"{mapped.Code}-{mapped.Id}"}"
                : null;

            decimal? amount = control is null ? null : Math.Abs(control.Source);
            decimal? booked = control is null ? null : Math.Abs(control.Base);

            // The money moved at the booked amount plus a gain on a receipt (the balance was credited)
            // and less one on a payment (it was debited): the difference is what was posted.
            decimal? moved = control is null ? null : booked + (control.Source < 0m ? r.Amount : -r.Amount);

            Add(r.CurrencyCode, r.AccountId, new ForeignCurrencyDocumentDto
            {
                Kind = ForeignDocumentKind.Settlement,
                Date = r.Date,
                TypeCode = r.Code,
                DocumentNo = NumberOf(r.Code, r.Id) ?? r.DocumentNo ?? $"{r.Code}-{r.Id}",
                ContactId = ContactOf(r),
                ContactName = NameOf(ContactOf(r)),
                Reference = settled,
                Amount = amount,
                BookedRate = amount is > 0m ? booked / amount : null,
                CurrentRate = amount is > 0m ? moved / amount : null,
                CarryingBase = booked,
                CurrentBase = moved,
                Realized = r.Amount,
            });
        }

        // ---- The summary over the same legs, then the rows under its accounts ----
        decimal realizedTotal = keptRealized.Sum(r => r.Amount) + keptUnassigned.Sum(r => r.Amount);

        ForeignCurrencyGainLossReportResultDto summary = ForeignCurrencyGainLossReportRepository.Compose(
            start, end, baseCurrency, accounts, BalancesOf(keptLegs), RealizedOf(keptRealized), realizedTotal, rates, null, rowsUnder);

        List<ForeignCurrencyDocumentDto> unclaimed =
        [
            .. keptUnassigned.OrderBy(r => r.Date).ThenBy(r => r.LedgerId).Select(r => new ForeignCurrencyDocumentDto
            {
                Kind = ForeignDocumentKind.Settlement,
                Date = r.Date,
                TypeCode = r.Code,
                DocumentNo = NumberOf(r.Code, r.Id) ?? r.DocumentNo ?? $"{r.Code}-{r.Id}",
                ContactId = r.ContactId,
                ContactName = NameOf(r.ContactId),
                Realized = r.Amount,
            }),
        ];

        return new ForeignCurrencyGainLossDetailsResultDto
        {
            FromDate = start,
            ToDate = end,
            BaseCurrencyCode = baseCurrency,
            Currencies =
            [
                .. summary.Currencies.Select(block => new ForeignCurrencyDetailBlockDto
                {
                    CurrencyCode = block.CurrencyCode,
                    Rate = block.Rate,
                    NeedsRate = block.NeedsRate,
                    DueSource = block.DueSource,
                    CarryingBase = block.CarryingBase,
                    RevaluedBase = block.RevaluedBase,
                    Unrealized = block.Unrealized,
                    Realized = block.Realized,
                    Net = block.Net,
                    Accounts =
                    [
                        .. block.Accounts.Select(a => new ForeignCurrencyDetailAccountDto
                        {
                            AccountId = a.AccountId,
                            AccountName = a.AccountName,
                            Kind = a.Kind,
                            DueSource = a.DueSource,
                            CarryingBase = a.CarryingBase,
                            RevaluedBase = a.RevaluedBase,
                            Unrealized = a.Unrealized,
                            Realized = a.Realized,
                            Documents =
                            [
                                .. (documents.TryGetValue((block.CurrencyCode, a.AccountId), out List<ForeignCurrencyDocumentDto>? rows) ? rows : [])
                                    .OrderBy(d => d.Date).ThenBy(d => d.Kind).ThenBy(d => d.DocumentNo, StringComparer.Ordinal),
                            ],
                        }),
                    ],
                }),
            ],
            Unassigned = unclaimed,
            UnassignedRealized = summary.UnassignedRealized,
            TotalRealized = summary.TotalRealized,
            TotalUnrealized = summary.TotalUnrealized,
            TotalNet = summary.TotalNet,
            CurrenciesWithoutRate = summary.CurrenciesWithoutRate,
        };
    }
}

/// <summary>One leg on Realized FX Gain/Loss, with the currency and account its document is tied to.</summary>
public sealed class FxRealizedLeg
{
    public long LedgerId { get; set; }

    public DateOnly Date { get; set; }

    public string Code { get; set; } = string.Empty;

    public long Id { get; set; }

    /// <summary>The line of the settling document the difference belongs to.</summary>
    public long DetailId { get; set; }

    public long? ContactId { get; set; }

    public string? DocumentNo { get; set; }

    /// <summary>Credits less debits: positive is a gain.</summary>
    public decimal Amount { get; set; }

    /// <summary>Empty when no currency can claim the leg.</summary>
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>Zero when no account can claim the leg.</summary>
    public long AccountId { get; set; }
}

/// <summary>What one line of a receive or spend money document settles.</summary>
public sealed class FxSettlementLine
{
    public string Code { get; set; } = string.Empty;

    public long Id { get; set; }

    public int Line { get; set; }

    public string MappedCode { get; set; } = string.Empty;

    public long MappedId { get; set; }
}
