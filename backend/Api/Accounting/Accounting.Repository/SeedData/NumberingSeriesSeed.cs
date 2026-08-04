using Shared.Kernel.Numbering;

namespace Accounting.Repository.SeedData;

/// <summary>
/// The numbering series written when an organization is created. Accounting
/// seeds its own document series here; Sales and Purchase seed theirs as those
/// services land, because each service owns its own transaction type codes.
///
/// <b>Master series allow a manual override; document series do not.</b> A
/// jeweller who already runs an item code scheme should be able to key their
/// own. A hand-typed journal number is a different thing — the series has to run
/// consecutively within its financial year, and the database refuses the
/// override on this side.
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
        Document(orgId, 100, "JRN", "Journal Number", "JV"),
    ];

    /// <summary>
    /// A document series. Resets every financial year and carries the year in
    /// the number, because "invoice 42" means nothing without saying which
    /// year's 42 — and the reset is what keeps the sequence consecutive within
    /// the year GST asks about.
    /// </summary>
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
