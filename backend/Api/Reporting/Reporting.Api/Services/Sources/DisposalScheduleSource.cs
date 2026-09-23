using System.Linq.Expressions;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

/// <summary>
/// The assets disposed of within the period: what each cost, what it was worth
/// on the day it went, what it sold for, and the gain or loss between the two.
///
/// <b>The gain or loss here is the register's arithmetic, not the ledger's.</b>
/// Disposal posts nothing yet (D-20), so these figures are what the journal
/// <i>should</i> say rather than what it does; the reconciliation report shows
/// the difference.
///
/// Mapped from <c>reports.json</c>: <b>Purchased</b> is the purchase price,
/// <b>Disposed</b> the cost taken off the register, <b>AssetValue</b> the book
/// value on the disposal date and <b>Sale Price</b> the proceeds. Avg Method and
/// CostLimit are not declared, because the register holds neither. Disposal date,
/// accumulated depreciation and gain on disposal are declared beyond the file: a
/// disposal schedule without its date, or with a loss column and no gain column,
/// cannot be read.
/// </summary>
public sealed class DisposalScheduleSource : ReportSource<FixedAssetPeriodRow>
{
    public override string ReportKey => "disposal-schedule";

    public override string Title => "Disposal Schedule Report";

    public override ReportModule Module => ReportModule.FixedAssets;

    public override string RequiredPermission => "accounting.view";

    public override IReadOnlyList<ReportParameter> Parameters => FixedAssetPeriod.Parameters;

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<FixedAssetPeriodRow, string>(
            "assetNumber", ColumnDataType.Text, r => r.AssetNumber, groupable: true),
        ReportColumn.Of<FixedAssetPeriodRow, string>(
            "assetName", ColumnDataType.Text, r => r.AssetName, groupable: true),
        ReportColumn.Of<FixedAssetPeriodRow, string>(
            "assetType", ColumnDataType.Enum, r => r.AssetType, groupable: true),
        ReportColumn.Of<FixedAssetPeriodRow, DateOnly?>(
            "disposalDate", ColumnDataType.Date, r => r.DisposalDate),
        ReportColumn.Of<FixedAssetPeriodRow, decimal>(
            "purchased", ColumnDataType.Money, r => r.PurchasePrice, AggregateFunction.Sum),
        ReportColumn.Of<FixedAssetPeriodRow, decimal>(
            "cost", ColumnDataType.Money, r => r.Cost, AggregateFunction.Sum),
        ReportColumn.Of<FixedAssetPeriodRow, string?>(
            "depMethod", ColumnDataType.Enum, r => r.DepMethod, groupable: true),
        ReportColumn.Of<FixedAssetPeriodRow, DateOnly?>(
            "depStartDate", ColumnDataType.Date, r => r.DepStartDate),
        ReportColumn.Of<FixedAssetPeriodRow, decimal?>(
            "rate", ColumnDataType.Percent, r => r.Rate),
        ReportColumn.Of<FixedAssetPeriodRow, int?>(
            "effectiveLife", ColumnDataType.Number, r => r.EffectiveLife),
        ReportColumn.Of<FixedAssetPeriodRow, decimal?>(
            "residualValue", ColumnDataType.Money, r => r.ResidualValue, AggregateFunction.Sum),
        ReportColumn.Of<FixedAssetPeriodRow, decimal>(
            "disposed", ColumnDataType.Money, r => r.DisposalCost, AggregateFunction.Sum),
        ReportColumn.Of<FixedAssetPeriodRow, decimal>(
            "accumDep", ColumnDataType.Money, r => r.DisposalAccumDep, AggregateFunction.Sum),
        ReportColumn.Of<FixedAssetPeriodRow, decimal?>(
            "assetValue", ColumnDataType.Money, r => r.NbvAtDisposal, AggregateFunction.Sum),
        ReportColumn.Of<FixedAssetPeriodRow, decimal?>(
            "salePrice", ColumnDataType.Money, r => r.SaleProceeds, AggregateFunction.Sum),
        ReportColumn.Of<FixedAssetPeriodRow, decimal?>(
            "gain", ColumnDataType.Money, r => r.GainOnDisposal, AggregateFunction.Sum),
        ReportColumn.Of<FixedAssetPeriodRow, decimal?>(
            "capitalGain", ColumnDataType.Money, r => r.CapitalGain, AggregateFunction.Sum),
        ReportColumn.Of<FixedAssetPeriodRow, decimal?>(
            "loss", ColumnDataType.Money, r => r.LossOnDisposal, AggregateFunction.Sum),
        ReportColumn.Of<FixedAssetPeriodRow, long>(
            "assetId", ColumnDataType.Number, r => r.FixedAssetId, filterable: false),
        ReportColumn.Of<FixedAssetPeriodRow, long>(
            "assetTypeId", ColumnDataType.Number, r => r.FixedAssetCategoryId, filterable: false),
    ];

    /// <summary>
    /// The register's rows, kept to those disposed of within the period — which
    /// is exactly the rows whose disposal date is set, because the register sets
    /// it for no other.
    /// </summary>
    protected override IQueryable<FixedAssetPeriodRow> Build(
        ReportParameters parameters, ReportingDbContext db) =>
        FixedAssetRegister
            .Rows(db, parameters.Date("from"), parameters.Date("to"))
            .Where(r => r.DisposalDate != null);

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<FixedAssetPeriodRow, long>>)(r => r.FixedAssetId);
}
