using Fee.Entity.TableEntities;
using Shared.Kernel.Numbering;

namespace Fee.Repository.SeedData;

/// <summary>What a School branch starts with in <c>fee</c> (S4, TK-64): common heads and the FDM and FRC series.</summary>
public static class FeeSeed
{
    public const string DemandSeriesCode = "FDM";
    public const string ReceiptSeriesCode = "FRC";

    /// <summary>The heads most schools bill. None names an income account, so all post to Fee Income until one is chosen.</summary>
    public static IReadOnlyList<FeeHead> Heads(Guid orgId) =>
    [
        new() { OrgId = orgId, Code = "TUITION", Name = "Tuition fee" },
        new() { OrgId = orgId, Code = "ADMISSION", Name = "Admission fee" },
        new() { OrgId = orgId, Code = "EXAM", Name = "Examination fee" },
        new() { OrgId = orgId, Code = "TRANSPORT", Name = "Transport fee" },
        new() { OrgId = orgId, Code = "CAUTION", Name = "Caution deposit", IsRefundable = true },
    ];

    public static NumberingSeries DemandSeries(Guid orgId) => Series(orgId, DemandSeriesCode, "Fee Demand", 930);

    public static NumberingSeries ReceiptSeries(Guid orgId) => Series(orgId, ReceiptSeriesCode, "Fee Receipt", 931);

    /// <summary>Document series, yearly and gapless: FDM/2627/00001.</summary>
    private static NumberingSeries Series(Guid orgId, string code, string name, int order) => new()
    {
        OrgId = orgId,
        SeriesSystemName = code,
        SeriesCode = code,
        SeriesName = name,
        SeriesFor = SeriesFor.Document,
        Prefix = code,
        Separator = "/",
        IncludeFinancialYear = true,
        FinancialYearFormat = FinancialYearFormat.Compact,
        NumberLength = 5,
        StartNumber = 1,
        NextNumber = 1,
        ResetFrequency = NumberResetFrequency.Yearly,
        AllowManualOverride = false,
        IsDefault = true,
        IsSystem = true,
        IsActive = true,
        DisplayOrder = order,
    };
}
