using Shared.Kernel.Numbering;

namespace WorkOrder.Repository.SeedData;

/// <summary>What a School branch starts with in <c>wrk</c> (S6, TK-66): the WRK series.</summary>
public static class WorkOrderSeed
{
    public const string SeriesCode = "WRK";

    /// <summary>A document series, yearly: WRK/2627/00001.</summary>
    public static NumberingSeries Series(Guid orgId) => new()
    {
        OrgId = orgId,
        SeriesSystemName = SeriesCode,
        SeriesCode = SeriesCode,
        SeriesName = "Work Order",
        SeriesFor = SeriesFor.Document,
        Prefix = SeriesCode,
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
        DisplayOrder = 940,
    };
}
