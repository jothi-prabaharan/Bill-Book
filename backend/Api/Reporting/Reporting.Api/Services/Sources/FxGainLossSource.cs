using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public sealed class FxGainLossSource : ReportSource<FxGainLossRow>
{
    public override string ReportKey => "fx-gain-loss";

    public override string Title => "Foreign Currency Gain or Loss";

    public override ReportModule Module => ReportModule.Accounting;

    public override string RequiredPermission => "accounting.view";

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        new() { Name = "asOf", Label = "As Of", DataType = ColumnDataType.Date },
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

        // Unrealized (null until revaluation job exists)
        ReportColumn.Of<FxGainLossRow, decimal?>("unrealizedAmount", ColumnDataType.Money, r => r.UnrealizedAmount, AggregateFunction.Sum),
        ReportColumn.Of<FxGainLossRow, decimal?>("unrealizedExposure", ColumnDataType.Money, r => r.UnrealizedExposure, AggregateFunction.Sum),
        ReportColumn.Of<FxGainLossRow, decimal?>("unrealizedYtd", ColumnDataType.Money, r => r.UnrealizedYtd, AggregateFunction.Sum),

        // Net
        ReportColumn.Of<FxGainLossRow, decimal?>("netAmount", ColumnDataType.Money, r => r.NetAmount, AggregateFunction.Sum),
        ReportColumn.Of<FxGainLossRow, decimal?>("netExposure", ColumnDataType.Money, r => r.NetExposure, AggregateFunction.Sum),
        ReportColumn.Of<FxGainLossRow, decimal?>("netYtd", ColumnDataType.Money, r => r.NetYtd, AggregateFunction.Sum),

        ReportColumn.Of<FxGainLossRow, long>("accountId", ColumnDataType.Number, r => r.AccountId, filterable: false),
    ];

    protected override IQueryable<FxGainLossRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly asOf = parameters.Date("asOf") ?? DateOnly.FromDateTime(DateTime.UtcNow);

        // Get open foreign currency legs (where CurrencyCode != branch base currency)
        // Realized gain/loss is computed at settlement from JournalLedger
        // Unrealized requires period-end revaluation job (not yet implemented)

        return from l in db.Ledger
               join a in db.Accounts on l.AccountId equals a.AccountId
               where l.CurrencyCode != "INR" // Assuming INR as base; should come from branch context
               && (l.DebitAmountBase > 0 || l.CreditAmountBase > 0)
               let isDebit = l.DebitAmountBase > 0
               let baseAmount = isDebit ? l.DebitAmountBase : -l.CreditAmountBase
               let sourceAmount = isDebit ? l.DebitAmount : -l.CreditAmount
               select new FxGainLossRow
               {
                   AccountId = a.AccountId,
                   AccountName = a.AccountName,
                   CurrencyCode = l.CurrencyCode,
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
        (Expression<Func<FxGainLossRow, long>>)(r => r.AccountId);
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

    // Unrealized (null until revaluation job exists)
    public decimal? UnrealizedAmount { get; set; }
    public decimal? UnrealizedExposure { get; set; }
    public decimal? UnrealizedYtd { get; set; }

    // Net
    public decimal? NetAmount { get; set; }
    public decimal? NetExposure { get; set; }
    public decimal? NetYtd { get; set; }
}