using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Foreign Currency Gain or Loss for export, saved views and scheduled emails
/// (TK-220). One row for each account and foreign currency, with the figures of the
/// page's builders in <see cref="ForeignCurrencyGainLossReportRepository"/>: the
/// balance in the currency and in base at the rates it was booked at, and what the
/// period posted to Realized FX Gain/Loss. <b>Both dates are required</b> (D-67): the
/// period the realized part covers, and the date the balances are taken at.
///
/// <b>What it leaves empty.</b> The revaluation rate, the revalued balance, the
/// unrealized figures, the net and the exposure and year-to-date columns have no value
/// here: a rate is a figure the person running the report gives, per currency, and the
/// page takes it. The columns are declared because saved views and the seeded catalog
/// name them; their cells are blank rather than a nought that reads as a result.
/// </summary>
public sealed class FxGainLossSourceRepository : ReportSource<FxGainLossRow>
{
    private readonly IBaseCurrencyProvider _currency;

    private string _baseCurrency = string.Empty;

    public FxGainLossSourceRepository(IBaseCurrencyProvider currency) => _currency = currency;

    public override string ReportKey => "fx-gain-loss";

    public override string Title => "Foreign Currency Gain or Loss";

    public override ReportModule Module => ReportModule.Accounting;

    public override string RequiredPermission => "accounting.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date, IsRequired = true },
        new() { Name = "asOf", Label = "As At", DataType = ColumnDataType.Date, IsRequired = true },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<FxGainLossRow, string>("accountName", ColumnDataType.Text, r => r.AccountName, groupable: true),
        ReportColumn.Of<FxGainLossRow, string>("currencyCode", ColumnDataType.Text, r => r.CurrencyCode, groupable: true),
        ReportColumn.Of<FxGainLossRow, decimal?>("dueSource", ColumnDataType.Money, r => r.DueSource, AggregateFunction.Sum),
        ReportColumn.Of<FxGainLossRow, decimal?>("due", ColumnDataType.Money, r => r.Due, AggregateFunction.Sum),
        ReportColumn.Of<FxGainLossRow, decimal?>("revalueFxRate", ColumnDataType.Rate, r => r.RevalueFxRate),
        ReportColumn.Of<FxGainLossRow, decimal?>("revaluedDue", ColumnDataType.Money, r => r.RevaluedDue, AggregateFunction.Sum),

        // Realized
        ReportColumn.Of<FxGainLossRow, decimal?>("realizedAmount", ColumnDataType.Money, r => r.RealizedAmount, AggregateFunction.Sum),
        ReportColumn.Of<FxGainLossRow, decimal?>("realizedExposure", ColumnDataType.Money, r => r.RealizedExposure, AggregateFunction.Sum),
        ReportColumn.Of<FxGainLossRow, decimal?>("realizedYtd", ColumnDataType.Money, r => r.RealizedYtd, AggregateFunction.Sum),

        // Unrealized
        ReportColumn.Of<FxGainLossRow, decimal?>("unrealizedAmount", ColumnDataType.Money, r => r.UnrealizedAmount, AggregateFunction.Sum),
        ReportColumn.Of<FxGainLossRow, decimal?>("unrealizedExposure", ColumnDataType.Money, r => r.UnrealizedExposure, AggregateFunction.Sum),
        ReportColumn.Of<FxGainLossRow, decimal?>("unrealizedYtd", ColumnDataType.Money, r => r.UnrealizedYtd, AggregateFunction.Sum),

        // Net
        ReportColumn.Of<FxGainLossRow, decimal?>("netAmount", ColumnDataType.Money, r => r.NetAmount, AggregateFunction.Sum),
        ReportColumn.Of<FxGainLossRow, decimal?>("netExposure", ColumnDataType.Money, r => r.NetExposure, AggregateFunction.Sum),
        ReportColumn.Of<FxGainLossRow, decimal?>("netYtd", ColumnDataType.Money, r => r.NetYtd, AggregateFunction.Sum),

        ReportColumn.Of<FxGainLossRow, long>("accountId", ColumnDataType.Number, r => r.AccountId, filterable: false),
    ];

    /// <summary>The base currency tells a foreign leg from a base one, and a source cannot await it in <see cref="Build"/>.</summary>
    protected override async Task PrepareAsync(ReportParameters parameters, ReportingDbContext db, CancellationToken ct) =>
        _baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

    protected override IQueryable<FxGainLossRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly start = parameters.Date("from") ?? throw new ReportQueryException("From date is required.");
        DateOnly asOf = parameters.Date("asOf") ?? throw new ReportQueryException("As at date is required.");

        if (asOf < start)
        {
            throw new ReportQueryException("As at date cannot be before the from date.");
        }

        // With no base currency known nothing can be told apart as foreign, and nothing is guessed.
        if (_baseCurrency.Length == 0)
        {
            return Enumerable.Empty<FxGainLossRow>().AsQueryable();
        }

        IQueryable<FxFigures> figures = Figures(db.Ledger, db.Accounts, _baseCurrency, start, asOf);

        return from f in figures
               join a in db.Accounts on f.AccountId equals a.AccountId
               where f.DueSource != 0m || f.CarryingBase != 0m || f.Realized != 0m
               select new FxGainLossRow
               {
                   AccountId = a.AccountId,
                   AccountName = a.AccountName,
                   CurrencyCode = f.CurrencyCode,
                   DueSource = f.DueSource,
                   Due = f.CarryingBase,
                   RealizedAmount = f.Realized,
                   RevalueFxRate = f.Blank,
                   RevaluedDue = f.Blank,
                   RealizedExposure = f.Blank,
                   RealizedYtd = f.Blank,
                   UnrealizedAmount = f.Blank,
                   UnrealizedExposure = f.Blank,
                   UnrealizedYtd = f.Blank,
                   NetAmount = f.Blank,
                   NetExposure = f.Blank,
                   NetYtd = f.Blank,
               };
    }

    /// <summary>
    /// The figures of the page for every account and currency with any: the balances
    /// and the realized part of the period, with what no currency can claim, put end to end
    /// and summed again, rather than one being looked up from the other for each account. LINQ only; the typed
    /// <c>0m</c> is what lets Postgres sum the union.
    /// </summary>
    public static IQueryable<FxFigures> Figures(
        IQueryable<JournalLedgerRead> ledger, IQueryable<AccountRead> accounts, string baseCurrency, DateOnly start, DateOnly asOf)
    {
        IQueryable<FxFigures> balances = ForeignCurrencyGainLossReportRepository.Balances(ledger, accounts, baseCurrency, asOf)
            .Select(b => new FxFigures { AccountId = b.AccountId, CurrencyCode = b.CurrencyCode, DueSource = b.DueSource, CarryingBase = b.CarryingBase, Realized = 0m });

        IQueryable<FxFigures> realized = ForeignCurrencyGainLossReportRepository.Realized(ledger, accounts, baseCurrency, start, asOf)
            .Where(r => r.AccountId != 0L && r.CurrencyCode != "")
            .Select(r => new FxFigures { AccountId = r.AccountId, CurrencyCode = r.CurrencyCode, DueSource = 0m, CarryingBase = 0m, Realized = r.Amount });

        // What no currency can claim stays on the Realized FX Gain/Loss account itself, in the
        // currency it was posted in, so the rows add up to that account's movement on the Profit and Loss.
        IQueryable<FxFigures> unassigned = ForeignCurrencyGainLossReportRepository.UnassignedRealizedLegs(ledger, accounts, baseCurrency, start, asOf)
            .GroupBy(r => new { r.AccountId, r.CurrencyCode })
            .Select(g => new FxFigures { AccountId = g.Key.AccountId, CurrencyCode = g.Key.CurrencyCode, DueSource = 0m, CarryingBase = 0m, Realized = g.Sum(r => r.CreditAmountBase - r.DebitAmountBase) });

        return balances
            .Concat(realized)
            .Concat(unassigned)
            .GroupBy(f => new { f.AccountId, f.CurrencyCode })
            .Select(g => new FxFigures
            {
                AccountId = g.Key.AccountId,
                CurrencyCode = g.Key.CurrencyCode,
                DueSource = g.Sum(f => f.DueSource),
                CarryingBase = g.Sum(f => f.CarryingBase),
                Realized = g.Sum(f => f.Realized),

                // Never a currency-less row, so always null; the CASE gives Postgres the type.
                Blank = g.Key.CurrencyCode == null ? 0m : (decimal?)null,
            });
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<FxGainLossRow, long>>)(r => r.AccountId);
}

/// <summary>An account's balance and realized figure in one foreign currency, before it is joined to the account.</summary>
public sealed class FxFigures
{
    public long AccountId { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal DueSource { get; set; }

    public decimal CarryingBase { get; set; }

    public decimal Realized { get; set; }

    /// <summary>
    /// Always null, and typed. The columns this source leaves empty are projected from it:
    /// a bare NULL in a sub-query has no type, and the runner sums those columns, which
    /// Postgres refuses as "function sum(unknown) is not unique" (the 500 this source gave).
    /// </summary>
    public decimal? Blank { get; set; }
}

public sealed class FxGainLossRow
{
    public long AccountId { get; set; }
    public string AccountName { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public decimal? DueSource { get; set; }
    public decimal? Due { get; set; }
    public decimal? RevalueFxRate { get; set; }
    public decimal? RevaluedDue { get; set; }

    // Realized
    public decimal? RealizedAmount { get; set; }
    public decimal? RealizedExposure { get; set; }
    public decimal? RealizedYtd { get; set; }

    // Unrealized, which needs a rate the page is given and this source is not
    public decimal? UnrealizedAmount { get; set; }
    public decimal? UnrealizedExposure { get; set; }
    public decimal? UnrealizedYtd { get; set; }

    // Net
    public decimal? NetAmount { get; set; }
    public decimal? NetExposure { get; set; }
    public decimal? NetYtd { get; set; }
}
