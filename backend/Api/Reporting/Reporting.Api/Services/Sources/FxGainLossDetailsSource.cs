using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public sealed class FxGainLossDetailsSource : ReportSource<FxGainLossDetailsRow>
{
    public override string ReportKey => "fx-gain-loss-details";

    public override string Title => "Foreign Currency Gain or Loss Details";

    public override ReportModule Module => ReportModule.Accounting;

    public override string RequiredPermission => "accounting.view";

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date },
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

    protected override IQueryable<FxGainLossDetailsRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly? start = parameters.Date("from");
        DateOnly? end = parameters.Date("to");

        IQueryable<Repository.ReadModels.JournalLedgerRead> ledger = db.Ledger;

        if (start is DateOnly from)
        {
            ledger = ledger.Where(l => l.LedgerDate >= from);
        }

        if (end is DateOnly to)
        {
            ledger = ledger.Where(l => l.LedgerDate <= to);
        }

        return from l in ledger
               join a in db.Accounts on l.AccountId equals a.AccountId
               join c in db.Contacts on l.ContactId equals c.ContactId into contacts
               from c in contacts.DefaultIfEmpty()
               where l.CurrencyCode != "INR" // Assuming INR as base; should come from branch context
               && (l.DebitAmountBase > 0 || l.CreditAmountBase > 0)
               let isDebit = l.DebitAmountBase > 0
               let baseAmount = isDebit ? l.DebitAmountBase : -l.CreditAmountBase
               let sourceAmount = isDebit ? l.DebitAmount : -l.CreditAmount
               select new FxGainLossDetailsRow
               {
                   LedgerId = l.LedgerId,
                   AccountName = a.AccountName,
                   CurrencyCode = l.CurrencyCode,
                   ContactName = c != null ? c.DisplayName : null,
                   TransactionDate = l.LedgerDate,
                   TransactionNo = l.TransactionTypeCode + "-" + l.TransactionId,
                   TransactionFxRate = l.ExchangeRate,
                   Reference = l.TransactionDesc,
                   Source = l.TransactionTypeCode,
                   DueSource = sourceAmount,
                   Due = baseAmount,
                   RevalueFxRate = null, // Requires revaluation job
                   RevaluedDue = null,   // Requires revaluation job
                   RealizedAmount = 0m,  // Would come from settlement entries
                   RealizedExposure = 0m,
                   RealizedYtd = 0m,
                   UnrealizedAmount = null,
                   UnrealizedExposure = null,
                   UnrealizedYtd = null,
                   NetAmount = 0m,
                   NetExposure = 0m,
                   NetYtd = 0m,
               };
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