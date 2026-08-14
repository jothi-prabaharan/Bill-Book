using Shared.Kernel.Numbering;

namespace Purchase.Repository.SeedData;

/// <summary>
/// Purchase's document series, written when an organization is created.
///
/// Each service seeds the series for the transaction types it owns: Accounting
/// seeds <c>JRN</c> and <c>OPB</c>, Banking <c>SPM</c>, <c>RCM</c> and
/// <c>TRM</c>, Sales its six. The table itself lives in <c>Shared.Kernel</c> and
/// is migrated by Accounting; Purchase maps it with <c>ExcludeFromMigrations</c>
/// so a number can still be allocated inside Purchase's own transaction — which
/// is the whole reason that table is shared rather than split five ways.
///
/// <b>No manual override, and a yearly reset</b>, as on the sales side. These are
/// document series and have to run consecutively within the financial year, so a
/// hand-typed number is refused by the database rather than merely discouraged.
///
/// <b>The vendor's number is not one of these.</b> <c>Bill.VendorBillNo</c> is
/// issued by the vendor and typed in; nothing here allocates it. The series below
/// allocates <c>DocumentNo</c>, which is ours, and the two live side by side on a
/// posted bill on purpose.
/// </summary>
public static class NumberingSeriesSeed
{
    public static IReadOnlyList<NumberingSeries> Build(Guid orgId) =>
    [
        Document(orgId, 400, "POR", "Purchase Order Number", "PO"),
        Document(orgId, 410, "GRN", "Goods Receipt Number", "GRN"),
        Document(orgId, 420, "BIL", "Bill Number", "BILL"),
        Document(orgId, 430, "DBN", "Debit Note Number", "DN"),
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
