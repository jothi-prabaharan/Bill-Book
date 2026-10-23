using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Bank Summary (TK-219): for each bank and cash account, its balance when the
/// period began, what was received and spent in it, the revaluation of a foreign
/// currency account, and its balance at the end.
///
/// <b>Everything is the account's own ledger account.</b> Each bank account
/// provisions an account in the chart (<c>LedgerAccountId</c>), so the opening
/// balance is that account's ledger balance before the period and the closing
/// balance is its ledger balance at the end, which is the figure the
/// Reconciliation Report calls the balance in the books. Received is the period's
/// debits and spent its credits, so a transfer between two bank accounts is spent
/// from one and received by the other. Credit cards and overdrafts are liabilities
/// in the chart, but here, as in the Reconciliation Report, a balance is money held,
/// so what is owed reads negative.
///
/// <b>A revaluation is a posting that moves a foreign account's value in base
/// currency without moving money.</b> It is told by its document: one that also
/// posts to the Unrealized FX Gain/Loss account. Its postings are the Revaluation
/// column and are left out of Received and Spent, so
/// <c>opening + received − spent + revaluation = closing</c> holds without the
/// revaluation being counted as money in or out. Nothing in the product posts one
/// yet; a manual journal against the account and Unrealized FX Gain/Loss is one.
///
/// <b>A foreign account's own-currency figures</b> count an amount in the account's
/// currency as it is and an amount in any other at its base amount, the rule the
/// Reconciliation Report uses, and leave a revaluation out altogether, because it
/// changes the base value and not the balance in the account's currency.
/// </summary>
public sealed class BankSummaryReportRepository
{
    /// <summary>
    /// The account name Accounting gives the Unrealized FX Gain/Loss control account
    /// (<c>SystemAccountNames.Of(SystemAccount.UnrealizedFxGainLoss)</c>), written as
    /// a literal because this project does not reference Accounting's enums.
    /// </summary>
    public const string UnrealizedFxAccountName = "Unrealized FX Gain/Loss";

    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public BankSummaryReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>The refusal a period needs beyond both dates being given, with the field it belongs to. Pure.</summary>
    public static IReadOnlyList<(string Field, string Message)> DateRefusals(BankSummaryReportRequest request) =>
        request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from
            ? [(nameof(BankSummaryReportRequest.ToDate), "To date cannot be before the from date.")]
            : [];

    /// <summary>Whether a bank account is on this branch; the query filter decides, so another branch's reads as none.</summary>
    public Task<bool> BankAccountExistsAsync(long bankAccountId, CancellationToken ct) =>
        _db.BankAccounts.AnyAsync(b => b.BankAccountId == bankAccountId, ct);

    /// <summary>The summary for a request the controller has already validated.</summary>
    public async Task<BankSummaryReportResultDto> RunAsync(BankSummaryReportRequest request, CancellationToken ct)
    {
        DateOnly start = request.FromDate!.Value;
        DateOnly end = request.ToDate!.Value;
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

        IQueryable<BankAccountRead> banks = _db.BankAccounts;
        if (request.BankAccountId is long chosen)
        {
            banks = banks.Where(b => b.BankAccountId == chosen);
        }

        List<BankAccountRead> accounts = await banks.ToListAsync(ct);
        List<BankSum> all = await Sums(_db.Ledger, banks, start, end).ToListAsync(ct);
        List<BankSum> revaluation = await Sums(RevaluationLegs(_db.Ledger, _db.Accounts), banks, start, end).ToListAsync(ct);

        return Compose(start, end, baseCurrency, accounts, all, revaluation, request.BankAccountId);
    }

    // ---- The builders: LINQ over the read models, so a test runs them over lists ----

    /// <summary>
    /// The ledger legs of the documents that also post to the Unrealized FX
    /// Gain/Loss account. The documents are collected once, each by its type and id,
    /// so a document with two such legs does not repeat its others. LINQ only.
    /// </summary>
    public static IQueryable<JournalLedgerRead> RevaluationLegs(
        IQueryable<JournalLedgerRead> ledger, IQueryable<AccountRead> accounts)
    {
        var documents = (from o in ledger
                         join a in accounts on o.AccountId equals a.AccountId
                         where a.AccountSystemName == UnrealizedFxAccountName
                         select new { Code = o.TransactionTypeCode, Id = o.TransactionId })
            .Distinct();

        return from l in ledger
               join d in documents on new { Code = l.TransactionTypeCode, Id = l.TransactionId } equals new { d.Code, d.Id }
               select l;
    }

    /// <summary>
    /// What each bank account's ledger account holds in a set of legs, summed once up
    /// to the end of the period: before the period, and its debits, credits and count,
    /// in base currency and in the account's own. One grouped pass; the typed
    /// <c>0m</c> is what lets Postgres sum a conditional. LINQ only.
    /// </summary>
    public static IQueryable<BankSum> Sums(
        IQueryable<JournalLedgerRead> legs, IQueryable<BankAccountRead> banks, DateOnly start, DateOnly end) =>
        from l in legs
        where l.LedgerDate <= end
        join b in banks on (long?)l.AccountId equals b.LedgerAccountId
        group l by new { b.BankAccountId, b.CurrencyCode } into g
        select new BankSum
        {
            BankAccountId = g.Key.BankAccountId,
            OpeningBase = g.Sum(l => l.LedgerDate < start ? l.DebitAmountBase - l.CreditAmountBase : 0m),
            DebitBase = g.Sum(l => l.LedgerDate >= start ? l.DebitAmountBase : 0m),
            CreditBase = g.Sum(l => l.LedgerDate >= start ? l.CreditAmountBase : 0m),
            OpeningOwn = g.Sum(l => l.LedgerDate < start
                ? (l.CurrencyCode == g.Key.CurrencyCode ? l.DebitAmount - l.CreditAmount : l.DebitAmountBase - l.CreditAmountBase)
                : 0m),
            DebitOwn = g.Sum(l => l.LedgerDate >= start
                ? (l.CurrencyCode == g.Key.CurrencyCode ? l.DebitAmount : l.DebitAmountBase)
                : 0m),
            CreditOwn = g.Sum(l => l.LedgerDate >= start
                ? (l.CurrencyCode == g.Key.CurrencyCode ? l.CreditAmount : l.CreditAmountBase)
                : 0m),
            Count = g.Count(l => l.LedgerDate >= start),
        };

    /// <summary>
    /// The base-currency figures of the summary for every bank account with a leg,
    /// as one query for the catalog source: the ledger summed once, then each
    /// revaluation document's legs taken out of the debits and credits and put in the
    /// revaluation. Two grouped queries are put end to end and summed again, rather
    /// than one being looked up from the other for each account. LINQ only.
    /// </summary>
    public static IQueryable<BankFigures> Figures(
        IQueryable<JournalLedgerRead> ledger,
        IQueryable<AccountRead> accounts,
        IQueryable<BankAccountRead> banks,
        DateOnly start,
        DateOnly end)
    {
        IQueryable<BankFigures> everything = Sums(ledger, banks, start, end).Select(s => new BankFigures
        {
            BankAccountId = s.BankAccountId,
            OpeningBase = s.OpeningBase,
            ReceivedBase = s.DebitBase,
            SpentBase = s.CreditBase,
            RevaluationBase = 0m,
            Count = s.Count,
        });

        IQueryable<BankFigures> revaluations = Sums(RevaluationLegs(ledger, accounts), banks, start, end).Select(s => new BankFigures
        {
            BankAccountId = s.BankAccountId,
            OpeningBase = 0m,
            ReceivedBase = -s.DebitBase,
            SpentBase = -s.CreditBase,
            RevaluationBase = s.DebitBase - s.CreditBase,
            Count = 0,
        });

        return everything
            .Concat(revaluations)
            .GroupBy(f => f.BankAccountId)
            .Select(g => new BankFigures
            {
                BankAccountId = g.Key,
                OpeningBase = g.Sum(f => f.OpeningBase),
                ReceivedBase = g.Sum(f => f.ReceivedBase),
                SpentBase = g.Sum(f => f.SpentBase),
                RevaluationBase = g.Sum(f => f.RevaluationBase),
                Count = g.Sum(f => f.Count),
            });
    }

    /// <summary>
    /// The accounts of the summary. A bank account is listed when it is switched on,
    /// holds a balance, has a posting in the period, or was asked for by name; one
    /// switched off with nothing to show is left out. Active accounts come first,
    /// then by name. Pure.
    ///
    /// Received and spent leave the revaluation's postings out, and the revaluation
    /// is their net, so closing = opening + received − spent + revaluation is the
    /// ledger balance at the end. An account in the base currency has no own-currency
    /// figures; one in another has them without the revaluation. Totals add the
    /// base-currency figures.
    /// </summary>
    public static BankSummaryReportResultDto Compose(
        DateOnly start,
        DateOnly end,
        string baseCurrency,
        IReadOnlyList<BankAccountRead> banks,
        IReadOnlyList<BankSum> all,
        IReadOnlyList<BankSum> revaluation,
        long? chosen)
    {
        Dictionary<long, BankSum> allById = all.ToDictionary(s => s.BankAccountId);
        Dictionary<long, BankSum> revaluationById = revaluation.ToDictionary(s => s.BankAccountId);
        List<BankSummaryAccountDto> accounts = [];

        foreach (BankAccountRead bank in banks.OrderByDescending(b => b.IsActive).ThenBy(b => b.AccountName, StringComparer.Ordinal).ThenBy(b => b.BankAccountId))
        {
            BankSum sum = allById.GetValueOrDefault(bank.BankAccountId) ?? new BankSum();
            BankSum rev = revaluationById.GetValueOrDefault(bank.BankAccountId) ?? new BankSum();

            if (!bank.IsActive && bank.BankAccountId != chosen && sum.OpeningBase == 0m && sum.Count == 0)
            {
                continue;
            }

            bool foreign = baseCurrency.Length > 0 && !string.Equals(bank.CurrencyCode, baseCurrency, StringComparison.OrdinalIgnoreCase);
            decimal received = sum.DebitBase - rev.DebitBase;
            decimal spent = sum.CreditBase - rev.CreditBase;
            decimal revalued = rev.DebitBase - rev.CreditBase;
            decimal opening = sum.OpeningBase;

            accounts.Add(new BankSummaryAccountDto
            {
                BankAccountId = bank.BankAccountId,
                AccountName = bank.AccountName,
                AccountNumber = bank.AccountNumber,
                AccountType = bank.AccountType,
                CurrencyCode = bank.CurrencyCode,
                IsActive = bank.IsActive,
                IsForeign = foreign,
                OpeningBalance = opening,
                Received = received,
                Spent = spent,
                Revaluation = revalued,
                ClosingBalance = opening + received - spent + revalued,
                OwnOpeningBalance = foreign ? sum.OpeningOwn - rev.OpeningOwn : null,
                OwnReceived = foreign ? sum.DebitOwn - rev.DebitOwn : null,
                OwnSpent = foreign ? sum.CreditOwn - rev.CreditOwn : null,
                OwnClosingBalance = foreign ? (sum.OpeningOwn - rev.OpeningOwn) + (sum.DebitOwn - rev.DebitOwn) - (sum.CreditOwn - rev.CreditOwn) : null,
            });
        }

        return new BankSummaryReportResultDto
        {
            FromDate = start,
            ToDate = end,
            BaseCurrencyCode = baseCurrency,
            Accounts = accounts,
            TotalOpeningBalance = accounts.Sum(a => a.OpeningBalance),
            TotalReceived = accounts.Sum(a => a.Received),
            TotalSpent = accounts.Sum(a => a.Spent),
            TotalRevaluation = accounts.Sum(a => a.Revaluation),
            TotalClosingBalance = accounts.Sum(a => a.ClosingBalance),
        };
    }
}

/// <summary>A bank account's ledger account summed, before it is joined to the account.</summary>
public sealed class BankSum
{
    public long BankAccountId { get; set; }

    /// <summary>Debits less credits dated before the period, in base currency.</summary>
    public decimal OpeningBase { get; set; }

    public decimal DebitBase { get; set; }

    public decimal CreditBase { get; set; }

    /// <summary>The same, counting an amount in the account's currency as it is and any other at its base amount.</summary>
    public decimal OpeningOwn { get; set; }

    public decimal DebitOwn { get; set; }

    public decimal CreditOwn { get; set; }

    /// <summary>How many legs the period has.</summary>
    public int Count { get; set; }
}

/// <summary>A bank account's base-currency figures for the period: the revaluation taken out of what was received and spent.</summary>
public sealed class BankFigures
{
    public long BankAccountId { get; set; }

    public decimal OpeningBase { get; set; }

    public decimal ReceivedBase { get; set; }

    public decimal SpentBase { get; set; }

    public decimal RevaluationBase { get; set; }

    /// <summary>How many legs the period has.</summary>
    public int Count { get; set; }
}
