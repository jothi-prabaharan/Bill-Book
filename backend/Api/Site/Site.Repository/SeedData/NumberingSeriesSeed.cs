using Shared.Kernel.Numbering;

namespace Site.Repository.SeedData;

/// <summary>
/// Site document series (TK-169): SIT, SDP, SRT, SUS, SBL.
/// </summary>
public static class NumberingSeriesSeed
{
    public static IReadOnlyList<NumberingSeries> Build(Guid orgId) =>
    [
        Document(orgId, 900, "SIT", "Site Job Number", "SIT"),
        Document(orgId, 910, "SDP", "Site Dispatch Number", "SDP"),
        Document(orgId, 920, "SRT", "Site Return Number", "SRT"),
        Document(orgId, 930, "SUS", "Site Material Usage Number", "SUS"),
        Document(orgId, 940, "SBL", "Site Billing Invoice Number", "SBL"),
    ];

    private static NumberingSeries Document(
        Guid orgId, int displayOrder, string code, string name, string prefix) =>
        new()
        {
            OrgId = orgId,
            SeriesSystemName = code,
            SeriesCode = code,
            SeriesName = name,
            SeriesFor = SeriesFor.Document,
            Prefix = prefix,
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
            DisplayOrder = displayOrder,
        };
}
