using System.Linq.Expressions;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

/// <summary>
/// Each asset on the register with its <c>Books</c> schedule, and what that
/// schedule charged it over the period: opening and closing accumulated
/// depreciation and book value, and the disposal if there was one.
///
/// <b><c>reports.json</c> lists five columns this does not declare</b> — Brand,
/// Outlet, Warranty Expiry, Cost Limit and Averaging Method. The register holds
/// none of them, and a column that can only ever be empty is a promise the report
/// cannot keep.
/// </summary>
public sealed class DepreciationScheduleSource : ReportSource<FixedAssetPeriodRow>
{
    public override string ReportKey => "depreciation-schedule";

    public override string Title => "Depreciation Schedule";

    public override ReportModule Module => ReportModule.FixedAssets;

    public override string RequiredPermission => "accounting.view";

    public override IReadOnlyList<ReportParameter> Parameters => FixedAssetPeriod.Parameters;

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<FixedAssetPeriodRow, string>(
            "assetNumber", ColumnDataType.Text, r => r.AssetNumber, groupable: true),
        ReportColumn.Of<FixedAssetPeriodRow, string>(
            "assetName", ColumnDataType.Text, r => r.AssetName, groupable: true),
        ReportColumn.Of<FixedAssetPeriodRow, string?>(
            "description", ColumnDataType.Text, r => r.Description),
        ReportColumn.Of<FixedAssetPeriodRow, string?>(
            "serialNumber", ColumnDataType.Text, r => r.SerialNumber, groupable: true),
        ReportColumn.Of<FixedAssetPeriodRow, string>(
            "assetType", ColumnDataType.Enum, r => r.AssetType, groupable: true),
        ReportColumn.Of<FixedAssetPeriodRow, string>(
            "assetAccount", ColumnDataType.Text, r => r.AssetAccount, groupable: true),
        ReportColumn.Of<FixedAssetPeriodRow, string>(
            "accumDepAccount", ColumnDataType.Text, r => r.AccumDepAccount, groupable: true),
        ReportColumn.Of<FixedAssetPeriodRow, string>(
            "depExpenseAccount", ColumnDataType.Text, r => r.DepExpenseAccount, groupable: true),
        ReportColumn.Of<FixedAssetPeriodRow, DateOnly>(
            "purchaseDate", ColumnDataType.Date, r => r.PurchaseDate),
        ReportColumn.Of<FixedAssetPeriodRow, decimal>(
            "purchasePrice", ColumnDataType.Money, r => r.PurchasePrice, AggregateFunction.Sum),
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
            "openingAccumDep", ColumnDataType.Money, r => r.OpeningAccumDep, AggregateFunction.Sum),
        ReportColumn.Of<FixedAssetPeriodRow, decimal>(
            "depreciation", ColumnDataType.Money, r => r.Depreciation, AggregateFunction.Sum),
        ReportColumn.Of<FixedAssetPeriodRow, decimal>(
            "closingAccumDep", ColumnDataType.Money, r => r.ClosingAccumDep, AggregateFunction.Sum),
        ReportColumn.Of<FixedAssetPeriodRow, decimal>(
            "openingNbv", ColumnDataType.Money, r => r.OpeningNbv, AggregateFunction.Sum),
        ReportColumn.Of<FixedAssetPeriodRow, decimal>(
            "closingNbv", ColumnDataType.Money, r => r.ClosingNbv, AggregateFunction.Sum),
        ReportColumn.Of<FixedAssetPeriodRow, DateOnly?>(
            "disposalDate", ColumnDataType.Date, r => r.DisposalDate),
        ReportColumn.Of<FixedAssetPeriodRow, decimal?>(
            "saleProceeds", ColumnDataType.Money, r => r.SaleProceeds, AggregateFunction.Sum),
        ReportColumn.Of<FixedAssetPeriodRow, decimal?>(
            "nbvAtDisposal", ColumnDataType.Money, r => r.NbvAtDisposal, AggregateFunction.Sum),
        ReportColumn.Of<FixedAssetPeriodRow, decimal?>(
            "gainOnDisposal", ColumnDataType.Money, r => r.GainOnDisposal, AggregateFunction.Sum),
        ReportColumn.Of<FixedAssetPeriodRow, decimal?>(
            "lossOnDisposal", ColumnDataType.Money, r => r.LossOnDisposal, AggregateFunction.Sum),
        ReportColumn.Of<FixedAssetPeriodRow, decimal?>(
            "capitalGain", ColumnDataType.Money, r => r.CapitalGain, AggregateFunction.Sum),
        ReportColumn.Of<FixedAssetPeriodRow, long>(
            "fixedAssetId", ColumnDataType.Number, r => r.FixedAssetId, filterable: false),
    ];

    protected override IQueryable<FixedAssetPeriodRow> Build(
        ReportParameters parameters, ReportingDbContext db) =>
        FixedAssetRegister.Rows(db, parameters.Date("from"), parameters.Date("to"));

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<FixedAssetPeriodRow, long>>)(r => r.FixedAssetId);
}
