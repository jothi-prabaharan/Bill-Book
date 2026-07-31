using Shared.Kernel.Numbering;

namespace Accounting.Repository.SeedData;

/// <summary>
/// The numbering series written when an organization is created. Master series
/// only — document series are seeded by Sales and Purchase as those services
/// land, because each needs its own transaction type code.
///
/// Master series allow a manual override: a jeweller who already runs an item
/// code scheme should be able to key their own. Document series will not, and
/// the database refuses it.
/// </summary>
public static class NumberingSeriesSeed
{
    public static IReadOnlyList<NumberingSeries> Build(Guid orgId) =>
    [
        Master(orgId, 10, "CUSTOMER", "Customer Code", "CUST", 5),
        Master(orgId, 20, "VENDOR", "Vendor Code", "VEND", 5),
        Master(orgId, 30, "ITEM", "Item Code", "ITM", 5),
        Master(orgId, 40, "WAREHOUSE", "Warehouse Code", "WH", 3),
        Master(orgId, 50, "BANK", "Bank Code", "BNK", 3),
    ];

    private static NumberingSeries Master(
        Guid orgId, int displayOrder, string code, string name, string prefix, int length) =>
        new()
        {
            OrgId = orgId,
            SeriesSystemName = code,
            SeriesCode = code,
            SeriesName = name,
            SeriesFor = SeriesFor.Master,
            Prefix = prefix,
            Separator = "-",
            IncludeFinancialYear = false,
            FinancialYearFormat = FinancialYearFormat.Compact,
            NumberLength = length,
            StartNumber = 1,
            NextNumber = 1,
            ResetFrequency = NumberResetFrequency.Never,
            AllowManualOverride = true,
            IsDefault = true,
            IsSystem = true,
            IsActive = true,
            DisplayOrder = displayOrder,
        };
}
