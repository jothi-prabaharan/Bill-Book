using Shared.Kernel.Numbering;

namespace Admission.Repository.SeedData;

/// <summary>What a School branch starts with in <c>adm</c> (S2, TK-62): the APL series.</summary>
public static class AdmissionSeed
{
    public const string ApplicationSeriesCode = "APL";

    /// <summary>A document series, yearly: APL/2627/00001.</summary>
    public static NumberingSeries ApplicationSeries(Guid orgId) => new()
    {
        OrgId = orgId,
        SeriesSystemName = ApplicationSeriesCode,
        SeriesCode = ApplicationSeriesCode,
        SeriesName = "Admission Application",
        SeriesFor = SeriesFor.Document,
        Prefix = ApplicationSeriesCode,
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
        DisplayOrder = 920,
    };
}
