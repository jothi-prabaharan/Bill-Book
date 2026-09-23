using Reporting.Entity.Enums;

namespace Reporting.Api.Services.Sources;

/// <summary>
/// The period every fixed-asset report is run for. Both ends are optional: with
/// no start there is no opening balance and everything is movement; with no end
/// the period runs to whatever the register holds.
/// </summary>
internal static class FixedAssetPeriod
{
    public static IReadOnlyList<ReportParameter> Parameters { get; } =
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date },
    ];
}
