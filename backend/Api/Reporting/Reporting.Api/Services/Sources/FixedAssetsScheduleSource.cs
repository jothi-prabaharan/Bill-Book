using System.Linq.Expressions;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

/// <summary>
/// The register as a roll-forward: for each asset, opening cost plus additions
/// less disposals is closing cost, and the same for accumulated depreciation —
/// the movement schedule that sits behind the fixed-asset note to a balance sheet.
///
/// Each line of the roll-forward adds up on its own row, so the column totals add
/// up too. <b>Brand, Outlet, Warranty Expiry, Cost Limit and Averaging Method</b>
/// are in <c>reports.json</c> and not here: the register holds none of them.
/// </summary>
public sealed class FixedAssetsScheduleSource : ReportSource<FixedAssetPeriodRow>
{
    public override string ReportKey => "fixed-assets-schedule";

    public override string Title => "Fixed Assets Schedule";

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
            "openingCost", ColumnDataType.Money, r => r.OpeningCost, AggregateFunction.Sum),
        ReportColumn.Of<FixedAssetPeriodRow, decimal>(
            "additionCost", ColumnDataType.Money, r => r.AdditionCost, AggregateFunction.Sum),
        ReportColumn.Of<FixedAssetPeriodRow, decimal>(
            "disposalCost", ColumnDataType.Money, r => r.DisposalCost, AggregateFunction.Sum),
        ReportColumn.Of<FixedAssetPeriodRow, decimal>(
            "closingCost", ColumnDataType.Money, r => r.ClosingCost, AggregateFunction.Sum),
        ReportColumn.Of<FixedAssetPeriodRow, decimal>(
            "openingAccumDep", ColumnDataType.Money, r => r.OpeningAccumDep, AggregateFunction.Sum),
        ReportColumn.Of<FixedAssetPeriodRow, decimal>(
            "depreciation", ColumnDataType.Money, r => r.Depreciation, AggregateFunction.Sum),
        ReportColumn.Of<FixedAssetPeriodRow, decimal>(
            "disposalAccumDep", ColumnDataType.Money, r => r.DisposalAccumDep, AggregateFunction.Sum),
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
        ReportColumn.Of<FixedAssetPeriodRow, long>(
            "fixedAssetId", ColumnDataType.Number, r => r.FixedAssetId, filterable: false),
    ];

    protected override IQueryable<FixedAssetPeriodRow> Build(
        ReportParameters parameters, ReportingDbContext db) =>
        FixedAssetRegister.Rows(db, parameters.Date("from"), parameters.Date("to"));

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<FixedAssetPeriodRow, long>>)(r => r.FixedAssetId);
}
