using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Foreign Currency Gain or Loss report (TK-220): what exchange rates have done
/// to the branch's foreign-currency balances. <b>Realized</b> is what settlements
/// posted to Realized FX Gain/Loss between two dates; <b>unrealized</b> is what the
/// balances still open would gain or lose at a rate the user gives. Nothing is
/// posted for it.
///
/// <b>Which balances.</b> The receivable and payable control accounts, in every
/// currency but the branch's own, and a bank or cash account kept in a foreign
/// currency, in that currency. A leg is counted by the amount the document gave in
/// its own currency and by the base amount the ledger stored beside it, so the
/// figures do not depend on which way a rate is written: the carrying value of a
/// balance is its stored base amounts added up, at the rates the documents were
/// booked at, and a balance is never repriced from a rate looked up now.
///
/// <b>Unrealized</b> is the balance times the revaluation rate, less its carrying
/// value, whichever side of the books it is on, so a rate that rises is a gain on
/// a receivable and a loss on a payable. The rate is in base currency per unit of
/// the foreign one, the way the rate master states it. It is the difference from the
/// original rates, whether or not a revaluation journal has been posted: a journal in
/// the base currency is not a foreign-currency leg and is not counted.
///
/// <b>Realized</b> is every leg on the Realized FX Gain/Loss account in the period,
/// credits less debits, so its total is that account's movement on the Profit and
/// Loss for the same dates. Accounting posts the difference in base currency, in the
/// document that settled the balance, so a leg is tied to a currency and an account
/// through the foreign-currency legs of its own document: the receivable or payable
/// first, else the bank account. A document with none (a manual journal) belongs to
/// no currency and is shown apart, so the total still agrees.
/// </summary>
public sealed class ForeignCurrencyGainLossReportRepository
{
    /// <summary>
    /// The account names Accounting gives its control accounts
    /// (<c>SystemAccountNames.Of(SystemAccount…)</c>), written as literals because
    /// this project does not reference Accounting's enums.
    /// </summary>
    public const string RealizedFxAccountName = "Realized FX Gain/Loss";

    public const string ReceivableAccountName = "Accounts Receivable";

    public const string PayableAccountName = "Accounts Payable";

    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public ForeignCurrencyGainLossReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>
    /// What a request needs beyond both dates being given, each with the field it
    /// belongs to: the as-at date not before the from date, a currency that is a
    /// three-letter code, and a rate for each currency that is above nought. Pure.
    /// </summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(ForeignCurrencyGainLossReportRequest request)
    {
        List<(string Field, string Message)> refusals = [];

        if (request.FromDate is DateOnly from && request.AsAtDate is DateOnly asAt && asAt < from)
        {
            refusals.Add((nameof(ForeignCurrencyGainLossReportRequest.AsAtDate), "As at date cannot be before the from date."));
        }

        if (!string.IsNullOrWhiteSpace(request.CurrencyCode) && !IsCurrencyCode(request.CurrencyCode.Trim()))
        {
            refusals.Add((nameof(ForeignCurrencyGainLossReportRequest.CurrencyCode), "Currency must be a 3-letter code."));
        }

        foreach ((string code, decimal rate) in request.Rates ?? [])
        {
            string field = $"{nameof(ForeignCurrencyGainLossReportRequest.Rates)}[{code}]";

            if (!IsCurrencyCode(code.Trim()))
            {
                refusals.Add((field, "Currency must be a 3-letter code."));
            }
            else if (rate <= 0m)
            {
                refusals.Add((field, "Rate must be greater than zero."));
            }
        }

        return refusals;
    }

    private static bool IsCurrencyCode(string code) => code.Length == 3 && code.All(char.IsAsciiLetter);

    /// <summary>The rates a request names, by upper-case currency code. Pure.</summary>
    public static Dictionary<string, decimal> RatesOf(ForeignCurrencyGainLossReportRequest request)
    {
        // Written out rather than ToDictionary: "usd" and "USD" are one currency, and the later one wins.
        Dictionary<string, decimal> rates = new(StringComparer.Ordinal);

        foreach ((string code, decimal rate) in request.Rates ?? [])
        {
            rates[code.Trim().ToUpperInvariant()] = rate;
        }

        return rates;
    }

    /// <summary>The report for a request the controller has already validated.</summary>
    public async Task<ForeignCurrencyGainLossReportResultDto> RunAsync(
        ForeignCurrencyGainLossReportRequest request, CancellationToken ct)
    {
        DateOnly from = request.FromDate!.Value;
        DateOnly asAt = request.AsAtDate!.Value;
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

        // Without a base currency nothing is foreign: there is nothing to answer, and no guess to make.
        if (baseCurrency.Length == 0)
        {
            return Compose(from, asAt, baseCurrency, [], [], [], 0m, new Dictionary<string, decimal>(), null);
        }

        List<FxBalance> balances = await Balances(_db.Ledger, _db.Accounts, baseCurrency, asAt).ToListAsync(ct);
        List<FxRealized> realized = await Realized(_db.Ledger, _db.Accounts, baseCurrency, from, asAt).ToListAsync(ct);
        decimal realizedTotal = await RealizedLegs(_db.Ledger, _db.Accounts, from, asAt)
            .SumAsync(l => l.CreditAmountBase - l.DebitAmountBase, ct);

        List<long> ids = [.. balances.Select(b => b.AccountId).Union(realized.Select(r => r.AccountId)).Distinct()];
        List<AccountRead> accounts = await _db.Accounts.Where(a => ids.Contains(a.AccountId)).ToListAsync(ct);

        string? only = string.IsNullOrWhiteSpace(request.CurrencyCode) ? null : request.CurrencyCode.Trim().ToUpperInvariant();

        return Compose(from, asAt, baseCurrency, accounts, balances, realized, realizedTotal, RatesOf(request), only);
    }

    /// <summary>The foreign currencies the branch holds a balance in at a date, for the rates the page offers.</summary>
    public async Task<ForeignCurrencyChoicesDto> ChoicesAsync(DateOnly asAt, CancellationToken ct)
    {
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

        if (baseCurrency.Length == 0)
        {
            return ChoicesOf(baseCurrency, []);
        }

        return ChoicesOf(baseCurrency, await Balances(_db.Ledger, _db.Accounts, baseCurrency, asAt).ToListAsync(ct));
    }

    // ---- The builders: LINQ over the read models, so a test runs them over lists ----

    /// <summary>
    /// Whether a leg is a foreign-currency balance: in a receivable or payable
    /// account and in a currency other than the branch's, or in a bank or cash
    /// account and in that account's own foreign currency. A bank account in the base
    /// currency is not one, whatever a document in a foreign currency tagged its leg
    /// with.
    /// </summary>
    public static IQueryable<ForeignLeg> ForeignLegs(
        IQueryable<JournalLedgerRead> ledger, IQueryable<AccountRead> accounts, string baseCurrency) =>
        from l in ledger
        join a in accounts on l.AccountId equals a.AccountId
        where l.CurrencyCode != baseCurrency
              && (a.AccountSystemName == ReceivableAccountName
                  || a.AccountSystemName == PayableAccountName
                  || (a.IsBank && l.CurrencyCode == a.CurrencyCode))
        select new ForeignLeg
        {
            LedgerId = l.LedgerId,
            Code = l.TransactionTypeCode,
            Id = l.TransactionId,
            DetailId = l.TransactionDetailId,
            ContactId = l.ContactId,
            DocumentNo = l.DocumentNo,
            LedgerDate = l.LedgerDate,
            CurrencyCode = l.CurrencyCode,
            AccountId = l.AccountId,
            IsControl = a.AccountSystemName == ReceivableAccountName || a.AccountSystemName == PayableAccountName,
            Source = l.DebitAmount - l.CreditAmount,
            Base = l.DebitAmountBase - l.CreditAmountBase,
        };

    /// <summary>
    /// What each account holds in each foreign currency at the end of a day: the
    /// amount in the currency, and the same legs in base currency as the ledger
    /// stored them. One grouped pass. LINQ only.
    /// </summary>
    public static IQueryable<FxBalance> Balances(
        IQueryable<JournalLedgerRead> ledger, IQueryable<AccountRead> accounts, string baseCurrency, DateOnly asAt) =>
        from l in ForeignLegs(ledger, accounts, baseCurrency)
        where l.LedgerDate <= asAt
        group l by new { l.AccountId, l.CurrencyCode } into g
        select new FxBalance
        {
            AccountId = g.Key.AccountId,
            CurrencyCode = g.Key.CurrencyCode,
            DueSource = g.Sum(l => l.Source),
            CarryingBase = g.Sum(l => l.Base),
        };

    /// <summary>The legs on the Realized FX Gain/Loss account between two dates, both counted. LINQ only.</summary>
    public static IQueryable<JournalLedgerRead> RealizedLegs(
        IQueryable<JournalLedgerRead> ledger, IQueryable<AccountRead> accounts, DateOnly start, DateOnly asAt) =>
        from l in ledger
        join a in accounts on l.AccountId equals a.AccountId
        where a.AccountSystemName == RealizedFxAccountName && l.LedgerDate >= start && l.LedgerDate <= asAt
        select l;

    /// <summary>
    /// Each document that has a foreign-currency balance leg, once: its currency (the
    /// first, when it has several) and the account the document is tied to, the
    /// receivable or payable when it has one, else its bank account. LINQ only.
    /// </summary>
    public static IQueryable<FxDocument> Documents(
        IQueryable<JournalLedgerRead> ledger, IQueryable<AccountRead> accounts, string baseCurrency) =>
        from f in ForeignLegs(ledger, accounts, baseCurrency)
        group f by new { f.Code, f.Id } into g
        select new FxDocument
        {
            Code = g.Key.Code,
            Id = g.Key.Id,
            Currency = g.Min(f => f.CurrencyCode)!,
            Control = g.Min(f => f.IsControl ? (long?)f.AccountId : null),
            Bank = g.Min(f => f.IsControl ? null : (long?)f.AccountId),
        };

    /// <summary>
    /// The realized gain or loss of the period for each currency and account: each
    /// leg on Realized FX Gain/Loss joined to its document in <see cref="Documents"/>,
    /// so a document with several foreign legs is not counted for each. A gain is
    /// positive. LINQ only.
    /// </summary>
    public static IQueryable<FxRealized> Realized(
        IQueryable<JournalLedgerRead> ledger, IQueryable<AccountRead> accounts, string baseCurrency, DateOnly start, DateOnly asAt) =>
        from r in RealizedLegs(ledger, accounts, start, asAt)
        join d in Documents(ledger, accounts, baseCurrency) on new { Code = r.TransactionTypeCode, Id = r.TransactionId } equals new { d.Code, d.Id }
        group r by new { d.Currency, AccountId = d.Control ?? d.Bank } into g
        select new FxRealized
        {
            CurrencyCode = g.Key.Currency,
            AccountId = g.Key.AccountId ?? 0L,
            Amount = g.Sum(r => r.CreditAmountBase - r.DebitAmountBase),
            Count = g.Count(),
        };

    /// <summary>
    /// The legs on Realized FX Gain/Loss whose document has no foreign-currency balance
    /// leg, such as a manual journal: no currency can claim them. LINQ only.
    /// </summary>
    public static IQueryable<JournalLedgerRead> UnassignedRealizedLegs(
        IQueryable<JournalLedgerRead> ledger, IQueryable<AccountRead> accounts, string baseCurrency, DateOnly start, DateOnly asAt)
    {
        // Held in a local: EF reads a captured query as a sub-query, and a method call as nothing it can translate.
        IQueryable<FxDocument> documents = Documents(ledger, accounts, baseCurrency);

        return RealizedLegs(ledger, accounts, start, asAt)
            .Where(r => !documents.Any(d => d.Code == r.TransactionTypeCode && d.Id == r.TransactionId));
    }

    /// <summary>
    /// The report: a block for each currency, an account row in it for each balance or
    /// realized figure, and the totals. A balance counts when it is not nought in the
    /// currency or in base; the rows of a currency run receivables, payables, then
    /// banks, by name. A currency with no rate has no revalued or unrealized figure,
    /// and its net is its realized part. Pure.
    /// </summary>
    /// <param name="realizedTotal">Every posting to Realized FX Gain/Loss in the period, credits less debits.</param>
    /// <param name="rates">Base currency per unit, by upper-case currency code.</param>
    /// <param name="only">One currency asked for, or null for all.</param>
    /// <param name="alsoShow">
    /// Currency and account pairs to list even when they hold nothing and posted nothing,
    /// because the details report has document rows under them: an open invoice and an
    /// unapplied advance can net to nought on the account and are still two documents.
    /// </param>
    public static ForeignCurrencyGainLossReportResultDto Compose(
        DateOnly from,
        DateOnly asAt,
        string baseCurrency,
        IReadOnlyList<AccountRead> accounts,
        IReadOnlyList<FxBalance> balances,
        IReadOnlyList<FxRealized> realized,
        decimal realizedTotal,
        IReadOnlyDictionary<string, decimal> rates,
        string? only,
        IReadOnlyCollection<(string Currency, long AccountId)>? alsoShow = null)
    {
        Dictionary<long, AccountRead> accountById = accounts.ToDictionary(a => a.AccountId);
        Dictionary<(string Currency, long AccountId), (decimal Due, decimal Carrying, decimal Realized)> figures = [];

        foreach (FxBalance b in balances.Where(b => only is null || b.CurrencyCode == only))
        {
            figures[(b.CurrencyCode, b.AccountId)] = (b.DueSource, b.CarryingBase, 0m);
        }

        List<FxRealized> attributed = [.. realized.Where(r => r.AccountId != 0L && r.CurrencyCode.Length > 0)];

        foreach (FxRealized r in attributed.Where(r => only is null || r.CurrencyCode == only))
        {
            (decimal due, decimal carrying, decimal already) = figures.GetValueOrDefault((r.CurrencyCode, r.AccountId));
            figures[(r.CurrencyCode, r.AccountId)] = (due, carrying, already + r.Amount);
        }

        foreach ((string currency, long accountId) in alsoShow ?? [])
        {
            if (!figures.ContainsKey((currency, accountId)))
            {
                figures[(currency, accountId)] = (0m, 0m, 0m);
            }
        }

        HashSet<(string Currency, long AccountId)> shown = [.. alsoShow ?? []];
        List<ForeignCurrencyBlockDto> blocks = [];

        foreach (IGrouping<string, KeyValuePair<(string Currency, long AccountId), (decimal Due, decimal Carrying, decimal Realized)>> group in
                 figures.Where(f => f.Value.Due != 0m || f.Value.Carrying != 0m || f.Value.Realized != 0m || shown.Contains(f.Key))
                     .GroupBy(f => f.Key.Currency)
                     .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            decimal? rate = rates.TryGetValue(group.Key, out decimal given) ? given : null;
            List<ForeignCurrencyAccountDto> rows = [];

            foreach (KeyValuePair<(string Currency, long AccountId), (decimal Due, decimal Carrying, decimal Realized)> f in group)
            {
                accountById.TryGetValue(f.Key.AccountId, out AccountRead? account);
                decimal? revalued = rate is decimal r ? Math.Round(f.Value.Due * r, 2, MidpointRounding.AwayFromZero) : null;

                rows.Add(new ForeignCurrencyAccountDto
                {
                    AccountId = f.Key.AccountId,
                    AccountName = account?.AccountName ?? $"Account {f.Key.AccountId}",
                    Kind = KindOf(account),
                    DueSource = f.Value.Due,
                    CarryingBase = f.Value.Carrying,
                    RevaluedBase = revalued,
                    Unrealized = revalued - f.Value.Carrying,
                    Realized = f.Value.Realized,
                });
            }

            rows = [.. rows.OrderBy(r => r.Kind).ThenBy(r => r.AccountName, StringComparer.Ordinal).ThenBy(r => r.AccountId)];

            bool hasBalance = rows.Any(r => r.DueSource != 0m || r.CarryingBase != 0m);
            decimal realizedOfCurrency = rows.Sum(r => r.Realized);
            decimal? unrealized = rate is null ? null : rows.Sum(r => r.Unrealized);

            blocks.Add(new ForeignCurrencyBlockDto
            {
                CurrencyCode = group.Key,
                Rate = rate,
                NeedsRate = rate is null && hasBalance,
                Accounts = rows,
                DueSource = rows.Sum(r => r.DueSource),
                CarryingBase = rows.Sum(r => r.CarryingBase),
                RevaluedBase = rate is null ? null : rows.Sum(r => r.RevaluedBase),
                Unrealized = unrealized,
                Realized = realizedOfCurrency,
                Net = realizedOfCurrency + (unrealized ?? 0m),
            });
        }

        // What no currency can claim is the account's movement less what the currencies were given.
        decimal unassigned = only is null ? realizedTotal - attributed.Sum(r => r.Amount) : 0m;
        decimal totalRealized = only is null ? realizedTotal : blocks.Sum(b => b.Realized);
        decimal totalUnrealized = blocks.Sum(b => b.Unrealized ?? 0m);

        return new ForeignCurrencyGainLossReportResultDto
        {
            FromDate = from,
            AsAtDate = asAt,
            BaseCurrencyCode = baseCurrency,
            Currencies = blocks,
            UnassignedRealized = unassigned,
            TotalRealized = totalRealized,
            TotalUnrealized = totalUnrealized,
            TotalNet = totalRealized + totalUnrealized,
            CurrenciesWithoutRate = [.. blocks.Where(b => b.NeedsRate).Select(b => b.CurrencyCode)],
        };
    }

    /// <summary>The foreign currencies with a balance, by code, each with how many accounts hold one. Pure.</summary>
    public static ForeignCurrencyChoicesDto ChoicesOf(string baseCurrency, IReadOnlyList<FxBalance> balances) =>
        new()
        {
            BaseCurrencyCode = baseCurrency,
            Currencies =
            [
                .. balances
                    .Where(b => b.DueSource != 0m || b.CarryingBase != 0m)
                    .GroupBy(b => b.CurrencyCode)
                    .OrderBy(g => g.Key, StringComparer.Ordinal)
                    .Select(g => new ForeignCurrencyChoiceDto { CurrencyCode = g.Key, Accounts = g.Count(), HasBalance = true }),
            ],
        };

    /// <summary>What kind of account an account is, from its system name; a bank account otherwise.</summary>
    public static ForeignAccountKind KindOf(AccountRead? account) => account?.AccountSystemName switch
    {
        ReceivableAccountName => ForeignAccountKind.Receivable,
        PayableAccountName => ForeignAccountKind.Payable,
        _ => ForeignAccountKind.Bank,
    };
}

/// <summary>One leg that is a foreign-currency balance, with what the grouping needs of its account.</summary>
public sealed class ForeignLeg
{
    public long LedgerId { get; set; }

    public string Code { get; set; } = string.Empty;

    public long Id { get; set; }

    /// <summary>The line of the document the leg belongs to; a money document's line number.</summary>
    public long DetailId { get; set; }

    public long? ContactId { get; set; }

    /// <summary>The number on the document's face, when the ledger row carries one.</summary>
    public string? DocumentNo { get; set; }

    public DateOnly LedgerDate { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public long AccountId { get; set; }

    /// <summary>The leg is on a receivable or payable control account.</summary>
    public bool IsControl { get; set; }

    /// <summary>Debits less credits in the document's currency.</summary>
    public decimal Source { get; set; }

    /// <summary>Debits less credits in base currency, as stored.</summary>
    public decimal Base { get; set; }
}

/// <summary>A document with a foreign-currency balance leg, with the currency and account it is tied to.</summary>
public sealed class FxDocument
{
    public string Code { get; set; } = string.Empty;

    public long Id { get; set; }

    public string Currency { get; set; } = string.Empty;

    /// <summary>The lowest receivable or payable account the document has a foreign leg on, if any.</summary>
    public long? Control { get; set; }

    /// <summary>The lowest foreign bank account it has a leg on, if any.</summary>
    public long? Bank { get; set; }
}

/// <summary>An account's balance in one foreign currency.</summary>
public sealed class FxBalance
{
    public long AccountId { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>Debits less credits in the currency.</summary>
    public decimal DueSource { get; set; }

    /// <summary>The same legs in base currency at the rates the documents were booked at.</summary>
    public decimal CarryingBase { get; set; }
}

/// <summary>What the period posted to Realized FX Gain/Loss for one currency and account.</summary>
public sealed class FxRealized
{
    public string CurrencyCode { get; set; } = string.Empty;

    public long AccountId { get; set; }

    /// <summary>Credits less debits: positive is a gain.</summary>
    public decimal Amount { get; set; }

    public int Count { get; set; }
}
