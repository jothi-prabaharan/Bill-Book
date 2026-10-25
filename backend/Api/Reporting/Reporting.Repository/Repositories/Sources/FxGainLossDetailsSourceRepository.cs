using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Foreign Currency Gain or Loss Details for export, saved views and scheduled emails
/// (TK-221). One row for each foreign-currency balance leg up to the To date, with the amount
/// in the currency and in base at the rate it was booked at, and one for each posting to
/// Realized FX Gain/Loss in the period, with its gain or loss: the legs the page's
/// <see cref="ForeignCurrencyGainLossDetailsReportRepository"/> builds its documents from, so
/// the rows add up to the page's figures for each currency. The page nets a settlement
/// against the document it settles; this lists the legs, as a ledger report does.
/// <b>Both dates are required</b> (D-67): the period the realized part covers, and at its end
/// the date the balances are taken at.
///
/// <b>What it leaves empty.</b> The revaluation rate, the revalued amount, the unrealized
/// figures, the net and the exposure and year-to-date columns have no value here: a rate is a
/// figure the person running the report gives, per currency, and the page takes it. The columns
/// are declared because saved views and the seeded catalog name them; their cells are blank
/// rather than a nought that reads as a result.
/// </summary>
public sealed class FxGainLossDetailsSourceRepository : ReportSource<FxGainLossDetailsRow>
{
    private readonly IBaseCurrencyProvider _currency;

    private string _baseCurrency = string.Empty;

    public FxGainLossDetailsSourceRepository(IBaseCurrencyProvider currency) => _currency = currency;

    public override string ReportKey => "fx-gain-loss-details";

    public override string Title => "Foreign Currency Gain or Loss Details";

    public override ReportModule Module => ReportModule.Accounting;

    public override string RequiredPermission => "accounting.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date, IsRequired = true },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date, IsRequired = true },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<FxGainLossDetailsRow, string>("accountName", ColumnDataType.Text, r => r.AccountName, groupable: true),
        ReportColumn.Of<FxGainLossDetailsRow, string>("currencyCode", ColumnDataType.Text, r => r.CurrencyCode, groupable: true),
        ReportColumn.Of<FxGainLossDetailsRow, string?>("contactName", ColumnDataType.Text, r => r.ContactName),
        ReportColumn.Of<FxGainLossDetailsRow, DateOnly>("transactionDate", ColumnDataType.Date, r => r.TransactionDate),
        ReportColumn.Of<FxGainLossDetailsRow, string?>("transactionNo", ColumnDataType.Text, r => r.TransactionNo),
        ReportColumn.Of<FxGainLossDetailsRow, decimal?>("transactionFxRate", ColumnDataType.Rate, r => r.TransactionFxRate),
        ReportColumn.Of<FxGainLossDetailsRow, string?>("reference", ColumnDataType.Text, r => r.Reference),
        ReportColumn.Of<FxGainLossDetailsRow, string>("source", ColumnDataType.Text, r => r.Source, groupable: true),

        ReportColumn.Of<FxGainLossDetailsRow, decimal?>("dueSource", ColumnDataType.Money, r => r.DueSource, AggregateFunction.Sum),
        ReportColumn.Of<FxGainLossDetailsRow, decimal?>("due", ColumnDataType.Money, r => r.Due, AggregateFunction.Sum),
        ReportColumn.Of<FxGainLossDetailsRow, decimal?>("revalueFxRate", ColumnDataType.Rate, r => r.RevalueFxRate),
        ReportColumn.Of<FxGainLossDetailsRow, decimal?>("revaluedDue", ColumnDataType.Money, r => r.RevaluedDue, AggregateFunction.Sum),

        // Realized
        ReportColumn.Of<FxGainLossDetailsRow, decimal?>("realizedAmount", ColumnDataType.Money, r => r.RealizedAmount, AggregateFunction.Sum),
        ReportColumn.Of<FxGainLossDetailsRow, decimal?>("realizedExposure", ColumnDataType.Money, r => r.RealizedExposure, AggregateFunction.Sum),
        ReportColumn.Of<FxGainLossDetailsRow, decimal?>("realizedYtd", ColumnDataType.Money, r => r.RealizedYtd, AggregateFunction.Sum),

        // Unrealized (null until revaluation job exists)
        ReportColumn.Of<FxGainLossDetailsRow, decimal?>("unrealizedAmount", ColumnDataType.Money, r => r.UnrealizedAmount, AggregateFunction.Sum),
        ReportColumn.Of<FxGainLossDetailsRow, decimal?>("unrealizedExposure", ColumnDataType.Money, r => r.UnrealizedExposure, AggregateFunction.Sum),
        ReportColumn.Of<FxGainLossDetailsRow, decimal?>("unrealizedYtd", ColumnDataType.Money, r => r.UnrealizedYtd, AggregateFunction.Sum),

        // Net
        ReportColumn.Of<FxGainLossDetailsRow, decimal?>("netAmount", ColumnDataType.Money, r => r.NetAmount, AggregateFunction.Sum),
        ReportColumn.Of<FxGainLossDetailsRow, decimal?>("netExposure", ColumnDataType.Money, r => r.NetExposure, AggregateFunction.Sum),
        ReportColumn.Of<FxGainLossDetailsRow, decimal?>("netYtd", ColumnDataType.Money, r => r.NetYtd, AggregateFunction.Sum),

        ReportColumn.Of<FxGainLossDetailsRow, long>("ledgerId", ColumnDataType.Number, r => r.LedgerId, filterable: false),
    ];

    /// <summary>The base currency tells a foreign leg from a base one, and a source cannot await it in <see cref="Build"/>.</summary>
    protected override async Task PrepareAsync(ReportParameters parameters, ReportingDbContext db, CancellationToken ct) =>
        _baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

    protected override IQueryable<FxGainLossDetailsRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly start = parameters.Date("from") ?? throw new ReportQueryException("From date is required.");
        DateOnly end = parameters.Date("to") ?? throw new ReportQueryException("To date is required.");

        if (end < start)
        {
            throw new ReportQueryException("To date cannot be before the from date.");
        }

        // With no base currency known nothing can be told apart as foreign, and nothing is guessed.
        if (_baseCurrency.Length == 0)
        {
            return Enumerable.Empty<FxGainLossDetailsRow>().AsQueryable();
        }

        IQueryable<FxGainLossDetailsRow> balances =
            from l in ForeignCurrencyGainLossReportRepository.ForeignLegs(db.Ledger, db.Accounts, _baseCurrency)
            where l.LedgerDate <= end
            join a in db.Accounts on l.AccountId equals a.AccountId
            join c in db.Contacts on l.ContactId equals c.ContactId into contacts
            from c in contacts.DefaultIfEmpty()
            // Never a negative id, so always null; the CASE gives Postgres the type of a column that has no value.
            let blank = l.LedgerId < 0 ? 0m : (decimal?)null
            select new FxGainLossDetailsRow
            {
                LedgerId = l.LedgerId,
                AccountName = a.AccountName,
                CurrencyCode = l.CurrencyCode,
                ContactName = c != null ? c.DisplayName : null,
                TransactionDate = l.LedgerDate,
                TransactionNo = l.DocumentNo ?? l.Code + "-" + l.Id,
                TransactionFxRate = l.Source == 0m ? blank : l.Base / l.Source,
                Reference = null,
                Source = l.Code,
                DueSource = l.Source,
                Due = l.Base,
                RealizedAmount = blank,
                RevalueFxRate = blank,
                RevaluedDue = blank,
                RealizedExposure = blank,
                RealizedYtd = blank,
                UnrealizedAmount = blank,
                UnrealizedExposure = blank,
                UnrealizedYtd = blank,
                NetAmount = blank,
                NetExposure = blank,
                NetYtd = blank,
            };

        // The realized differences of the period, each under the currency and account its document is tied to.
        IQueryable<FxGainLossDetailsRow> realized =
            from r in ForeignCurrencyGainLossDetailsReportRepository.RealizedLegDetails(db.Ledger, db.Accounts, _baseCurrency, start, end)
            where r.AccountId != 0L
            join a in db.Accounts on r.AccountId equals a.AccountId
            let blank = r.LedgerId < 0 ? 0m : (decimal?)null
            select new FxGainLossDetailsRow
            {
                LedgerId = r.LedgerId,
                AccountName = a.AccountName,
                CurrencyCode = r.CurrencyCode,
                ContactName = null,
                TransactionDate = r.Date,
                TransactionNo = r.DocumentNo ?? r.Code + "-" + r.Id,
                TransactionFxRate = blank,
                Reference = null,
                Source = r.Code,
                DueSource = blank,
                Due = blank,
                RealizedAmount = r.Amount,
                RevalueFxRate = blank,
                RevaluedDue = blank,
                RealizedExposure = blank,
                RealizedYtd = blank,
                UnrealizedAmount = blank,
                UnrealizedExposure = blank,
                UnrealizedYtd = blank,
                NetAmount = blank,
                NetExposure = blank,
                NetYtd = blank,
            };

        // What no currency can claim stays on the Realized FX Gain/Loss account itself, so the rows add up to its movement.
        IQueryable<FxGainLossDetailsRow> unassigned =
            from r in ForeignCurrencyGainLossReportRepository.UnassignedRealizedLegs(db.Ledger, db.Accounts, _baseCurrency, start, end)
            join a in db.Accounts on r.AccountId equals a.AccountId
            let blank = r.LedgerId < 0 ? 0m : (decimal?)null
            select new FxGainLossDetailsRow
            {
                LedgerId = r.LedgerId,
                AccountName = a.AccountName,
                CurrencyCode = r.CurrencyCode,
                ContactName = null,
                TransactionDate = r.LedgerDate,
                TransactionNo = r.DocumentNo ?? r.TransactionTypeCode + "-" + r.TransactionId,
                TransactionFxRate = blank,
                Reference = r.TransactionDesc,
                Source = r.TransactionTypeCode,
                DueSource = blank,
                Due = blank,
                RealizedAmount = r.CreditAmountBase - r.DebitAmountBase,
                RevalueFxRate = blank,
                RevaluedDue = blank,
                RealizedExposure = blank,
                RealizedYtd = blank,
                UnrealizedAmount = blank,
                UnrealizedExposure = blank,
                UnrealizedYtd = blank,
                NetAmount = blank,
                NetExposure = blank,
                NetYtd = blank,
            };

        return balances.Concat(realized).Concat(unassigned);
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<FxGainLossDetailsRow, long>>)(r => r.LedgerId);
}

public sealed class FxGainLossDetailsRow
{
    public long LedgerId { get; set; }
    public string AccountName { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public string? ContactName { get; set; }
    public DateOnly TransactionDate { get; set; }
    public string? TransactionNo { get; set; }
    public decimal? TransactionFxRate { get; set; }
    public string? Reference { get; set; }
    public string Source { get; set; } = null!;
    public decimal? DueSource { get; set; }
    public decimal? Due { get; set; }
    public decimal? RevalueFxRate { get; set; }
    public decimal? RevaluedDue { get; set; }

    // Realized
    public decimal? RealizedAmount { get; set; }
    public decimal? RealizedExposure { get; set; }
    public decimal? RealizedYtd { get; set; }

    // Unrealized (null until revaluation job exists)
    public decimal? UnrealizedAmount { get; set; }
    public decimal? UnrealizedExposure { get; set; }
    public decimal? UnrealizedYtd { get; set; }

    // Net
    public decimal? NetAmount { get; set; }
    public decimal? NetExposure { get; set; }
    public decimal? NetYtd { get; set; }
}
