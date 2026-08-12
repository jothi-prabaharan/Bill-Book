using Shared.Kernel.Numbering;

namespace Sales.Repository.SeedData;

/// <summary>
/// Sales' document series, written when an organization is created.
///
/// Each service seeds the series for the transaction types it owns: Accounting
/// seeds <c>JRN</c> and <c>OPB</c>, Banking <c>SPM</c>, <c>RCM</c> and
/// <c>TRM</c>, Purchase its own. The table itself lives in
/// <c>Shared.Kernel</c> and is migrated by Accounting; Sales maps it with
/// <c>ExcludeFromMigrations</c> so a number can still be allocated inside Sales'
/// own transaction — which is the whole reason that table is shared rather than
/// split five ways.
///
/// <b><c>DLC</c> is deliberately absent.</b> The delivery challan needs a
/// seventeenth <c>mst.TransactionTypes</c> row that does not exist yet, and a
/// series pointing at a transaction type the master has never heard of is a
/// number that resolves to nothing. It is seeded by T3.6, alongside the row.
///
/// <b>No manual override, and a yearly reset.</b> These are document series: they
/// have to run consecutively within the financial year — statutory on an Indian
/// invoice — so a hand-typed number is refused by the database rather than merely
/// discouraged.
/// </summary>
public static class NumberingSeriesSeed
{
    public static IReadOnlyList<NumberingSeries> Build(Guid orgId) =>
    [
        Document(orgId, 300, "QTE", "Quote Number", "QT"),
        Document(orgId, 310, "SOR", "Sales Order Number", "SO"),
        Document(orgId, 320, "INV", "Invoice Number", "INV"),
        Document(orgId, 330, "CRN", "Credit Note Number", "CN"),

        // Its own series rather than sharing the invoice's. A till and the back
        // office both issue invoices, and a shop that cannot tell which came
        // from where cannot reconcile a till at close.
        Document(orgId, 340, "POS", "POS Sale Number", "POS"),
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
