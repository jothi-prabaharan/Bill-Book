using Shared.Kernel.Numbering;

namespace Inventory.Repository.SeedData;

/// <summary>
/// Inventory's own document series, written when an organization is created.
///
/// Each service seeds the series for the transaction types it owns — Accounting
/// seeds <c>JRN</c> and <c>OPB</c>, Banking seeds the three money codes, and
/// <c>STA</c> is Inventory's because Inventory is what issues a stock
/// adjustment. The <c>ITEM</c> and <c>WAREHOUSE</c> master series that Inventory
/// also allocates from are seeded by Accounting, which owns the table's
/// migration; those are master codes rather than document types, and moving them
/// here would split one seed list across two services for no gain.
///
/// <b>No manual override, and a yearly reset.</b> A stock adjustment is a
/// document: it writes to the general ledger, so its numbers have to run
/// consecutively within the financial year, and a hand-typed one is refused by
/// the database rather than merely discouraged.
/// </summary>
public static class NumberingSeriesSeed
{
    public static IReadOnlyList<NumberingSeries> Build(Guid orgId) =>
    [
        Document(orgId, 300, "STA", "Stock Adjustment Number", "ADJ"),
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
